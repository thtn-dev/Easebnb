import type { Metadata } from "next";

import { ChangePasswordForm } from "@/features/account/components/change-password-form";
import { AuthGuard } from "@/features/auth/components/auth-guard";

export const metadata: Metadata = {
  title: "Change password",
};

export default function AccountSecurityPage() {
  return (
    <AuthGuard>
      <main className="flex flex-1 items-center justify-center p-8">
        <ChangePasswordForm />
      </main>
    </AuthGuard>
  );
}
