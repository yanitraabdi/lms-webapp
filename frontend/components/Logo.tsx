import Image from "next/image";
import logoColour from "@/public/logo.png";
import logoWhite from "@/public/logo-white.png";
import { cn } from "@/lib/cn";

/**
 * The INVERTA wordmark. One component for every header, because three hand-rolled marks had
 * already drifted — two rendered "A" (from the archived Academy product) and one rendered "I",
 * so a learner and an admin saw different brands.
 *
 * `variant="white"` is for dark backgrounds: the admin sidebar is `bg-ink`, where the colour
 * wordmark is nearly invisible.
 */
export function Logo({
  variant = "colour",
  className,
}: {
  variant?: "colour" | "white";
  className?: string;
}) {
  return (
    <Image
      // Statically imported, so width and height come from the file itself. Hardcoding them
      // would silently drift the moment the artwork is regenerated at a different aspect.
      src={variant === "white" ? logoWhite : logoColour}
      // The wordmark IS the site name here, so that is what a screen reader should announce.
      // "INVERTA logo" would add a word that tells a listener nothing.
      alt="INVERTA"
      // Above the fold on every page — a late-loading logo reads as a broken site.
      priority
      className={cn("h-7 w-auto", className)}
    />
  );
}
