using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OrdersService.Api.Tests;

public sealed class OrdersApiTests(OrdersApiFactory factory) : IClassFixture<OrdersApiFactory>
{
    [Fact]
    public async Task Healthz_ReturnsOk()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/healthz");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"status\":\"healthy\"", content);
    }

    [Fact]
    public async Task Metrics_ReturnsPrometheusPayload()
    {
        var client = factory.CreateClient();

        _ = await client.GetAsync("/api/orders");
        var response = await client.GetAsync("/metrics");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("http_request_duration_seconds", content);
    }

    [Fact]
    public async Task OrdersEndpoint_ReturnsSeedData()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/orders");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Observability Starter Kit", content);
    }
}

public sealed class OrdersApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CatalogService:BaseUrl"] = "http://127.0.0.1:65535",
                ["OpenTelemetry:OtlpEndpoint"] = "http://127.0.0.1:4317",
                ["Service:Name"] = "orders-service"
            });
        });
    }
}
