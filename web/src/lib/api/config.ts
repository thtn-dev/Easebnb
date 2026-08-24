import { env } from "@/lib/env";

export const API_DEFAULT_TIMEOUT_MS = 15_000;

/**
 * Resolve the base URL for every API request.
 *
 * - An explicit NEXT_PUBLIC_API_URL wins everywhere (e.g. a deployed API origin).
 * - On the server, relative URLs cannot be fetched, so go straight to the
 *   backend origin (API_PROXY_URL) that the browser reaches via rewrites.
 * - In the browser, default to same-origin "/api" which next.config.ts
 *   rewrites to the backend — no CORS and cookies just work.
 */
function resolveBaseUrl(): string {
  const publicUrl = env.NEXT_PUBLIC_API_URL;
  if (publicUrl !== "") {
    return publicUrl;
  }
  const proxyUrl = process.env.API_PROXY_URL?.trim().replace(/\/+$/, "");
  if (typeof window === "undefined" && proxyUrl) {
    return `${proxyUrl}/api`;
  }
  return "/api";
}

export const apiConfig = {
  baseUrl: resolveBaseUrl(),
  timeoutMs: API_DEFAULT_TIMEOUT_MS,
  /**
   * Auth readiness: send cookies with every request so a future HttpOnly
   * session cookie works without touching any call site. A Bearer-token
   * scheme can be added later inside ApiClient only.
   */
  credentials: "include" as RequestCredentials,
};
