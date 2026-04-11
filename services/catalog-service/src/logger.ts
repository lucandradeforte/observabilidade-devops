import { context, trace } from "@opentelemetry/api";
import winston from "winston";

import { config } from "./config";

const enrichWithTraceContext = winston.format((info) => {
  const span = trace.getSpan(context.active());
  if (span) {
    const spanContext = span.spanContext();
    info.trace_id = spanContext.traceId;
    info.span_id = spanContext.spanId;
  }

  return info;
});

export const logger = winston.createLogger({
  level: process.env.LOG_LEVEL ?? "info",
  defaultMeta: {
    service: config.serviceName,
    environment: config.nodeEnv
  },
  format: winston.format.combine(
    winston.format.timestamp(),
    winston.format.errors({ stack: true }),
    enrichWithTraceContext(),
    winston.format.json()
  ),
  transports: [new winston.transports.Console()]
});

