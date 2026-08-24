"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { CircleAlertIcon, CircleCheckIcon } from "lucide-react";

import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Field, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { collectFieldErrors } from "@/features/auth/form-errors";
import { getErrorMessage } from "@/lib/api/errors";

import { resetPassword } from "../session";
import { resetPasswordRequestSchema } from "../schemas";

/**
 * Email and token are prefilled from the reset link
 * (/reset-password?email=...&token=...) when present; both remain editable.
 */
export function ResetPasswordForm({
  initialEmail = "",
  initialToken = "",
}: {
  initialEmail?: string;
  initialToken?: string;
}) {
  const [email, setEmail] = useState(initialEmail);
  const [token, setToken] = useState(initialToken);
  const [newPassword, setNewPassword] = useState("");
  const [confirmNewPassword, setConfirmNewPassword] = useState("");
  const [fieldErrors, setFieldErrors] = useState<
    Record<string, Array<{ message?: string }>>
  >({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSuccess, setIsSuccess] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(null);

    const parsed = resetPasswordRequestSchema.safeParse({
      email: email.trim(),
      token: token.trim(),
      newPassword,
      confirmNewPassword,
    });
    if (!parsed.success) {
      setFieldErrors(collectFieldErrors(parsed.error));
      return;
    }
    setFieldErrors({});

    setIsSubmitting(true);
    try {
      await resetPassword(parsed.data);
      setIsSuccess(true);
    } catch (error) {
      setFormError(getErrorMessage(error, "Could not reset your password."));
    } finally {
      setIsSubmitting(false);
    }
  }

  if (isSuccess) {
    return (
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle className="text-lg">Password reset</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <Alert>
            <CircleCheckIcon />
            <AlertDescription>
              Your password has been reset successfully.
            </AlertDescription>
          </Alert>
          <Button
            variant="outline"
            nativeButton={false}
            render={<Link href="/login" />}
            className="w-full"
          >
            Go to sign in
          </Button>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="w-full max-w-sm">
      <CardHeader>
        <CardTitle className="text-lg">Reset password</CardTitle>
        <CardDescription>
          Set a new password using the token from your reset email.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit} noValidate className="space-y-4">
          <FieldGroup>
            <Field data-invalid={fieldErrors.email?.length ? true : undefined}>
              <FieldLabel htmlFor="email">Email</FieldLabel>
              <Input
                id="email"
                name="email"
                type="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                autoComplete="email"
                disabled={isSubmitting}
                aria-invalid={fieldErrors.email?.length ? true : undefined}
              />
              <FieldError errors={fieldErrors.email} />
            </Field>

            <Field data-invalid={fieldErrors.token?.length ? true : undefined}>
              <FieldLabel htmlFor="token">Reset token</FieldLabel>
              <Input
                id="token"
                name="token"
                value={token}
                onChange={(event) => setToken(event.target.value)}
                disabled={isSubmitting}
                aria-invalid={fieldErrors.token?.length ? true : undefined}
              />
              <FieldError errors={fieldErrors.token} />
            </Field>

            <Field data-invalid={fieldErrors.newPassword?.length ? true : undefined}>
              <FieldLabel htmlFor="newPassword">New password</FieldLabel>
              <Input
                id="newPassword"
                name="newPassword"
                type="password"
                value={newPassword}
                onChange={(event) => setNewPassword(event.target.value)}
                autoComplete="new-password"
                disabled={isSubmitting}
                aria-invalid={fieldErrors.newPassword?.length ? true : undefined}
              />
              <FieldError errors={fieldErrors.newPassword} />
            </Field>

            <Field
              data-invalid={
                fieldErrors.confirmNewPassword?.length ? true : undefined
              }
            >
              <FieldLabel htmlFor="confirmNewPassword">
                Confirm new password
              </FieldLabel>
              <Input
                id="confirmNewPassword"
                name="confirmNewPassword"
                type="password"
                value={confirmNewPassword}
                onChange={(event) => setConfirmNewPassword(event.target.value)}
                autoComplete="new-password"
                disabled={isSubmitting}
                aria-invalid={
                  fieldErrors.confirmNewPassword?.length ? true : undefined
                }
              />
              <FieldError errors={fieldErrors.confirmNewPassword} />
            </Field>
          </FieldGroup>

          {formError ? (
            <Alert variant="destructive">
              <CircleAlertIcon />
              <AlertDescription>{formError}</AlertDescription>
            </Alert>
          ) : null}

          <Button type="submit" className="w-full" disabled={isSubmitting}>
            {isSubmitting ? (
              <>
                <Spinner className="size-3.5" /> Resetting password…
              </>
            ) : (
              "Reset password"
            )}
          </Button>

          <p className="text-center text-xs text-muted-foreground">
            <Link
              href="/login"
              className="text-primary underline-offset-4 hover:underline"
            >
              Back to sign in
            </Link>
          </p>
        </form>
      </CardContent>
    </Card>
  );
}
