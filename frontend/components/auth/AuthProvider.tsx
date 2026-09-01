"use client";

import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from "react";
import type { AuthSession, AuthUser } from "@/lib/auth/types";
import { registerSession } from "@/lib/auth/session";

type Status = "loading" | "authenticated" | "unauthenticated";

interface AuthContextValue {
  status: Status;
  user: AuthUser | null;
  accessToken: string | null;
  login: (email: string, password: string) => Promise<void>;
  register: (name: string, email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  refresh: () => Promise<boolean>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

async function toError(res: Response): Promise<Error> {
  const data = await res.json().catch(() => null);
  const title =
    data && typeof data === "object" && typeof (data as Record<string, unknown>).title === "string"
      ? (data as Record<string, string>).title
      : "Terjadi kesalahan. Coba lagi.";
  return new Error(title);
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<Status>("loading");
  const [user, setUser] = useState<AuthUser | null>(null);
  const [accessToken, setAccessToken] = useState<string | null>(null);

  // Latest token, readable by non-React callers (see lib/auth/session.ts) without a re-render.
  const tokenRef = useRef<string | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const refreshRef = useRef<() => Promise<boolean>>(async () => false);

  const apply = useCallback((s: AuthSession) => {
    tokenRef.current = s.accessToken;
    setAccessToken(s.accessToken);
    setUser(s.user);
    setStatus("authenticated");

    // Refresh a minute before expiry rather than waiting to be rejected. Without this the token
    // simply died after 15 minutes and every later call 401'd until the page was reloaded — which
    // would strand a learner part-way through a 115-minute exam.
    if (timer.current) clearTimeout(timer.current);
    const lead = Math.max((s.expiresInSeconds ?? 900) - 60, 30);
    timer.current = setTimeout(() => { void refreshRef.current(); }, lead * 1000);
  }, []);

  const clear = useCallback(() => {
    if (timer.current) { clearTimeout(timer.current); timer.current = null; }
    tokenRef.current = null;
    setAccessToken(null);
    setUser(null);
    setStatus("unauthenticated");
  }, []);

  const refresh = useCallback(async () => {
    try {
      const res = await fetch("/api/auth/refresh", { method: "POST" });
      if (!res.ok) {
        clear();
        return false;
      }
      apply((await res.json()) as AuthSession);
      return true;
    } catch {
      clear();
      return false;
    }
  }, [apply, clear]);

  // Let plain modules (lib/*) read the live token and trigger a refresh on a 401.
  refreshRef.current = refresh;
  useEffect(() => {
    registerSession(() => tokenRef.current, () => refreshRef.current());
  }, []);

  // Bootstrap the session from the httpOnly refresh cookie on first load.
  useEffect(() => {
    void refresh();
  }, [refresh]);

  useEffect(() => () => { if (timer.current) clearTimeout(timer.current); }, []);

  const login = useCallback(
    async (email: string, password: string) => {
      const res = await fetch("/api/auth/login", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email, password }),
      });
      if (!res.ok) throw await toError(res);
      apply((await res.json()) as AuthSession);
    },
    [apply]
  );

  const register = useCallback(
    async (name: string, email: string, password: string) => {
      const res = await fetch("/api/auth/register", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ name, email, password }),
      });
      if (!res.ok) throw await toError(res);
      apply((await res.json()) as AuthSession);
    },
    [apply]
  );

  const logout = useCallback(async () => {
    await fetch("/api/auth/logout", { method: "POST" }).catch(() => {});
    clear();
  }, [clear]);

  const value: AuthContextValue = { status, user, accessToken, login, register, logout, refresh };
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

// Loading-state value used only during server render. The authenticated surfaces
// are CSR (TSD §4.2): the server renders a "loading" shell, the client hydrates
// with the real provider. On the client a missing provider is still a hard error.
const ssrFallback: AuthContextValue = {
  status: "loading",
  user: null,
  accessToken: null,
  login: () => Promise.reject(new Error("auth not ready")),
  register: () => Promise.reject(new Error("auth not ready")),
  logout: () => Promise.resolve(),
  refresh: () => Promise.resolve(false),
};

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (ctx) return ctx;
  if (typeof window === "undefined") return ssrFallback;
  throw new Error("useAuth must be used within <AuthProvider>");
}
