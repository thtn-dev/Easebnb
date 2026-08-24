"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { CircleAlertIcon, CircleCheckIcon } from "lucide-react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { getErrorMessage } from "@/lib/api/errors";

import { confirmEmail } from "../session";

type ConfirmState =
  | { status: "missing-params" }
  | { status: "confirming" }
  | { status: "success"; message: string | null }
  | { status: "error"; message: string };

/**
 * Confirms an email address from the link parameters
 * (/account/confirm-email?userId=...&token=...) as soon as it mounts.
 */
export function ConfirmEmailPanel({
  userId,
  token,
}: {
  userId?: string;
  token?: string;
}) {
  const hasParams = Boolean(userId && token);
  const [state, setState] = useState<ConfirmState>(
    hasParams ? { status: "confirming" } : { status: "missing-params" },
  );
  // Guard against double-submission under React Strict Mode remounts.
  const startedRef = useRef(false);

  useEffect(() => {
    if (!hasParams || startedRef.current) {
      return;
    }
    startedRef.current = true;
    confirmEmail({ userId: userId!, token: token! })
      .then((message) => setState({ status: "success", message }))
      .catch((error: unknown) =>
        setState({
          status: "error",
          message: getErrorMessage(error, "Could not confirm your email."),
        }),
      );
  }, [hasParams, userId, token]);

  return (
    <Card className="w-full max-w-sm">
      <CardHeader>
        <CardTitle className="text-lg">Confirm email</CardTitle>
        <CardDescription>
          Confirming your email address using the link you received.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {state.status === "missing-params" ? (
          <Alert variant="destructive">
            <CircleAlertIcon />
            <AlertTitle>Invalid confirmation link</AlertTitle>
            <AlertDescription>
              The link is missing the user id or the confirmation token.
              Please use the link from your confirmation email.
            </AlertDescription>
          </Alert>
        ) : null}

        {state.status === "confirming" ? (
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Spinner /> Confirming your email…
          </div>
        ) : null}

        {state.status === "success" ? (
          <Alert>
            <CircleCheckIcon />
            <AlertTitle>Email confirmed</AlertTitle>
            <AlertDescription>
              {state.message ?? "Your email address has been confirmed."}
            </AlertDescription>
          </Alert>
        ) : null}

        {state.status === "error" ? (
          <Alert variant="destructive">
            <CircleAlertIcon />
            <AlertTitle>Confirmation failed</AlertTitle>
            <AlertDescription>{state.message}</AlertDescription>
          </Alert>
        ) : null}

        <Button variant="outline" nativeButton={false} render={<Link href="/login" />} className="w-full">
          Go to sign in
        </Button>
      </CardContent>
    </Card>
  );
}
