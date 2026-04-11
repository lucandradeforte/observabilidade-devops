import { createApp } from "./app";
import { config } from "./config";
import { logger } from "./logger";
import { shutdownTelemetry, startTelemetry } from "./telemetry";

const bootstrap = async () => {
  await startTelemetry();

  const app = createApp();
  const server = app.listen(config.port, () => {
    logger.info("service_started", {
      port: config.port
    });
  });

  const shutdown = async () => {
    logger.info("service_stopping");

    await new Promise<void>((resolve, reject) => {
      server.close((error) => {
        if (error) {
          reject(error);
          return;
        }

        resolve();
      });
    });

    await shutdownTelemetry();
    process.exit(0);
  };

  process.on("SIGINT", () => {
    void shutdown();
  });

  process.on("SIGTERM", () => {
    void shutdown();
  });
};

bootstrap().catch((error) => {
  logger.error("service_failed_to_start", {
    message: error instanceof Error ? error.message : String(error),
    stack: error instanceof Error ? error.stack : undefined
  });
  process.exit(1);
});

