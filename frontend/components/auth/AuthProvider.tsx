"use client";

import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from "react";
import type { AuthSession, AuthUser } from "@/lib/auth/types";

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

  const apply = useCallback((s: AuthSession) => {
    setAccessToken(s.accessToken);
    setUser(s.user);
    setStatus("authenticated");
  }, []);

  const clear = useCallback(() => {
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

  // Bootstrap the session from the httpOnly refresh cookie on first load.
  useEffect(() => {
    void refresh();
  }, [refresh]);

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
