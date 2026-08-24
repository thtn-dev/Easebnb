import { z } from "zod";

/**
 * Schemas mirrored from open-api-v1.json (`/api/v1/auth`). Types are
 * derived via z.infer — never duplicated by hand.
 */

// ---- Requests ----

export const loginRequestSchema = z.object({
  username: z.string().min(1, "Username is required."),
  password: z.string().min(1, "Password is required."),
  rememberMe: z.boolean().optional(),
});

export const registerRequestSchema = z
  .object({
    username: z.string().min(1, "Username is required."),
    email: z.email("Enter a valid email address."),
    password: z.string().min(1, "Password is required."),
    confirmPassword: z.string().min(1, "Please confirm your password."),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: "Passwords do not match.",
    path: ["confirmPassword"],
  });

// Every field is nullable per the OpenAPI spec; only refreshToken is used.
export const refreshTokenRequestSchema = z.object({
  refreshToken: z.string().nullable(),
  scope: z.string().nullable(),
  grantType: z.string().nullable(),
  clientId: z.string().nullable(),
  clientSecret: z.string().nullable(),
});

export type LoginRequest = z.infer<typeof loginRequestSchema>;
export type RegisterRequest = z.infer<typeof registerRequestSchema>;
export type RefreshTokenRequest = z.infer<typeof refreshTokenRequestSchema>;

// ---- Responses ----

export const userInfoSchema = z.object({
  id: z.string(),
  username: z.string(),
  email: z.string(),
  emailConfirmed: z.boolean(),
  phoneNumber: z.string().nullable(),
  twoFactorEnabled: z.boolean(),
});

// OpenAPI types expiresIn as integer | string — normalize to seconds.
export const tokenResponseSchema = z.object({
  accessToken: z.string(),
  refreshToken: z.string(),
  tokenType: z.string(),
  expiresIn: z.coerce.number(),
  user: userInfoSchema,
});

export const apiResponseSchema = z.object({
  success: z.boolean(),
  message: z.string().nullable(),
});

export const authSessionResponseSchema = z.object({
  data: tokenResponseSchema.nullable(),
  success: z.boolean(),
  message: z.string().nullable(),
});

export type UserInfo = z.infer<typeof userInfoSchema>;
export type TokenResponse = z.infer<typeof tokenResponseSchema>;

/** What the auth store keeps from a successful login/register/refresh. */
export interface AuthSession {
  accessToken: string;
  refreshToken: string;
  tokenType: string;
  /** Absolute expiry timestamp in ms since epoch. */
  expiresAt: number;
  user: UserInfo;
}

// Persisted sessions come back from storage (untrusted), so re-validate.
export const persistedSessionSchema = z.object({
  accessToken: z.string(),
  refreshToken: z.string(),
  tokenType: z.string(),
  expiresAt: z.number(),
  user: userInfoSchema,
  rememberMe: z.boolean(),
});

export type PersistedSession = z.infer<typeof persistedSessionSchema>;
