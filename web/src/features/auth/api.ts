import { api } from "@/lib/api/client";
import { ApiError } from "@/lib/api/errors";

import {
  apiResponseSchema,
  authSessionResponseSchema,
  loginRequestSchema,
  refreshTokenRequestSchema,
  registerRequestSchema,
  type AuthSession,
  type LoginRequest,
  type RefreshTokenRequest,
  type RegisterRequest,
  type TokenResponse,
} from "./schemas";

const AUTH_BASE = "/v1/auth";

function toSession(data: TokenResponse): AuthSession {
  return {
    accessToken: data.accessToken,
    refreshToken: data.refreshToken,
    tokenType: data.tokenType || "Bearer",
    expiresAt: Date.now() + data.expiresIn * 1000,
    user: data.user,
  };
}

/**
 * POSTs a token endpoint and unwraps the { data, success, message }
 * envelope. HTTP errors (ProblemDetails) are already normalized to
 * ApiError by the client; here we also reject success === false bodies.
 * Resolves null when the backend answers with no body (204).
 */
async function postSession(path: string, body: unknown): Promise<AuthSession | null> {
  const response = await api.post<unknown>(`${AUTH_BASE}${path}`, body, {
    skipAuth: true,
  });
  if (response === undefined || response === null) {
    return null;
  }
  const parsed = authSessionResponseSchema.parse(response);
  if (!parsed.success || !parsed.data) {
    throw new ApiError({
      kind: "http",
      message: parsed.message ?? "Authentication failed. Please try again.",
    });
  }
  return toSession(parsed.data);
}

export async function login(request: LoginRequest): Promise<AuthSession | null> {
  return postSession("/login", loginRequestSchema.parse(request));
}

/**
 * The real backend currently answers 204 No Content on success (differs
 * from the OpenAPI-declared 200 + token envelope). A missing body means
 * the account was created but no tokens were issued — the caller must
 * not treat it as a signed-in session.
 */
export async function register(
  request: RegisterRequest,
): Promise<AuthSession | null> {
  const response = await api.post<unknown>(
    `${AUTH_BASE}/register`,
    registerRequestSchema.parse(request),
    { skipAuth: true },
  );
  if (response === undefined || response === null) {
    return null;
  }
  const parsed = authSessionResponseSchema.parse(response);
  if (!parsed.success || !parsed.data) {
    throw new ApiError({
      kind: "http",
      message: parsed.message ?? "Registration failed. Please try again.",
    });
  }
  return toSession(parsed.data);
}

export async function refreshTokens(
  request: RefreshTokenRequest,
): Promise<AuthSession | null> {
  return postSession("/refresh-token", refreshTokenRequestSchema.parse(request));
}

/** Authenticated: the client attaches the Authorization header (and may
 *  transparently refresh + retry once on 401). */
export async function logout(): Promise<void> {
  const response = await api.post<unknown>(`${AUTH_BASE}/logout`);
  const parsed = apiResponseSchema.parse(response);
  if (!parsed.success) {
    throw new ApiError({
      kind: "http",
      message: parsed.message ?? "Logout failed.",
    });
  }
}

export async function revokeToken(refreshToken: string): Promise<void> {
  const response = await api.post<unknown>(
    `${AUTH_BASE}/revoke-token`,
    refreshTokenRequestSchema.parse({
      refreshToken,
      scope: null,
      grantType: null,
      clientId: null,
      clientSecret: null,
    }),
    { skipAuth: true },
  );
  const parsed = apiResponseSchema.parse(response);
  if (!parsed.success) {
    throw new ApiError({
      kind: "http",
      message: parsed.message ?? "Failed to revoke the token.",
    });
  }
}
