import crypto from "node:crypto";

import type { NextFunction, Request, Response } from "express";
import express from "express";
import { trace } from "@opentelemetry/api";

import { catalogItems, findCatalogItem } from "./catalog";
import { config } from "./config";
import { httpRequestDurationSeconds, httpRequestsTotal, register } from "./metrics";
import { logger } from "./logger";

export const createApp = () => {
  const app = express();

  app.use(express.json());

  app.use((req: Request, res: Response, next: NextFunction) => {
    const requestId = req.header("x-request-id") ?? crypto.randomUUID();
    const startTime = process.hrtime.bigint();
    const activeSpan = trace.getActiveSpan();
    const spanContext = activeSpan?.spanContext();

    res.setHeader("x-request-id", requestId);

    res.on("finish", () => {
      const route = req.route?.path ?? req.path;
      const statusCode = res.statusCode.toString();
      const durationSeconds = Number(process.hrtime.bigint() - startTime) / 1_000_000_000;

      httpRequestsTotal.inc({
        method: req.method,
        route,
        status_code: statusCode
      });

      httpRequestDurationSeconds.observe(
        {
          method: req.method,
          route,
          status_code: statusCode
        },
        durationSeconds
      );

      logger.info("request_completed", {
        request_id: requestId,
        method: req.method,
        route,
        status_code: res.statusCode,
        duration_ms: Number((durationSeconds * 1000).toFixed(2)),
        trace_id: spanContext?.traceId,
        span_id: spanContext?.spanId
      });
    });

    next();
  });

  app.get("/", (_req, res) => {
    res.json({
      service: config.serviceName,
      description: "Catalog microservice with structured logs, metrics, and tracing."
    });
  });

  app.get("/healthz", (_req, res) => {
    res.json({
      status: "ok",
      service: config.serviceName
    });
  });

  app.get("/readyz", (_req, res) => {
    res.json({
      status: "ready",
      service: config.serviceName
    });
  });

  app.get("/metrics", async (_req, res) => {
    res.setHeader("Content-Type", register.contentType);
    res.send(await register.metrics());
  });

  app.get("/api/catalog/items", (_req, res) => {
    res.json({
      items: catalogItems
    });
  });

  app.get("/api/catalog/items/:id", (req, res) => {
    const item = findCatalogItem(req.params.id);
    if (!item) {
      logger.warn("catalog_item_not_found", {
        item_id: req.params.id
      });

      res.status(404).json({
        message: "Catalog item not found.",
        itemId: req.params.id
      });
      return;
    }

    res.json(item);
  });

  app.use((err: Error, _req: Request, res: Response, _next: NextFunction) => {
    logger.error("unhandled_error", {
      message: err.message,
      stack: err.stack
    });

    res.status(500).json({
      message: "Internal server error."
    });
  });

  return app;
};

