import type { Metadata } from "next";

import { HealthStatus } from "@/features/health/components/health-status";

export const metadata: Metadata = {
  title: "API health (example)",
};

/**
 * Example route proving the API foundation works end to end. Not linked
 * from the home page — safe to delete once real features exist.
 */
export default function HealthExamplePage() {
  return (
    <main className="mx-auto flex w-full max-w-xl flex-1 flex-col gap-6 p-8">
      <div className="space-y-1">
        <h1 className="text-xl font-semibold">API foundation example</h1>
        <p className="text-sm text-muted-foreground">
          Demonstrates the full data flow: useQuery → feature query → feature
          API service → API client → backend. Until the backend is running
          (set API_PROXY_URL), this page shows the standardized error state.
        </p>
      </div>
      <HealthStatus />
    </main>
  );
}
