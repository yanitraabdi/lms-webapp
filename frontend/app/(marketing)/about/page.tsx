import { RouteStub } from "@/components/RouteStub";

// /about — SSG (static marketing page).
export const dynamic = "force-static";

export default function AboutPage() {
  return <RouteStub name="About" mode="SSG (force-static)" />;
}
