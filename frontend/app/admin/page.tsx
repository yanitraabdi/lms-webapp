"use client";

import { RouteStub } from "@/components/RouteStub";

// /admin — CSR (client component), role-gated placeholder.
// M0: no real auth/role check yet — gating logic lands in a later milestone.
export default function AdminPage() {
  return <RouteStub name="Admin" mode="CSR (client component, role-gated placeholder)" />;
}
