import { apiConfig } from "./config";
import { ApiError } from "./errors";
import type { RequestOptions } from "./types";

type HttpMethod = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";

/**
 * Fetch-based API client. All backend communication goes through this
 * class so cross-cutting concerns (base URL, auth, timeouts, error
 * normalization) live in exactly one place.
 */
export class ApiClient {
  private readonly baseUrl: string;

  constructor(baseUrl: string = apiConfig.baseUrl) {
    this.baseUrl = baseUrl;
  }

  get<TResponse>(path: string, options?: RequestOptions): Promise<TResponse> {
    return this.request<TResponse>("GET", path, options);
  }

  post<TResponse, TBody = unknown>(
    path: string,
    body?: TBody,
    options?: RequestOptions,
  ): Promise<TResponse> {
    return this.request<TResponse>("POST", path, options, body);
  }

  put<TResponse, TBody = unknown>(
    path: string,
    body?: TBody,
    options?: RequestOptions,
  ): Promise<TResponse> {
    return this.request<TResponse>("PUT", path, options, body);
  }

  patch<TResponse, TBody = unknown>(
    path: string,
    body?: TBody,
    options?: RequestOptions,
  ): Promise<TResponse> {
    return this.request<TResponse>("PATCH", path, options, body);
  }

  delete<TResponse>(path: string, options?: RequestOptions): Promise<TResponse> {
    return this.request<TResponse>("DELETE", path, options);
  }

  private async request<TResponse>(
    method: HttpMethod,
    path: string,
    options: RequestOptions = {},
    body?: unknown,
  ): Promise<TResponse> {
    const headers = new Headers(options.headers);
    let payload: string | undefined;
    if (body !== undefined) {
      if (!headers.has("Content-Type")) {
        headers.set("Content-Type", "application/json");
      }
      payload = JSON.stringify(body);
    }

    const timeoutController = new AbortController();
    const timeoutId = setTimeout(
      () => timeoutController.abort(),
      options.timeoutMs ?? apiConfig.timeoutMs,
    );
    // Combine the caller's signal with the timeout so either can cancel.
    const signal = options.signal
      ? AbortSignal.any([options.signal, timeoutController.signal])
      : timeoutController.signal;

    let response: Response;
    try {
      response = await fetch(this.buildUrl(path, options.query), {
        method,
        headers,
        body: payload,
        signal,
        credentials: apiConfig.credentials,
        cache: options.cache,
        ...(options.next ?? {}),
      });
    } catch (error) {
      if (timeoutController.signal.aborted) {
        throw ApiError.timeout(error);
      }
      if (options.signal?.aborted) {
        // Caller-initiated cancellation (e.g. TanStack Query unmount):
        // rethrow as-is so it is recognized as a cancel, not a failure.
        throw error;
      }
      throw ApiError.network(error);
    } finally {
      clearTimeout(timeoutId);
    }

    if (!response.ok) {
      throw ApiError.fromResponse(response.status, await readBodySafe(response));
    }

    if (response.status === 204) {
      return undefined as TResponse;
    }
    if (response.headers.get("content-type")?.includes("application/json")) {
      return (await response.json()) as TResponse;
    }
    return (await response.text()) as TResponse;
  }

  private buildUrl(path: string, query?: RequestOptions["query"]): string {
    const url = `${this.baseUrl}${path.startsWith("/") ? path : `/${path}`}`;
    if (!query) {
      return url;
    }
    const search = new URLSearchParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== null && value !== undefined) {
        search.append(key, String(value));
      }
    }
    const queryString = search.toString();
    return queryString ? `${url}?${queryString}` : url;
  }
}

async function readBodySafe(response: Response): Promise<unknown> {
  try {
    return await response.json();
  } catch {
    return undefined;
  }
}

/** Shared singleton — import this, never instantiate ApiClient in features. */
export const api = new ApiClient();
