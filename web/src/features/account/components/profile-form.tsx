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
import { useAuthStore } from "@/stores/auth-store";

import { updateProfile } from "../session";
import { updateProfileRequestSchema } from "../schemas";

export function ProfileForm() {
  const user = useAuthStore((state) => state.user);
  // AuthGuard guarantees `user` is available before this form mounts, so the
  // store values are captured once as initial state; later updates come from
  // this form's own submit (updateProfile syncs the store on success).
  const [email, setEmail] = useState(user?.email ?? "");
  const [phoneNumber, setPhoneNumber] = useState(user?.phoneNumber ?? "");
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

    const parsed = updateProfileRequestSchema.safeParse({
      email: email.trim() === "" ? null : email.trim(),
      phoneNumber: phoneNumber.trim() === "" ? null : phoneNumber.trim(),
    });
    if (!parsed.success) {
      setFieldErrors(collectFieldErrors(parsed.error));
      return;
    }
    setFieldErrors({});

    setIsSubmitting(true);
    try {
      // Updates auth.user on success — no re-login needed.
      await updateProfile(parsed.data);
      setSuccessMessage("Profile updated.");
    } catch (error) {
      setFormError(getErrorMessage(error, "Could not update your profile."));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <Card className="w-full max-w-sm">
      <CardHeader>
        <CardTitle className="text-lg">Edit profile</CardTitle>
        <CardDescription>Update your email and phone number.</CardDescription>
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

            <Field
              data-invalid={fieldErrors.phoneNumber?.length ? true : undefined}
            >
              <FieldLabel htmlFor="phoneNumber">Phone number</FieldLabel>
              <Input
                id="phoneNumber"
                name="phoneNumber"
                value={phoneNumber}
                onChange={(event) => setPhoneNumber(event.target.value)}
                autoComplete="tel"
                disabled={isSubmitting}
                aria-invalid={fieldErrors.phoneNumber?.length ? true : undefined}
              />
              <FieldError errors={fieldErrors.phoneNumber} />
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
                <Spinner className="size-3.5" /> Saving…
              </>
            ) : (
              "Save changes"
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
