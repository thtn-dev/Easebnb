import type { Metadata } from "next";

import { GuestGuard } from "@/features/auth/components/auth-guard";
import { LoginForm } from "@/features/auth/components/login-form";

export const metadata: Metadata = {
  title: "Sign in",
};

export default function LoginPage() {
  return (
    <GuestGuard>
      <main className="flex flex-1 items-center justify-center p-8">
        <LoginForm />
      </main>
    </GuestGuard>
  );
}
