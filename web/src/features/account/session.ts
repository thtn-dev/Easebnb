import { useAuthStore } from "@/stores/auth-store";

import * as accountApi from "./api";
import type {
  ChangePasswordRequest,
  ConfirmEmailRequest,
  ForgotPasswordRequest,
  ResetPasswordRequest,
  UpdateProfileRequest,
} from "./schemas";

/**
 * Account actions that keep the centralized auth state in sync:
 * `auth.user` always reflects the current user (task §18).
 */

/** GET /account/me → updates auth.user. */
export async function refreshCurrentUser(): Promise<void> {
  const user = await accountApi.getCurrentUser();
  useAuthStore.getState().setUser(user);
}

/** PUT /account/me → updates auth.user from the response. */
export async function updateProfile(
  request: UpdateProfileRequest,
): Promise<void> {
  const user = await accountApi.updateProfile(request);
  useAuthStore.getState().setUser(user);
}

/**
 * Confirms the email, then refreshes the user so emailConfirmed reflects
 * the new value. The refresh only runs while signed in (the page itself
 * is public) and never masks the confirmation result.
 */
export async function confirmEmail(
  request: ConfirmEmailRequest,
): Promise<string | null> {
  const message = await accountApi.confirmEmail(request);
  if (useAuthStore.getState().isAuthenticated) {
    try {
      await refreshCurrentUser();
    } catch {
      // Keep the confirmation result; the profile refresh is best-effort.
    }
  }
  return message;
}

export function changePassword(
  request: ChangePasswordRequest,
): Promise<string | null> {
  return accountApi.changePassword(request);
}

export function resendEmailConfirmation(
  request: ForgotPasswordRequest,
): Promise<string | null> {
  return accountApi.resendEmailConfirmation(request);
}

export function forgotPassword(
  request: ForgotPasswordRequest,
): Promise<string | null> {
  return accountApi.forgotPassword(request);
}

export function resetPassword(
  request: ResetPasswordRequest,
): Promise<string | null> {
  return accountApi.resetPassword(request);
}
