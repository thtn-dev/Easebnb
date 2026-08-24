"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { CircleAlertIcon, CircleCheckIcon } from "lucide-react";

import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { logout } from "@/features/auth/session";
import { getErrorMessage } from "@/lib/api/errors";
import { useAuthStore } from "@/stores/auth-store";

import { refreshCurrentUser, resendEmailConfirmation } from "../session";

export function AccountOverview() {
  const router = useRouter();
  const user = useAuthStore((state) => state.user);
  const [isRefreshing, setIsRefreshing] = useState(true);
  const [refreshError, setRefreshError] = useState<string | null>(null);
  const [isResending, setIsResending] = useState(false);
  const [resendMessage, setResendMessage] = useState<string | null>(null);
  const [resendError, setResendError] = useState<string | null>(null);
  const [isLoggingOut, setIsLoggingOut] = useState(false);

  // Pull the latest profile once per visit so auth.user stays current.
  useEffect(() => {
    let cancelled = false;
    refreshCurrentUser()
      .catch((error: unknown) => {
        if (!cancelled) {
          setRefreshError(
            getErrorMessage(error, "Could not load your profile."),
          );
        }
      })
      .finally(() => {
        if (!cancelled) {
          setIsRefreshing(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  async function handleResend() {
    if (!user) {
      return;
    }
    setResendError(null);
    setResendMessage(null);
    setIsResending(true);
    try {
      const message = await resendEmailConfirmation({ email: user.email });
      setResendMessage(message ?? "Confirmation email sent.");
    } catch (error) {
      setResendError(
        getErrorMessage(error, "Could not resend the confirmation email."),
      );
    } finally {
      setIsResending(false);
    }
  }

  async function handleLogout() {
    setIsLoggingOut(true);
    try {
      await logout();
    } finally {
      router.replace("/login");
    }
  }

  if (!user) {
    return null;
  }

  return (
    <Card className="w-full max-w-md">
      <CardHeader>
        <CardTitle className="text-lg">Account</CardTitle>
        <CardDescription>Your Easebnb profile.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {isRefreshing ? (
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Spinner /> Refreshing profile…
          </div>
        ) : null}

        {refreshError ? (
          <Alert variant="destructive">
            <CircleAlertIcon />
            <AlertDescription>{refreshError}</AlertDescription>
          </Alert>
        ) : null}

        <dl className="space-y-2 text-sm">
          <div className="flex items-center justify-between gap-4">
            <dt className="text-muted-foreground">Username</dt>
            <dd className="font-medium">{user.username}</dd>
          </div>
          <div className="flex items-center justify-between gap-4">
            <dt className="text-muted-foreground">Email</dt>
            <dd className="font-medium">{user.email}</dd>
          </div>
          <div className="flex items-center justify-between gap-4">
            <dt className="text-muted-foreground">Email confirmed</dt>
            <dd>
              <Badge variant={user.emailConfirmed ? "default" : "secondary"}>
                {user.emailConfirmed ? "Yes" : "No"}
              </Badge>
            </dd>
          </div>
          <div className="flex items-center justify-between gap-4">
            <dt className="text-muted-foreground">Phone number</dt>
            <dd className="font-medium">{user.phoneNumber ?? "—"}</dd>
          </div>
          <div className="flex items-center justify-between gap-4">
            <dt className="text-muted-foreground">Two-factor</dt>
            <dd>
              <Badge variant={user.twoFactorEnabled ? "default" : "secondary"}>
                {user.twoFactorEnabled ? "Enabled" : "Disabled"}
              </Badge>
            </dd>
          </div>
        </dl>

        {!user.emailConfirmed ? (
          <Alert>
            <CircleAlertIcon />
            <AlertDescription>
              Your email is not confirmed yet.
              <Button
                variant="link"
                size="xs"
                onClick={handleResend}
                disabled={isResending}
              >
                {isResending ? "Sending…" : "Resend confirmation email"}
              </Button>
            </AlertDescription>
          </Alert>
        ) : null}

        {resendMessage ? (
          <Alert>
            <CircleCheckIcon />
            <AlertDescription>{resendMessage}</AlertDescription>
          </Alert>
        ) : null}

        {resendError ? (
          <Alert variant="destructive">
            <CircleAlertIcon />
            <AlertDescription>{resendError}</AlertDescription>
          </Alert>
        ) : null}

        <div className="grid grid-cols-2 gap-2">
          <Button
            variant="outline"
            nativeButton={false}
            render={<Link href="/account/profile" />}
          >
            Edit profile
          </Button>
          <Button
            variant="outline"
            nativeButton={false}
            render={<Link href="/account/security" />}
          >
            Change password
          </Button>
        </div>

        <Button
          variant="destructive"
          className="w-full"
          onClick={handleLogout}
          disabled={isLoggingOut}
        >
          {isLoggingOut ? (
            <>
              <Spinner className="size-3.5" /> Signing out…
            </>
          ) : (
            "Sign out"
          )}
        </Button>
      </CardContent>
    </Card>
  );
}
