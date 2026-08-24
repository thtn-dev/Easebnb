import type { Metadata } from "next";

import { AuthGuard } from "@/features/auth/components/auth-guard";
import { DashboardContent } from "@/features/auth/components/dashboard-content";

export const metadata: Metadata = {
  title: "Dashboard",
};

export default function DashboardPage() {
  return (
    <AuthGuard>
      <main className="flex flex-1 items-center justify-center p-8">
        <DashboardContent />
      </main>
    </AuthGuard>
  );
}
