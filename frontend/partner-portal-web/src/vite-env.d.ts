/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** OpenIddict authority (the Zahy.Identity host), e.g. https://localhost:44300 */
  readonly VITE_OIDC_AUTHORITY?: string;
  /** Public SPA client id seeded in OpenIddict. */
  readonly VITE_OIDC_CLIENT_ID?: string;
  /** Space-separated scopes to request. */
  readonly VITE_OIDC_SCOPE?: string;
  /** Backend API base URL (defaults to the authority). */
  readonly VITE_API_BASE_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
