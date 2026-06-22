import { RouteStub } from "@/components/RouteStub";

// /how-it-works — SSG (static marketing page).
export const dynamic = "force-static";

export default function HowItWorksPage() {
  return <RouteStub name="How It Works" mode="SSG (force-static)" />;
}
