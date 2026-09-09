"use client";

import { useEffect } from "react";

// Importing session.ts wires the auth store into the shared API client
// (Authorization header + 401 refresh-retry) — a module side effect.
import "@/features/auth/session";

import { useAuthStore } from "@/stores/auth-store";

/** Reads the persisted auth session once on mount. Renders nothing. */
export function AuthHydration() {
  const hydrate = useAuthStore((state) => state.hydrate);

  useEffect(() => {
    hydrate();
  }, [hydrate]);

  return null;
}
