const DEFAULT_AUTHORITY = "https://localhost:44300";
const DEFAULT_CLIENT_ID = "zahy-partner-web";
const DEFAULT_SCOPE =
  "openid profile email roles offline_access catalog:read orders:read partners:manage";

const origin = typeof window !== "undefined" ? window.location.origin : "http://localhost:5173";

export const authority = import.meta.env.VITE_OIDC_AUTHORITY ?? DEFAULT_AUTHORITY;

export const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? authority).replace(/\/$/, "");

/** Matches the seeded redirect URI: `{root}/auth/callback`. */
export const REDIRECT_PATH = "/auth/callback";

export const oidcConfig = {
  authority,
  client_id: import.meta.env.VITE_OIDC_CLIENT_ID ?? DEFAULT_CLIENT_ID,
  redirect_uri: `${origin}${REDIRECT_PATH}`,
  // Seeder registers the post-logout redirect as the SPA root with no trailing slash.
  post_logout_redirect_uri: origin,
  response_type: "code",
  scope: import.meta.env.VITE_OIDC_SCOPE ?? DEFAULT_SCOPE,
} as const;
