import { create } from "zustand";

import {
  persistedSessionSchema,
  type AuthSession,
  type UserInfo,
} from "@/features/auth/schemas";

const STORAGE_KEY = "easebnb.auth";

interface AuthState {
  user: UserInfo | null;
  accessToken: string | null;
  refreshToken: string | null;
  tokenType: string;
  expiresAt: number | null;
  rememberMe: boolean;
  isAuthenticated: boolean;
  /** True until persisted auth state has been read on the client. */
  isLoading: boolean;
  setSession: (session: AuthSession, rememberMe: boolean) => void;
  /** Replaces the stored user (e.g. after GET/PUT /account/me). */
  setUser: (user: UserInfo) => void;
  clearSession: () => void;
  hydrate: () => void;
}

/**
 * rememberMe has a real effect: false keeps the session in sessionStorage
 * (dies with the tab), true persists it in localStorage (survives restarts).
 */
function persistSession(session: AuthSession, rememberMe: boolean): void {
  if (typeof window === "undefined") {
    return;
  }
  const persisted = JSON.stringify({ ...session, rememberMe });
  try {
    if (rememberMe) {
      window.localStorage.setItem(STORAGE_KEY, persisted);
      window.sessionStorage.removeItem(STORAGE_KEY);
    } else {
      window.sessionStorage.setItem(STORAGE_KEY, persisted);
      window.localStorage.removeItem(STORAGE_KEY);
    }
  } catch {
    // Storage unavailable (e.g. private mode): keep the session in memory.
  }
}

function clearPersistedSession(): void {
  if (typeof window === "undefined") {
    return;
  }
  try {
    window.localStorage.removeItem(STORAGE_KEY);
    window.sessionStorage.removeItem(STORAGE_KEY);
  } catch {
    // Ignore storage failures — the in-memory state is cleared either way.
  }
}

function readPersistedSession() {
  if (typeof window === "undefined") {
    return null;
  }
  try {
    const raw =
      window.sessionStorage.getItem(STORAGE_KEY) ??
      window.localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return null;
    }
    // Storage is untrusted input — validate before trusting it.
    return persistedSessionSchema.safeParse(JSON.parse(raw)).data ?? null;
  } catch {
    return null;
  }
}

export const useAuthStore = create<AuthState>()((set, get) => ({
  user: null,
  accessToken: null,
  refreshToken: null,
  tokenType: "Bearer",
  expiresAt: null,
  rememberMe: false,
  isAuthenticated: false,
  isLoading: true,
  setSession: (session, rememberMe) => {
    persistSession(session, rememberMe);
    set({
      user: session.user,
      accessToken: session.accessToken,
      refreshToken: session.refreshToken,
      tokenType: session.tokenType || "Bearer",
      expiresAt: session.expiresAt,
      rememberMe,
      isAuthenticated: true,
    });
  },
  setUser: (user) => {
    const current = get();
    if (current.accessToken && current.refreshToken) {
      persistSession(
        {
          accessToken: current.accessToken,
          refreshToken: current.refreshToken,
          tokenType: current.tokenType,
          expiresAt: current.expiresAt ?? Date.now(),
          user,
        },
        current.rememberMe,
      );
    }
    set({ user });
  },
  clearSession: () => {
    clearPersistedSession();
    set({
      user: null,
      accessToken: null,
      refreshToken: null,
      tokenType: "Bearer",
      expiresAt: null,
      rememberMe: false,
      isAuthenticated: false,
    });
  },
  hydrate: () => {
    const persisted = readPersistedSession();
    if (!persisted) {
      set({ isLoading: false });
      return;
    }
    set({
      user: persisted.user,
      accessToken: persisted.accessToken,
      refreshToken: persisted.refreshToken,
      tokenType: persisted.tokenType || "Bearer",
      expiresAt: persisted.expiresAt,
      rememberMe: persisted.rememberMe,
      isAuthenticated: true,
      isLoading: false,
    });
  },
}));
