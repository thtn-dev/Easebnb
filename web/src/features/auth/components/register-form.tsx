"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { CircleAlertIcon, EyeIcon, EyeOffIcon } from "lucide-react";

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
import { getErrorMessage } from "@/lib/api/errors";

import { collectFieldErrors } from "../form-errors";
import { register } from "../session";
import { registerRequestSchema } from "../schemas";

export function RegisterForm() {
  const router = useRouter();
  const [username, setUsername] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<
    Record<string, Array<{ message?: string }>>
  >({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(null);

    const parsed = registerRequestSchema.safeParse({
      username,
      email,
      password,
      confirmPassword,
    });
    if (!parsed.success) {
      setFieldErrors(collectFieldErrors(parsed.error));
      return;
    }
    setFieldErrors({});

    setIsSubmitting(true);
    try {
      // The current backend registers without returning tokens: land on
      // /login so the user signs in with the new account.
      const signedIn = await register(parsed.data);
      router.replace(signedIn ? "/dashboard" : "/login");
    } catch (error) {
      setFormError(getErrorMessage(error, "Registration failed. Please try again."));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <Card className="w-full max-w-sm">
      <CardHeader>
        <CardTitle className="text-lg">Create account</CardTitle>
        <CardDescription>Register a new Easebnb account.</CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit} noValidate className="space-y-4">
          <FieldGroup>
            <Field data-invalid={fieldErrors.username?.length ? true : undefined}>
              <FieldLabel htmlFor="username">Username</FieldLabel>
              <Input
                id="username"
                name="username"
                value={username}
                onChange={(event) => setUsername(event.target.value)}
                autoComplete="username"
                disabled={isSubmitting}
                aria-invalid={fieldErrors.username?.length ? true : undefined}
              />
              <FieldError errors={fieldErrors.username} />
            </Field>

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

            <Field data-invalid={fieldErrors.password?.length ? true : undefined}>
              <FieldLabel htmlFor="password">Password</FieldLabel>
              <div className="relative">
                <Input
                  id="password"
                  name="password"
                  type={showPassword ? "text" : "password"}
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  autoComplete="new-password"
                  disabled={isSubmitting}
                  aria-invalid={fieldErrors.password?.length ? true : undefined}
                  className="pr-8"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword((visible) => !visible)}
                  disabled={isSubmitting}
                  aria-label={showPassword ? "Hide password" : "Show password"}
                  className="absolute top-1/2 right-2 -translate-y-1/2 text-muted-foreground transition-colors hover:text-foreground"
                >
                  {showPassword ? (
                    <EyeOffIcon className="size-3.5" />
                  ) : (
                    <EyeIcon className="size-3.5" />
                  )}
                </button>
              </div>
              <FieldError errors={fieldErrors.password} />
            </Field>

            <Field
              data-invalid={fieldErrors.confirmPassword?.length ? true : undefined}
            >
              <FieldLabel htmlFor="confirmPassword">Confirm password</FieldLabel>
              <Input
                id="confirmPassword"
                name="confirmPassword"
                type={showPassword ? "text" : "password"}
                value={confirmPassword}
                onChange={(event) => setConfirmPassword(event.target.value)}
                autoComplete="new-password"
                disabled={isSubmitting}
                aria-invalid={fieldErrors.confirmPassword?.length ? true : undefined}
              />
              <FieldError errors={fieldErrors.confirmPassword} />
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
                <Spinner className="size-3.5" /> Creating account…
              </>
            ) : (
              "Create account"
            )}
          </Button>

          <p className="text-center text-xs text-muted-foreground">
            Already have an account?{" "}
            <Link
              href="/login"
              className="text-primary underline-offset-4 hover:underline"
            >
              Sign in
            </Link>
          </p>
        </form>
      </CardContent>
    </Card>
  );
}
