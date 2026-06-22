import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { REFRESH_COOKIE, apiInternalUrl, refreshCookieOptions } from "@/lib/auth/bff";

export async function POST() {
  const rt = (await cookies()).get(REFRESH_COOKIE)?.value;
  if (rt) {
    await fetch(`${apiInternalUrl()}/api/auth/logout`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ refreshToken: rt }),
    }).catch(() => {});
  }
  const res = new NextResponse(null, { status: 204 });
  res.cookies.set(REFRESH_COOKIE, "", refreshCookieOptions(0));
  return res;
}
