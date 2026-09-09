import type { Metadata } from "next";

import { ProfileForm } from "@/features/account/components/profile-form";
import { AuthGuard } from "@/features/auth/components/auth-guard";

export const metadata: Metadata = {
  title: "Edit profile",
};

export default function AccountProfilePage() {
  return (
    <AuthGuard>
      <main className="flex flex-1 items-center justify-center p-8">
        <ProfileForm />
      </main>
    </AuthGuard>
  );
}
