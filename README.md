# Observability-Ready Microservices Reference Stack

This repository now contains a small reference microservices platform built to demonstrate:

- Structured logging with `Winston` in Node.js and `Serilog` in ASP.NET Core
- Metrics exposed to `Prometheus`
- Dashboards provisioned automatically in `Grafana`
- Distributed tracing with `OpenTelemetry`
- Local orchestration with `Docker Compose`
- Kubernetes deployment manifests with health probes
- GitHub Actions for build, test, image publish, and deploy

## Services

- `services/catalog-service`
  Node.js/TypeScript API exposing catalog endpoints, `/metrics`, `/healthz`, and `/readyz`
- `services/orders-service`
  ASP.NET Core API exposing order endpoints, `/metrics`, `/healthz`, and `/readyz`

`orders-service` calls `catalog-service`, so the tracing pipeline produces a simple cross-service flow out of the box.

## Local Run

```bash
docker compose up --build
```

After the stack starts:

- Catalog API: `http://localhost:3000`
- Orders API: `http://localhost:8080`
- Prometheus: `http://localhost:9090`
- Grafana: `http://localhost:3001` with `admin / admin`
- Tempo: `http://localhost:3200`

Useful endpoints:

- `GET /healthz`
- `GET /readyz`
- `GET /metrics`
- `GET /api/catalog/items`
- `GET /api/orders`
- `POST /api/orders`

Example order creation:

```bash
curl -X POST http://localhost:8080/api/orders \
  -H "Content-Type: application/json" \
  -d '{"itemId":"sku-1001","quantity":2}'
```

## CI/CD

The workflow file lives at `.github/workflows/ci-cd.yml` and does the following:

1. Installs dependencies and runs build/test for both services.
2. Builds Docker images for both services.
3. Pushes the images to GitHub Container Registry on `push` to `main`.
4. Deploys to Kubernetes when `KUBE_CONFIG` is available as a base64-encoded secret.

Image names follow this pattern:

- `ghcr.io/<owner>/<repo>-catalog-service:<tag>`
- `ghcr.io/<owner>/<repo>-orders-service:<tag>`

## Kubernetes

Apply the manifests with Kustomize:

```bash
kubectl apply -k k8s
```

Port-forward the dashboards locally if needed:

```bash
kubectl -n observability-demo port-forward svc/grafana 3001:80
kubectl -n observability-demo port-forward svc/prometheus 9090:9090
```

The deployment script used by GitHub Actions is:

```bash
CATALOG_IMAGE=ghcr.io/acme/catalog-service:sha \
ORDERS_IMAGE=ghcr.io/acme/orders-service:sha \
bash scripts/deploy.sh
```

## Observability Layout

- Prometheus scrapes `catalog-service` and `orders-service` every 15 seconds.
- Grafana auto-loads the `Microservices Overview` dashboard.
- The OpenTelemetry Collector receives OTLP traffic on `4317` and `4318`.
- Tempo stores traces so Grafana can query them centrally.
- Both applications emit structured JSON logs to stdout for container-native log shipping.

## Local Verification

Application-level checks already exercised in this workspace:

- `npm run build`
- `npm test`
- `dotnet test services/orders-service/OrdersService.slnx`

Docker and Kubernetes commands were generated but not executed here because the current environment does not have a Docker daemon or a Kubernetes cluster configured.
