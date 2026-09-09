"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

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
import { useAuthStore } from "@/stores/auth-store";

import { logout } from "../session";

/**
 * Minimal authenticated home: shows the signed-in user and proves the
 * Bearer flow — the logout button calls the authenticated endpoint.
 */
export function DashboardContent() {
  const router = useRouter();
  const user = useAuthStore((state) => state.user);
  const [isLoggingOut, setIsLoggingOut] = useState(false);

  async function handleLogout() {
    setIsLoggingOut(true);
    try {
      await logout();
    } finally {
      // logout() always clears the session, even when the API call fails.
      router.replace("/login");
    }
  }

  if (!user) {
    return null;
  }

  return (
    <Card className="w-full max-w-md">
      <CardHeader>
        <CardTitle className="text-lg">Signed in</CardTitle>
        <CardDescription>
          You are authenticated. This page is protected.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
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
