import { redirect } from "next/navigation";

/**
 * INVERTA is a single-program product, so the root is the program landing (KAK §9.15).
 * The previous tiered-subscription marketing home belonged to the archived AI Academy
 * product and is superseded; a bespoke root page can replace this redirect in M6.
 */
export const dynamic = "force-static";

export default function HomePage() {
  redirect("/program/toefl-preparation");
}
