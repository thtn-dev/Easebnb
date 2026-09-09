import type { Metadata } from "next";

import { AccountOverview } from "@/features/account/components/account-overview";
import { AuthGuard } from "@/features/auth/components/auth-guard";

export const metadata: Metadata = {
  title: "Account",
};

export default function AccountPage() {
  return (
    <AuthGuard>
      <main className="flex flex-1 items-center justify-center p-8">
        <AccountOverview />
      </main>
    </AuthGuard>
  );
}
