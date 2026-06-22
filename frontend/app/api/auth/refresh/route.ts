import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { REFRESH_COOKIE, apiInternalUrl, refreshCookieOptions, relayAuth } from "@/lib/auth/bff";

export async function POST() {
  const rt = (await cookies()).get(REFRESH_COOKIE)?.value;
  if (!rt) return NextResponse.json({ title: "Tidak ada sesi." }, { status: 401 });

  const upstream = await fetch(`${apiInternalUrl()}/api/auth/refresh`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ refreshToken: rt }),
  });

  if (!upstream.ok) {
    const res = NextResponse.json({ title: "Sesi berakhir." }, { status: 401 });
    res.cookies.set(REFRESH_COOKIE, "", refreshCookieOptions(0));
    return res;
  }
  return relayAuth(upstream);
}
