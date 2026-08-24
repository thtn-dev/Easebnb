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

import { forgotPassword } from "../session";
import { forgotPasswordRequestSchema } from "../schemas";

export function ForgotPasswordForm() {
  const [email, setEmail] = useState("");
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

    const parsed = forgotPasswordRequestSchema.safeParse({ email: email.trim() });
    if (!parsed.success) {
      setFieldErrors(collectFieldErrors(parsed.error));
      return;
    }
    setFieldErrors({});

    setIsSubmitting(true);
    try {
      // The backend sends the reset link; no token is created client-side.
      const message = await forgotPassword(parsed.data);
      setSuccessMessage(
        message ??
          "If the email exists, a password reset link has been sent.",
      );
    } catch (error) {
      setFormError(
        getErrorMessage(error, "Could not send the password reset email."),
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <Card className="w-full max-w-sm">
      <CardHeader>
        <CardTitle className="text-lg">Forgot password</CardTitle>
        <CardDescription>
          Enter your email and we&apos;ll send you a reset link.
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
              <AlertDescription>
                {successMessage}{" "}
                <Link
                  href="/reset-password"
                  className="text-primary underline-offset-4 hover:underline"
                >
                  Continue to reset password
                </Link>
              </AlertDescription>
            </Alert>
          ) : null}

          <Button type="submit" className="w-full" disabled={isSubmitting}>
            {isSubmitting ? (
              <>
                <Spinner className="size-3.5" /> Sending…
              </>
            ) : (
              "Send reset link"
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
