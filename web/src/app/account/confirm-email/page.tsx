import type { Metadata } from "next";

import { ConfirmEmailPanel } from "@/features/account/components/confirm-email-panel";

export const metadata: Metadata = {
  title: "Confirm email",
};

/**
 * Public page: reads userId/token from the confirmation link
 * (/account/confirm-email?userId=...&token=...). Intentionally not wrapped
 * in AuthGuard — the link must work while signed out.
 */
export default async function ConfirmEmailPage({
  searchParams,
}: {
  searchParams: Promise<{ userId?: string; token?: string }>;
}) {
  const { userId, token } = await searchParams;

  return (
    <main className="flex flex-1 items-center justify-center p-8">
      <ConfirmEmailPanel userId={userId} token={token} />
    </main>
  );
}
