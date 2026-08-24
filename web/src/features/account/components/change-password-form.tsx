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

import { changePassword } from "../session";
import { changePasswordRequestSchema } from "../schemas";

export function ChangePasswordForm() {
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmNewPassword, setConfirmNewPassword] = useState("");
  const [fieldErrors, setFieldErrors] = useState<
    Record<string, Array<{ message?: string }>>
  >({});
  const [formError, setFormError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(null);
    setSuccessMessage(null);

    const parsed = changePasswordRequestSchema.safeParse({
      currentPassword,
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
      // Backend keeps the session valid: show the message, reset the form,
      // and stay signed in (no logout).
      const message = await changePassword(parsed.data);
      setSuccessMessage(message ?? "Password changed successfully.");
      setCurrentPassword("");
      setNewPassword("");
      setConfirmNewPassword("");
    } catch (error) {
      setFormError(
        getErrorMessage(error, "Could not change your password."),
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <Card className="w-full max-w-sm">
      <CardHeader>
        <CardTitle className="text-lg">Change password</CardTitle>
        <CardDescription>Update your account password.</CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit} noValidate className="space-y-4">
          <FieldGroup>
            <Field
              data-invalid={fieldErrors.currentPassword?.length ? true : undefined}
            >
              <FieldLabel htmlFor="currentPassword">Current password</FieldLabel>
              <Input
                id="currentPassword"
                name="currentPassword"
                type="password"
                value={currentPassword}
                onChange={(event) => setCurrentPassword(event.target.value)}
                autoComplete="current-password"
                disabled={isSubmitting}
                aria-invalid={
                  fieldErrors.currentPassword?.length ? true : undefined
                }
              />
              <FieldError errors={fieldErrors.currentPassword} />
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

          {successMessage ? (
            <Alert>
              <CircleCheckIcon />
              <AlertDescription>{successMessage}</AlertDescription>
            </Alert>
          ) : null}

          <Button type="submit" className="w-full" disabled={isSubmitting}>
            {isSubmitting ? (
              <>
                <Spinner className="size-3.5" /> Changing password…
              </>
            ) : (
              "Change password"
            )}
          </Button>

          <p className="text-center text-xs text-muted-foreground">
            <Link
              href="/account"
              className="text-primary underline-offset-4 hover:underline"
            >
              Back to account
            </Link>
          </p>
        </form>
      </CardContent>
    </Card>
  );
}
