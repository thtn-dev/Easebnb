import { setApiAuthBridge } from "@/lib/api/auth-bridge";
import { useAuthStore } from "@/stores/auth-store";

import * as authApi from "./api";
import type { LoginRequest, RegisterRequest } from "./schemas";

let inflightRefresh: Promise<boolean> | null = null;

async function performRefresh(): Promise<boolean> {
  const { refreshToken } = useAuthStore.getState();
  if (!refreshToken) {
    return false;
  }
  try {
    const session = await authApi.refreshTokens({
      refreshToken,
      scope: null,
      grantType: null,
      clientId: null,
      clientSecret: null,
    });
    if (!session) {
      useAuthStore.getState().clearSession();
      return false;
    }
    useAuthStore
      .getState()
      .setSession(session, useAuthStore.getState().rememberMe);
    return true;
  } catch {
    useAuthStore.getState().clearSession();
    return false;
  }
}

/**
 * Single-flight session refresh: concurrent 401s share one refresh
 * request. Resolves true when the session was refreshed and the caller
 * may retry its original request.
 */
export function refreshAuthSession(): Promise<boolean> {
  inflightRefresh ??= performRefresh().finally(() => {
    inflightRefresh = null;
  });
  return inflightRefresh;
}

// Wire the auth store into the shared API client — the one place where
// Authorization headers and 401 recovery are connected.
setApiAuthBridge({
  getAccessToken: () => useAuthStore.getState().accessToken,
  getTokenType: () => useAuthStore.getState().tokenType || "Bearer",
  refresh: refreshAuthSession,
});

/** Logs in and stores the session (persisted when rememberMe is set). */
export async function login(request: LoginRequest): Promise<boolean> {
  const session = await authApi.login(request);
  if (!session) {
    // No tokens in the response: signed-in state is impossible.
    return false;
  }
  useAuthStore.getState().setSession(session, request.rememberMe === true);
  return true;
}

/**
 * Registers and, when the API returns tokens, stores the session like a
 * login. Resolves false when the account was created without tokens —
 * the user must then sign in.
 */
export async function register(request: RegisterRequest): Promise<boolean> {
  const session = await authApi.register(request);
  if (!session) {
    return false;
  }
  useAuthStore.getState().setSession(session, false);
  return true;
}

/**
 * Logs out. The backend call is best-effort — the local session is
 * cleared even when it fails, so the user is always signed out of the
 * frontend.
 */
export async function logout(): Promise<void> {
  try {
    await authApi.logout();
  } catch {
    // Ignore: the server-side revocation failing must not keep the
    // user signed in locally.
  } finally {
    useAuthStore.getState().clearSession();
  }
}

/** Revokes a specific refresh token (exposed for future features). */
export async function revokeToken(refreshToken: string): Promise<void> {
  return authApi.revokeToken(refreshToken);
}
