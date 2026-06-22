// Server-only helpers for the auth BFF route handlers.
import { NextResponse } from "next/server";

export const REFRESH_COOKIE = "academy_rt";

/** Internal base URL for server-to-server calls to the .NET API. */
export const apiInternalUrl = () => process.env.API_INTERNAL_URL ?? "http://localhost:8080";

export function refreshCookieOptions(maxAge = 60 * 60 * 24 * 30) {
  return {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax" as const,
    path: "/api/auth",
    maxAge,
  };
}

/**
 * Relay a .NET auth response to the SPA: move the refresh token into an httpOnly
 * cookie and return only { accessToken, expiresInSeconds, user }. The refresh
 * token never reaches the browser's JS.
 */
export async function relayAuth(upstream: Response): Promise<NextResponse> {
  const data = await upstream.json().catch(() => null);
  if (!upstream.ok || !data?.accessToken) {
    return NextResponse.json(data ?? { title: "Autentikasi gagal." }, { status: upstream.status || 502 });
  }
  const res = NextResponse.json({
    accessToken: data.accessToken,
    expiresInSeconds: data.expiresInSeconds,
    user: data.user,
  });
  res.cookies.set(REFRESH_COOKIE, data.refreshToken, refreshCookieOptions());
  return res;
}
