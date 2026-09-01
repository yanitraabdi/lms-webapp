// Shared authenticated fetch.
//
// The access token lives 15 minutes and is held in memory only (GR-5). AuthProvider refreshes it
// proactively before it expires, but a timer is not enough on its own: a suspended laptop or a
// throttled background tab can miss the scheduled refresh, and the next call then 401s. Before
// this existed, that 401 surfaced as a bare error the learner could only clear by reloading the
// page — during a 115-minute exam with a server-authoritative clock, that means losing a paid
// attempt.
//
// So every authenticated call goes through here: on a 401 it refreshes once and retries with the
// NEW token. One retry only — if the refresh cookie is genuinely gone, retrying forever would just
// spin while the session is over.

type TokenGetter = () => string | null;
type Refresher = () => Promise<boolean>;

let getToken: TokenGetter = () => null;
let refreshSession: Refresher = async () => false;

/** AuthProvider registers itself here so plain modules can reach the live session. */
export function registerSession(token: TokenGetter, refresh: Refresher) {
  getToken = token;
  refreshSession = refresh;
}

function withAuth(init: RequestInit, token: string | null): RequestInit {
  const headers = new Headers(init.headers);
  if (token) headers.set("Authorization", `Bearer ${token}`);
  return { ...init, headers };
}

/**
 * Fetch with the current access token, retrying once after a refresh if the token had expired.
 * `fallbackToken` is what the calling module was holding — used for the first attempt so behaviour
 * is unchanged when the provider has not registered (e.g. server-side rendering).
 */
export async function apiFetch(
  url: string,
  init: RequestInit = {},
  fallbackToken?: string
): Promise<Response> {
  const first = getToken() ?? fallbackToken ?? null;
  const res = await fetch(url, withAuth(init, first));
  if (res.status !== 401) return res;

  // The body of a 401 is discarded; we only care whether a refresh rescues the call.
  if (!(await refreshSession())) return res;
  return fetch(url, withAuth(init, getToken() ?? first));
}
