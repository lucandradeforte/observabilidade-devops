#!/usr/bin/env bash
set -euo pipefail

: "${CATALOG_IMAGE:?CATALOG_IMAGE must be set}"
: "${ORDERS_IMAGE:?ORDERS_IMAGE must be set}"

KUBECTL_BIN="${KUBECTL_BIN:-kubectl}"
NAMESPACE="${NAMESPACE:-observability-demo}"

"$KUBECTL_BIN" apply -k k8s
"$KUBECTL_BIN" -n "$NAMESPACE" set image deployment/catalog-service catalog-service="$CATALOG_IMAGE"
"$KUBECTL_BIN" -n "$NAMESPACE" set image deployment/orders-service orders-service="$ORDERS_IMAGE"
"$KUBECTL_BIN" -n "$NAMESPACE" rollout status deployment/catalog-service --timeout=180s
"$KUBECTL_BIN" -n "$NAMESPACE" rollout status deployment/orders-service --timeout=180s

