import { api } from "@/lib/api/client";

import { healthSchema, type Health } from "./schemas";

/**
 * Example API service — demonstrates the pattern for real endpoints:
 * components never call `api` directly, they go through a feature service
 * that also validates the response at the boundary.
 */
export async function getHealth(): Promise<Health> {
  const response = await api.get<unknown>("/health");
  return healthSchema.parse(response);
}
