export const ADMIN = { email: "e2e-admin@test.local", password: "E2e-Admin-Only-1!" };

const base = () => `${process.env.BASE_URL ?? "http://localhost:3100"}/api`;

export async function api(
  path: string,
  { method = "GET", token, body }: { method?: string; token?: string; body?: unknown } = {},
): Promise<any> {
  const res = await fetch(`${base()}${path}`, {
    method,
    headers: { ...(body !== undefined && { "content-type": "application/json" }), ...(token && { authorization: `Bearer ${token}` }) },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  const text = await res.text();
  if (!res.ok) throw new Error(`${method} ${path} -> ${res.status}: ${text}`);
  return text ? JSON.parse(text) : null;
}

export async function login(email: string, password: string): Promise<string> {
  return (await api("/auth/login", { method: "POST", body: { email, password } })).accessToken;
}
