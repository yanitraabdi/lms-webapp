// Inline SVG icon set (feather-style, currentColor) — matches the Design Foundation.
// Dependency-free so the kit doesn't pull an icon library.
import type { ReactNode, SVGProps } from "react";

export interface IconProps extends SVGProps<SVGSVGElement> {
  size?: number;
}

function Stroke({ size = 18, children, ...props }: IconProps & { children: ReactNode }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      {...props}
    >
      {children}
    </svg>
  );
}

function Fill({ size = 18, children, ...props }: IconProps & { children: ReactNode }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="currentColor" aria-hidden="true" {...props}>
      {children}
    </svg>
  );
}

export const CheckIcon = (p: IconProps) => <Stroke {...p}><path d="M20 6 9 17l-5-5" /></Stroke>;
export const CheckCircleIcon = (p: IconProps) => <Stroke {...p}><circle cx="12" cy="12" r="9" /><path d="m8 12 2.5 2.5L16 9" /></Stroke>;
export const LockIcon = (p: IconProps) => <Stroke {...p}><rect x="5" y="11" width="14" height="9" rx="2" /><path d="M8 11V8a4 4 0 0 1 8 0v3" /></Stroke>;
export const PlayIcon = (p: IconProps) => <Fill {...p}><path d="M8 5v14l11-7z" /></Fill>;
export const StarIcon = (p: IconProps) => <Fill {...p}><path d="m12 2 3 6.3 6.9 1-5 4.8 1.2 6.9L12 17.8 5.9 21l1.2-6.9-5-4.8 6.9-1z" /></Fill>;
export const SearchIcon = (p: IconProps) => <Stroke {...p}><circle cx="11" cy="11" r="7" /><path d="m20 20-3.5-3.5" /></Stroke>;
export const BellIcon = (p: IconProps) => <Stroke {...p}><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9" /><path d="M10 21a2 2 0 0 0 4 0" /></Stroke>;
export const AlertCircleIcon = (p: IconProps) => <Stroke {...p}><circle cx="12" cy="12" r="9" /><path d="M12 7v6M12 16.5v.5" /></Stroke>;
export const AlertTriangleIcon = (p: IconProps) => <Stroke {...p}><path d="M12 3 2 20h20L12 3z" /><path d="M12 10v4M12 17.5v.5" /></Stroke>;
export const InfoIcon = (p: IconProps) => <Stroke {...p}><circle cx="12" cy="12" r="9" /><path d="M12 11v5M12 8v.5" /></Stroke>;
export const XIcon = (p: IconProps) => <Stroke {...p}><path d="M18 6 6 18M6 6l12 12" /></Stroke>;
export const ChevronRightIcon = (p: IconProps) => <Stroke {...p}><path d="m9 18 6-6-6-6" /></Stroke>;
export const WrenchIcon = (p: IconProps) => <Stroke {...p}><path d="M14.7 6.3a4 4 0 0 1-5 5L4 17v3h3l5.7-5.7a4 4 0 0 0 5-5l-2.3 2.3-2.4-.6-.6-2.4 2.3-2.3z" /></Stroke>;
