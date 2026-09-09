import { queryOptions } from "@tanstack/react-query";

import { getHealth } from "./api";

/**
 * Query key factory: keys stay type-safe and co-located with the feature
 * instead of magic strings scattered across components.
 */
export const healthKeys = {
  all: ["health"] as const,
  status: () => [...healthKeys.all, "status"] as const,
};

export const healthQuery = queryOptions({
  queryKey: healthKeys.status(),
  queryFn: getHealth,
});
