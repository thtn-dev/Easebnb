import type { ApiErrorBody } from "./types";

/** Classification of API failures, independent of any backend contract. */
export type ApiErrorKind = "http" | "timeout" | "network" | "unknown";

export interface ApiErrorInit {
  kind: ApiErrorKind;
  /** User-safe message: built from the backend body or our own generic text, never raw internals. */
  message: string;
  status?: number;
  code?: string;
  details?: unknown;
  requestId?: string;
  cause?: unknown;
}

/**
 * Normalized error thrown by every lib/api client request.
 *
 * When the backend error contract is finalized (e.g. ProblemDetails),
 * only `fromResponse`/`readErrorBody` need to change — the rest of the
 * application already consumes this shape.
 */
export class ApiError extends Error {
  readonly kind: ApiErrorKind;
  readonly status?: number;
  readonly code?: string;
  readonly details?: unknown;
  readonly requestId?: string;

  constructor(init: ApiErrorInit) {
    super(init.message);
    this.name = "ApiError";
    this.kind = init.kind;
    this.status = init.status;
    this.code = init.code;
    this.details = init.details;
    this.requestId = init.requestId;
    this.cause = init.cause;
  }

  static fromResponse(status: number, body: unknown): ApiError {
    const parsed = readErrorBody(body);
    return new ApiError({
      kind: "http",
      status: parsed.status ?? status,
      code: parsed.code,
      message: parsed.message ?? `Request failed with status ${status}.`,
      details: parsed.details,
      requestId: parsed.requestId,
    });
  }

  static timeout(cause?: unknown): ApiError {
    return new ApiError({
      kind: "timeout",
      message: "The request timed out. Please try again.",
      cause,
    });
  }

  static network(cause?: unknown): ApiError {
    return new ApiError({
      kind: "network",
      message: "Could not reach the server. Check your connection and try again.",
      cause,
    });
  }

  static unknown(cause?: unknown): ApiError {
    return new ApiError({
      kind: "unknown",
      message: "Something went wrong. Please try again.",
      cause,
    });
  }
}

/**
 * Extract known fields from an untrusted error body. Unknown shapes simply
 * produce an empty result — parsing must never throw.
 */
function readErrorBody(body: unknown): ApiErrorBody {
  if (typeof body !== "object" || body === null) {
    return {};
  }
  const record = body as Record<string, unknown>;
  const asString = (value: unknown) => (typeof value === "string" ? value : undefined);
  return {
    status: typeof record.status === "number" ? record.status : undefined,
    code: asString(record.code),
    // ProblemDetails spells the message "detail"/"title"; accept both.
    message: asString(record.message) ?? asString(record.detail) ?? asString(record.title),
    details: record.details ?? record.errors,
    requestId: asString(record.requestId) ?? asString(record.traceId),
  };
}

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError;
}

/** 401 — no session or session expired. */
export function isUnauthorizedError(error: unknown): boolean {
  return isApiError(error) && error.status === 401;
}

/** 403 — authenticated but not allowed. */
export function isForbiddenError(error: unknown): boolean {
  return isApiError(error) && error.status === 403;
}

/** Any 4xx — the client's request is wrong, retrying unchanged cannot help. */
export function isClientError(error: unknown): boolean {
  return isApiError(error) && error.status !== undefined && error.status >= 400 && error.status < 500;
}

/** Failures that can heal on their own: network, timeout, or 5xx. */
export function isRetryableError(error: unknown): boolean {
  if (!isApiError(error)) {
    return false;
  }
  if (error.kind === "network" || error.kind === "timeout") {
    return true;
  }
  return error.status !== undefined && error.status >= 500;
}

/**
 * User-safe message for any thrown value. ApiError messages are safe by
 * construction; anything else (ZodError, TypeError, ...) gets a generic
 * fallback so internals never leak into the UI.
 */
export function getErrorMessage(error: unknown, fallback = "Something went wrong. Please try again."): string {
  if (isApiError(error)) {
    return error.message;
  }
  return fallback;
}
