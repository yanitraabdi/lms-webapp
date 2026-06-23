import { Suspense } from "react";
import { DevPayClient } from "./DevPayClient";

export const dynamic = "force-dynamic";

export default function DevPayPage() {
  return (
    <Suspense fallback={null}>
      <DevPayClient />
    </Suspense>
  );
}
