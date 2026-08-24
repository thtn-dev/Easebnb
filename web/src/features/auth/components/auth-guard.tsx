"use client";

import { useEffect, type ReactNode } from "react";
import { useRouter } from "next/navigation";

import { Spinner } from "@/components/ui/spinner";
import { useAuthStore } from "@/stores/auth-store";

/** Blocks authenticated pages: unauthenticated visitors go to /login. */
export function AuthGuard({ children }: { children: ReactNode }) {
  const router = useRouter();
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const isLoading = useAuthStore((state) => state.isLoading);

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.replace("/login");
    }
  }, [isLoading, isAuthenticated, router]);

  if (isLoading || !isAuthenticated) {
    return (
      <div
        className="flex flex-1 items-center justify-center p-8"
        role="status"
        aria-label="Checking session"
      >
        <Spinner className="size-6" />
      </div>
    );
  }

  return children;
}

/** Keeps /login and /register out of authenticated users' way. */
export function GuestGuard({ children }: { children: ReactNode }) {
  const router = useRouter();
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const isLoading = useAuthStore((state) => state.isLoading);

  useEffect(() => {
    if (!isLoading && isAuthenticated) {
      router.replace("/dashboard");
    }
  }, [isLoading, isAuthenticated, router]);

  if (isLoading || isAuthenticated) {
    return (
      <div
        className="flex flex-1 items-center justify-center p-8"
        role="status"
        aria-label="Loading"
      >
        <Spinner className="size-6" />
      </div>
    );
  }

  return children;
}
