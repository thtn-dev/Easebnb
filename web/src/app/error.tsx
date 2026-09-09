"use client";

import { CircleAlertIcon } from "lucide-react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";

// Next.js requires error boundaries to be Client Components.
export default function Error({
  error,
  retry,
}: {
  error: Error & { digest?: string };
  retry: () => void;
}) {
  return (
    <main className="flex flex-1 flex-col items-center justify-center gap-4 p-8">
      <Alert variant="destructive" className="max-w-md">
        <CircleAlertIcon />
        <AlertTitle>Something went wrong</AlertTitle>
        <AlertDescription>
          An unexpected error occurred while loading this page.
          {process.env.NODE_ENV === "development" && error.digest ? (
            <span className="block font-mono text-xs opacity-70">digest: {error.digest}</span>
          ) : null}
        </AlertDescription>
      </Alert>
      <Button variant="outline" onClick={retry}>
        Try again
      </Button>
    </main>
  );
}
