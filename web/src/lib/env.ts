import { z } from "zod";

/**
 * Client-safe environment configuration.
 *
 * Only NEXT_PUBLIC_* variables belong here — this module is imported by
 * client code, so every value in it is inlined into the browser bundle.
 * Server-only variables (e.g. API_PROXY_URL) are read directly in
 * next.config.ts and src/lib/api/config.ts instead.
 *
 * Validation must stay non-fatal while the backend does not exist yet:
 * an unset variable falls back to a default instead of crashing the build.
 */
const publicEnvSchema = z.object({
  /**
   * Base URL of the backend API as seen from the browser.
   * Empty (the default) means same-origin "/api", which the rewrite proxy
   * in next.config.ts forwards to the backend — no CORS, cookies just work.
   */
  NEXT_PUBLIC_API_URL: z
    .string()
    .default("")
    .transform((value) => value.trim().replace(/\/+$/, "")),
});

export const env = publicEnvSchema.parse({
  NEXT_PUBLIC_API_URL: process.env.NEXT_PUBLIC_API_URL,
});
