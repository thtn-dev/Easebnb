import type { Metadata } from "next";

import { ResetPasswordForm } from "@/features/account/components/reset-password-form";

export const metadata: Metadata = {
  title: "Reset password",
};

/**
 * Public page. Prefills email/token from the reset link
 * (/reset-password?email=...&token=...) when present.
 */
export default async function ResetPasswordPage({
  searchParams,
}: {
  searchParams: Promise<{ email?: string; token?: string }>;
}) {
  const { email, token } = await searchParams;

  return (
    <main className="flex flex-1 items-center justify-center p-8">
      <ResetPasswordForm initialEmail={email} initialToken={token} />
    </main>
  );
}
