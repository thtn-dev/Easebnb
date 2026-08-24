/**
 * Contract between the shared API client and the auth feature.
 * lib/api stays free of feature imports; the auth module registers an
 * implementation at startup (see features/auth/session.ts).
 */
export interface ApiAuthBridge {
  getAccessToken(): string | null;
  /** Token type from the login response, e.g. "Bearer". */
  getTokenType(): string;
  /**
   * Refresh the session. Implementations must be single-flight so
   * concurrent 401s share one refresh request. Resolves true when a
   * fresh access token is available and the caller may retry.
   */
  refresh(): Promise<boolean>;
}

let bridge: ApiAuthBridge | null = null;

export function setApiAuthBridge(next: ApiAuthBridge): void {
  bridge = next;
}

export function getApiAuthBridge(): ApiAuthBridge | null {
  return bridge;
}
