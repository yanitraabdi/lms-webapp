"use client";

import { useEffect } from "react";
import { useParams, useRouter } from "next/navigation";
import { useAuth } from "@/components/auth/AuthProvider";
import { Spinner } from "@/components/ui";
import { SessionView } from "@/components/learn/SessionView";

/**
 * The standalone session page. Kept as its own route because the dashboard, the assessment page's
 * back-link and learners' bookmarks all point at it — and because it is what a phone uses, where a
 * side-by-side layout has no room.
 */
export default function SessionPage() {
  const { status, accessToken } = useAuth();
  const router = useRouter();
  const id = useParams<{ id: string }>().id;

  useEffect(() => {
    if (status === "unauthenticated") router.replace(`/login?next=/app/session/${id}`);
  }, [status, id, router]);

  if (status !== "authenticated" || !accessToken) {
    return <div className="flex min-h-screen items-center justify-center bg-bg"><Spinner size={24} /></div>;
  }
  return <SessionView token={accessToken} sessionId={id} />;
}
