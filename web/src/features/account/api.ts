import { api } from "@/lib/api/client";
import { ApiError } from "@/lib/api/errors";
import { apiResponseSchema, type UserInfo } from "@/features/auth/schemas";

import {
  changePasswordRequestSchema,
  confirmEmailRequestSchema,
  forgotPasswordRequestSchema,
  resendEmailConfirmationRequestSchema,
  resetPasswordRequestSchema,
  updateProfileRequestSchema,
  userInfoResponseSchema,
  type ChangePasswordRequest,
  type ConfirmEmailRequest,
  type ForgotPasswordRequest,
  type ResendEmailConfirmationRequest,
  type ResetPasswordRequest,
  type UpdateProfileRequest,
} from "./schemas";

const ACCOUNT_BASE = "/v1/account";

function requireSuccess(success: boolean, message: string | null, fallback: string): void {
  if (!success) {
    throw new ApiError({ kind: "http", message: message ?? fallback });
  }
}

/**
 * Message-only endpoints currently answer 204 No Content on success
 * (differs from the OpenAPI-declared 200 + ApiResponse envelope). An empty
 * body is therefore treated as success without a message.
 */
function unwrapMessage(response: unknown, fallback: string): string | null {
  if (typeof response !== "object" || response === null) {
    return null;
  }
  const parsed = apiResponseSchema.parse(response);
  requireSuccess(parsed.success, parsed.message, fallback);
  return parsed.message;
}

/** Authenticated (GET /me): returns the fresh UserInfo. */
export async function getCurrentUser(): Promise<UserInfo> {
  const response = await api.get<unknown>(`${ACCOUNT_BASE}/me`);
  const parsed = userInfoResponseSchema.parse(response);
  if (!parsed.success || !parsed.data) {
    throw new ApiError({
      kind: "http",
      message: parsed.message ?? "Could not load your profile.",
    });
  }
  return parsed.data;
}

/** Authenticated (PUT /me): returns the updated UserInfo. */
export async function updateProfile(
  request: UpdateProfileRequest,
): Promise<UserInfo> {
  const response = await api.put<unknown>(
    `${ACCOUNT_BASE}/me`,
    updateProfileRequestSchema.parse(request),
  );
  const parsed = userInfoResponseSchema.parse(response);
  if (!parsed.success || !parsed.data) {
    throw new ApiError({
      kind: "http",
      message: parsed.message ?? "Could not update your profile.",
    });
  }
  return parsed.data;
}

/** Authenticated. Returns the API message for display. */
export async function changePassword(
  request: ChangePasswordRequest,
): Promise<string | null> {
  const response = await api.post<unknown>(
    `${ACCOUNT_BASE}/change-password`,
    changePasswordRequestSchema.parse(request),
  );
  return unwrapMessage(response, "Could not change your password.");
}

// The remaining endpoints are public: skipAuth keeps the Authorization
// header (and 401 auto-refresh) away from them entirely.

export async function confirmEmail(
  request: ConfirmEmailRequest,
): Promise<string | null> {
  const response = await api.post<unknown>(
    `${ACCOUNT_BASE}/confirm-email`,
    confirmEmailRequestSchema.parse(request),
    { skipAuth: true },
  );
  return unwrapMessage(response, "Could not confirm your email.");
}

export async function resendEmailConfirmation(
  request: ResendEmailConfirmationRequest,
): Promise<string | null> {
  const response = await api.post<unknown>(
    `${ACCOUNT_BASE}/resend-email-confirmation`,
    resendEmailConfirmationRequestSchema.parse(request),
    { skipAuth: true },
  );
  return unwrapMessage(
    response,
    "Could not resend the confirmation email.",
  );
}

export async function forgotPassword(
  request: ForgotPasswordRequest,
): Promise<string | null> {
  const response = await api.post<unknown>(
    `${ACCOUNT_BASE}/forgot-password`,
    forgotPasswordRequestSchema.parse(request),
    { skipAuth: true },
  );
  return unwrapMessage(
    response,
    "Could not send the password reset email.",
  );
}

export async function resetPassword(
  request: ResetPasswordRequest,
): Promise<string | null> {
  const response = await api.post<unknown>(
    `${ACCOUNT_BASE}/reset-password`,
    resetPasswordRequestSchema.parse(request),
    { skipAuth: true },
  );
  return unwrapMessage(response, "Could not reset your password.");
}
