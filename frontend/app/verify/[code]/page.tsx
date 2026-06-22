import { RouteStub } from "@/components/RouteStub";

// /verify/[code] — SSR (server-rendered per request).
export const dynamic = "force-dynamic";

export default async function VerifyPage({
  params,
}: {
  params: Promise<{ code: string }>;
}) {
  const { code } = await params;
  return <RouteStub name={`Verify: ${code}`} mode="SSR (force-dynamic)" />;
}
