using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

var serviceName = builder.Configuration["Service:Name"]
    ?? Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME")
    ?? "orders-service";

var catalogServiceBaseUrl = builder.Configuration["CatalogService:BaseUrl"]
    ?? Environment.GetEnvironmentVariable("CATALOG_SERVICE_BASE_URL")
    ?? "http://catalog-service:3000";

var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"]
    ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
    ?? "http://otel-collector:4317";

builder.Host.UseSerilog((_, _, loggerConfiguration) =>
{
    loggerConfiguration
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("System", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithEnvironmentName()
        .Enrich.WithProcessId()
        .Enrich.WithThreadId()
        .Enrich.WithSpan()
        .Enrich.WithProperty("service", serviceName)
        .WriteTo.Console(new RenderedCompactJsonFormatter());
});

builder.Services.AddSingleton(new ActivitySource(serviceName));
builder.Services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
builder.Services.AddHttpClient<ICatalogClient, CatalogClient>(client =>
{
    client.BaseAddress = new Uri(catalogServiceBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live", "ready"])
    .AddCheck("catalog_configuration", () => HealthCheckResult.Healthy("Catalog endpoint configured"), tags: ["ready"]);

builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource =>
    {
        resource
            .AddService(serviceName)
            .AddAttributes(
            [
                new KeyValuePair<string, object>("deployment.environment", builder.Environment.EnvironmentName)
            ]);
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(serviceName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(options => { options.Endpoint = new Uri(otlpEndpoint); });
    });

var app = builder.Build();

app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = (httpContext, _, exception) =>
        exception is null && httpContext.Response.StatusCode < 500
            ? LogEventLevel.Information
            : LogEventLevel.Error;

    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("requestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("requestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("userAgent", httpContext.Request.Headers.UserAgent.ToString());
    };
});

app.UseHttpMetrics(options =>
{
    options.AddCustomLabel("service", _ => serviceName);
    options.ReduceStatusCodeCardinality();
});

app.MapGet("/", () => Results.Ok(new
{
    service = serviceName,
    description = "Orders microservice with Serilog, Prometheus, and OpenTelemetry."
}));

app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
    ResponseWriter = WriteHealthResponseAsync
});

app.MapHealthChecks("/readyz", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponseAsync
});

app.MapMetrics("/metrics");

app.MapGet("/api/orders", (IOrderRepository repository, ActivitySource activitySource, ILoggerFactory loggerFactory) =>
{
    using var activity = activitySource.StartActivity("orders.list");
    var logger = loggerFactory.CreateLogger("OrdersList");
    var orders = repository.List();

    logger.LogInformation("Fetched {OrderCount} orders", orders.Count);
    activity?.SetTag("orders.count", orders.Count);

    return Results.Ok(new { orders });
});

app.MapGet("/api/orders/{id:guid}", (Guid id, IOrderRepository repository) =>
{
    var order = repository.Get(id);
    return order is null
        ? Results.NotFound(new { message = "Order not found.", orderId = id })
        : Results.Ok(order);
});

app.MapPost("/api/orders", async (
    CreateOrderRequest request,
    ICatalogClient catalogClient,
    IOrderRepository repository,
    ActivitySource activitySource,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    if (request.Quantity <= 0)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["quantity"] = ["Quantity must be greater than zero."]
        });
    }

    using var activity = activitySource.StartActivity("orders.create", ActivityKind.Internal);
    activity?.SetTag("order.item_id", request.ItemId);
    activity?.SetTag("order.quantity", request.Quantity);

    var logger = loggerFactory.CreateLogger("OrdersCreate");
    var item = await catalogClient.GetItemAsync(request.ItemId, cancellationToken);
    if (item is null)
    {
        logger.LogWarning("Catalog item {ItemId} was not found", request.ItemId);
        activity?.SetStatus(ActivityStatusCode.Error, "Catalog item not found");
        return Results.NotFound(new { message = "Catalog item not found.", itemId = request.ItemId });
    }

    var createdOrder = repository.Create(request, item);
    logger.LogInformation(
        "Created order {OrderId} for item {ItemId} with quantity {Quantity}",
        createdOrder.Id,
        request.ItemId,
        request.Quantity);

    activity?.SetTag("order.id", createdOrder.Id.ToString());
    return Results.Created($"/api/orders/{createdOrder.Id}", createdOrder);
});

app.Run();

static Task WriteHealthResponseAsync(HttpContext httpContext, HealthReport report)
{
    httpContext.Response.ContentType = "application/json";

    var payload = JsonSerializer.Serialize(new
    {
        status = report.Status.ToString().ToLowerInvariant(),
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString().ToLowerInvariant(),
            description = entry.Value.Description
        })
    });

    return httpContext.Response.WriteAsync(payload);
}

public sealed record CreateOrderRequest(string ItemId, int Quantity);

public sealed record OrderRecord(
    Guid Id,
    string ItemId,
    string ItemName,
    int Quantity,
    decimal UnitPrice,
    string Currency,
    DateTimeOffset CreatedAt);

public sealed class CatalogItemResponse
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public decimal Price { get; init; }
    public required string Currency { get; init; }
    public int Stock { get; init; }
}

public interface ICatalogClient
{
    Task<CatalogItemResponse?> GetItemAsync(string itemId, CancellationToken cancellationToken);
}

public sealed class CatalogClient(HttpClient httpClient, ILogger<CatalogClient> logger) : ICatalogClient
{
    public async Task<CatalogItemResponse?> GetItemAsync(string itemId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"/api/catalog/items/{itemId}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<CatalogItemResponse>(cancellationToken: cancellationToken);

        logger.LogInformation("Fetched catalog item {ItemId} from catalog-service", itemId);
        return item;
    }
}

public interface IOrderRepository
{
    IReadOnlyCollection<OrderRecord> List();
    OrderRecord? Get(Guid id);
    OrderRecord Create(CreateOrderRequest request, CatalogItemResponse item);
}

public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly Lock _syncRoot = new();
    private readonly List<OrderRecord> _orders =
    [
        new(
            Guid.Parse("2f2d9381-f843-4108-8ce4-d7ad53cd4fc4"),
            "sku-1001",
            "Observability Starter Kit",
            1,
            149.9m,
            "USD",
            DateTimeOffset.UtcNow.AddMinutes(-10))
    ];

    public IReadOnlyCollection<OrderRecord> List()
    {
        lock (_syncRoot)
        {
            return _orders.ToArray();
        }
    }

    public OrderRecord? Get(Guid id)
    {
        lock (_syncRoot)
        {
            return _orders.FirstOrDefault(order => order.Id == id);
        }
    }

    public OrderRecord Create(CreateOrderRequest request, CatalogItemResponse item)
    {
        var order = new OrderRecord(
            Guid.NewGuid(),
            item.Id,
            item.Name,
            request.Quantity,
            item.Price,
            item.Currency,
            DateTimeOffset.UtcNow);

        lock (_syncRoot)
        {
            _orders.Add(order);
        }

        return order;
    }
}

public partial class Program;
