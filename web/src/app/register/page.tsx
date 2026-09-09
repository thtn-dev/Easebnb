import type { Metadata } from "next";

import { GuestGuard } from "@/features/auth/components/auth-guard";
import { RegisterForm } from "@/features/auth/components/register-form";

export const metadata: Metadata = {
  title: "Create account",
};

export default function RegisterPage() {
  return (
    <GuestGuard>
      <main className="flex flex-1 items-center justify-center p-8">
        <RegisterForm />
      </main>
    </GuestGuard>
  );
}
