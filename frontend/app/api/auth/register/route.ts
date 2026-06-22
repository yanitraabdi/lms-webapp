import { apiInternalUrl, relayAuth } from "@/lib/auth/bff";

export async function POST(request: Request) {
  const upstream = await fetch(`${apiInternalUrl()}/api/auth/register`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: await request.text(),
  });
  return relayAuth(upstream);
}
