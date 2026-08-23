/**
 * Loose description of an error payload the backend may return.
 *
 * The backend contract is not final yet, so every field is optional and
 * parsed defensively in errors.ts. ASP.NET Core's ProblemDetails
 * (status, title, detail, errors) is the most likely shape and maps
 * cleanly onto these fields.
 */
export interface ApiErrorBody {
  status?: number;
  code?: string;
  message?: string;
  details?: unknown;
  requestId?: string;
}

export type QueryParams = Record<string, string | number | boolean | null | undefined>;

export interface RequestOptions {
  /** Query parameters appended to the URL; null/undefined values are skipped. */
  query?: QueryParams;
  /** Extra headers merged over the defaults; JSON headers are set automatically when a body is present. */
  headers?: HeadersInit;
  /** Caller-provided abort signal, combined with the request timeout. */
  signal?: AbortSignal;
  /** Per-request timeout in milliseconds. Defaults to apiConfig.timeoutMs. */
  timeoutMs?: number;
  /**
   * Fetch cache mode. Next.js does not cache fetch by default, so this only
   * matters when a request should opt into caching (e.g. "force-cache").
   */
  cache?: RequestCache;
  /** Next.js-specific fetch options (revalidate, tags). Only effective server-side. */
  next?: { revalidate?: number | false; tags?: string[] };
}
