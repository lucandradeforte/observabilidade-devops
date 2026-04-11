import { NodeSDK } from "@opentelemetry/sdk-node";
import { getNodeAutoInstrumentations } from "@opentelemetry/auto-instrumentations-node";
import { resourceFromAttributes } from "@opentelemetry/resources";
import { SemanticResourceAttributes } from "@opentelemetry/semantic-conventions";
import { OTLPTraceExporter } from "@opentelemetry/exporter-trace-otlp-http";

import { config } from "./config";
import { logger } from "./logger";

let sdk: NodeSDK | undefined;

const resolveTraceEndpoint = (): string =>
  new URL("/v1/traces", config.otlpBaseUrl).toString();

export const startTelemetry = async (): Promise<void> => {
  if (config.nodeEnv === "test" || sdk) {
    return;
  }

  sdk = new NodeSDK({
    resource: resourceFromAttributes({
      [SemanticResourceAttributes.SERVICE_NAME]: config.serviceName,
      [SemanticResourceAttributes.DEPLOYMENT_ENVIRONMENT]: config.nodeEnv
    }),
    traceExporter: new OTLPTraceExporter({
      url: resolveTraceEndpoint()
    }),
    instrumentations: [getNodeAutoInstrumentations()]
  });

  await Promise.resolve(sdk.start());
  logger.info("telemetry_started", {
    otlp_endpoint: resolveTraceEndpoint()
  });
};

export const shutdownTelemetry = async (): Promise<void> => {
  if (!sdk) {
    return;
  }

  await sdk.shutdown();
  sdk = undefined;
};

