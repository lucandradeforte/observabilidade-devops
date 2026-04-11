export type CatalogItem = {
  id: string;
  name: string;
  price: number;
  currency: string;
  stock: number;
};

export const catalogItems: CatalogItem[] = [
  {
    id: "sku-1001",
    name: "Observability Starter Kit",
    price: 149.9,
    currency: "USD",
    stock: 12
  },
  {
    id: "sku-1002",
    name: "Platform Runbook",
    price: 39.9,
    currency: "USD",
    stock: 84
  },
  {
    id: "sku-1003",
    name: "Kubernetes Operations Handbook",
    price: 79.9,
    currency: "USD",
    stock: 31
  }
];

export const findCatalogItem = (id: string): CatalogItem | undefined =>
  catalogItems.find((item) => item.id === id);

