import { RouteStub } from "@/components/RouteStub";

// /for-business — SSG (static marketing page).
export const dynamic = "force-static";

export default function ForBusinessPage() {
  return <RouteStub name="For Business" mode="SSG (force-static)" />;
}
