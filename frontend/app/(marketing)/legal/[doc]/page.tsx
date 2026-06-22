import { RouteStub } from "@/components/RouteStub";

// /legal/[doc] — SSG with dynamic params (e.g. /legal/terms, /legal/privacy).
// M0: no generateStaticParams yet; params are resolved on demand.
export const dynamic = "force-static";

export default async function LegalDocPage({
  params,
}: {
  params: Promise<{ doc: string }>;
}) {
  const { doc } = await params;
  return <RouteStub name={`Legal: ${doc}`} mode="SSG (force-static, dynamic param)" />;
}
