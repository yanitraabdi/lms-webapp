import { apiInternalUrl, relayAuth } from "@/lib/auth/bff";

export async function POST(request: Request) {
  const upstream = await fetch(`${apiInternalUrl()}/api/auth/login`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: await request.text(),
  });
  return relayAuth(upstream);
}
