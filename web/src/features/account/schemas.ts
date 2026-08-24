import { z } from "zod";

import { userInfoSchema } from "@/features/auth/schemas";

/**
 * Schemas mirrored from open-api-v1.json (`/api/v1/account`).
 * `UserInfo` and the shared `ApiResponse` envelope are reused from the
 * auth feature — never duplicated.
 */

// ---- Requests ----

// Both properties are always present but nullable per the OpenAPI schema.
export const updateProfileRequestSchema = z.object({
  email: z.email("Enter a valid email address.").nullable(),
  phoneNumber: z.string().nullable(),
});

export const changePasswordRequestSchema = z
  .object({
    currentPassword: z.string().min(1, "Current password is required."),
    newPassword: z.string().min(1, "New password is required."),
    confirmNewPassword: z
      .string()
      .min(1, "Please confirm the new password."),
  })
  .refine((data) => data.newPassword === data.confirmNewPassword, {
    message: "Passwords do not match.",
    path: ["confirmNewPassword"],
  });

export const confirmEmailRequestSchema = z.object({
  userId: z.string().min(1, "User id is required."),
  token: z.string().min(1, "Confirmation token is required."),
});

export const resendEmailConfirmationRequestSchema = z.object({
  email: z.email("Enter a valid email address."),
});

export const forgotPasswordRequestSchema = z.object({
  email: z.email("Enter a valid email address."),
});

export const resetPasswordRequestSchema = z
  .object({
    email: z.email("Enter a valid email address."),
    token: z.string().min(1, "Reset token is required."),
    newPassword: z.string().min(1, "New password is required."),
    confirmNewPassword: z
      .string()
      .min(1, "Please confirm the new password."),
  })
  .refine((data) => data.newPassword === data.confirmNewPassword, {
    message: "Passwords do not match.",
    path: ["confirmNewPassword"],
  });

export type UpdateProfileRequest = z.infer<typeof updateProfileRequestSchema>;
export type ChangePasswordRequest = z.infer<typeof changePasswordRequestSchema>;
export type ConfirmEmailRequest = z.infer<typeof confirmEmailRequestSchema>;
export type ResendEmailConfirmationRequest = z.infer<
  typeof resendEmailConfirmationRequestSchema
>;
export type ForgotPasswordRequest = z.infer<typeof forgotPasswordRequestSchema>;
export type ResetPasswordRequest = z.infer<typeof resetPasswordRequestSchema>;

// ---- Responses ----

export const userInfoResponseSchema = z.object({
  data: userInfoSchema.nullable(),
  success: z.boolean(),
  message: z.string().nullable(),
});
