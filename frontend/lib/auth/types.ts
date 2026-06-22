export interface AuthUser {
  id: string;
  email: string;
  name: string;
  role: string;
  emailVerified: boolean;
}

/** What the SPA receives from the BFF — never includes the refresh token. */
export interface AuthSession {
  accessToken: string;
  expiresInSeconds: number;
  user: AuthUser;
}
