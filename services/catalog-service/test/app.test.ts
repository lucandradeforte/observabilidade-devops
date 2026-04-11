import assert from "node:assert/strict";
import test from "node:test";

import request from "supertest";

import { createApp } from "../src/app";

const app = createApp();

test("GET /healthz returns an OK health payload", async () => {
  const response = await request(app).get("/healthz");

  assert.equal(response.status, 200);
  assert.equal(response.body.status, "ok");
  assert.equal(response.body.service, "catalog-service");
});

test("GET /api/catalog/items returns seeded catalog data", async () => {
  const response = await request(app).get("/api/catalog/items");

  assert.equal(response.status, 200);
  assert.equal(Array.isArray(response.body.items), true);
  assert.equal(response.body.items.length > 0, true);
});

test("GET /metrics exposes Prometheus metrics", async () => {
  await request(app).get("/api/catalog/items");
  const response = await request(app).get("/metrics");

  assert.equal(response.status, 200);
  assert.match(response.text, /http_request_duration_seconds/);
  assert.match(response.text, /http_requests_total/);
});
