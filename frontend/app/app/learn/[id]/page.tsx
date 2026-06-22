"use client";

import { use } from "react";
import { RouteStub } from "@/components/RouteStub";

// /app/learn/[id] — CSR (client component). params is a Promise in Next 15;
// unwrap it with React.use() inside the client component.
export default function LearnPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = use(params);
  return <RouteStub name={`Learn: ${id}`} mode="CSR (client component)" />;
}
