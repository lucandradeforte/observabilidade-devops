import client from "prom-client";

import { config } from "./config";

export const register = new client.Registry();

register.setDefaultLabels({
  service: config.serviceName,
  environment: config.nodeEnv
});

client.collectDefaultMetrics({
  register
});

export const httpRequestsTotal = new client.Counter({
  name: "http_requests_total",
  help: "Total number of HTTP requests handled by the catalog service.",
  labelNames: ["method", "route", "status_code"] as const,
  registers: [register]
});

export const httpRequestDurationSeconds = new client.Histogram({
  name: "http_request_duration_seconds",
  help: "Duration of HTTP requests handled by the catalog service.",
  labelNames: ["method", "route", "status_code"] as const,
  buckets: [0.05, 0.1, 0.25, 0.5, 1, 2, 5],
  registers: [register]
});

