import type { Metadata } from "next";

import { ForgotPasswordForm } from "@/features/account/components/forgot-password-form";

export const metadata: Metadata = {
  title: "Forgot password",
};

export default function ForgotPasswordPage() {
  return (
    <main className="flex flex-1 items-center justify-center p-8">
      <ForgotPasswordForm />
    </main>
  );
}
