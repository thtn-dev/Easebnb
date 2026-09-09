import { QueryClient } from "@tanstack/react-query";

import { isRetryableError } from "@/lib/api/errors";

export function makeQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // Navigating back and forth within a minute reuses cached data
        // instead of flashing loading states; data still refreshes after.
        staleTime: 60 * 1000,
        gcTime: 5 * 60 * 1000,
        // Only retry failures that can heal (network, timeout, 5xx).
        // Retrying a 4xx (validation, auth) just repeats the same failure.
        retry: (failureCount, error) => isRetryableError(error) && failureCount < 2,
        // Refetching on window focus surprises users with flickering UI;
        // data refreshes on navigation and after mutations instead.
        refetchOnWindowFocus: false,
      },
      mutations: {
        // Mutations are user-intentional writes; silent retries could
        // double-apply them, so surface errors to the UI instead.
        retry: false,
      },
    },
  });
}

let browserQueryClient: QueryClient | undefined;

/**
 * Canonical TanStack Query pattern for the Next.js App Router:
 * a fresh client per server render (never shared across requests) and a
 * singleton in the browser (shared across navigations/re-renders).
 */
export function getQueryClient(): QueryClient {
  if (typeof window === "undefined") {
    return makeQueryClient();
  }
  browserQueryClient ??= makeQueryClient();
  return browserQueryClient;
}
