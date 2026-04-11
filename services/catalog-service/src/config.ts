const parsePort = (value: string | undefined, fallback: number): number => {
  const parsed = Number.parseInt(value ?? "", 10);
  return Number.isNaN(parsed) ? fallback : parsed;
};

export const config = {
  serviceName: process.env.OTEL_SERVICE_NAME ?? "catalog-service",
  nodeEnv: process.env.NODE_ENV ?? "development",
  port: parsePort(process.env.PORT, 3000),
  otlpBaseUrl: process.env.OTEL_EXPORTER_OTLP_ENDPOINT ?? "http://otel-collector:4318"
};

