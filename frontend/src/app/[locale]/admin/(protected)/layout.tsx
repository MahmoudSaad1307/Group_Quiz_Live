import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import type { AdminProfile } from "@/lib/api/client";
import { AdminHeader } from "@/components/AdminHeader";

async function getAdminProfile(): Promise<AdminProfile | null> {
  const cookieStore = await cookies();
  const authCookie = cookieStore.get("ql_auth");
  if (!authCookie) return null;

  const apiInternalUrl = process.env.API_INTERNAL_URL ?? "http://localhost:5000";
  try {
    const res = await fetch(`${apiInternalUrl}/api/auth/me`, {
      headers: {
        Cookie: `ql_auth=${authCookie.value}`,
      },
      cache: "no-store",
    });
    if (!res.ok) return null;
    return (await res.json()) as AdminProfile;
  } catch {
    return null;
  }
}

export default async function AdminLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const profile = await getAdminProfile();

  if (!profile) {
    redirect("/admin/login");
  }

  return (
    <div className="min-h-screen flex flex-col">
      <AdminHeader profile={profile} />
      <div className="mx-auto w-full max-w-6xl flex-1 p-6">{children}</div>
    </div>
  );
}
