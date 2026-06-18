import { UserManager, WebStorageStateStore, type User } from "oidc-client-ts";
import { oidcConfig } from "./oidcConfig";

export const userManager = new UserManager({
  authority: oidcConfig.authority,
  client_id: oidcConfig.client_id,
  redirect_uri: oidcConfig.redirect_uri,
  post_logout_redirect_uri: oidcConfig.post_logout_redirect_uri,
  response_type: oidcConfig.response_type,
  scope: oidcConfig.scope,
  // Public SPA client — tokens persist in localStorage; rotating refresh tokens
  // are exchanged silently before expiry (matches the ~30 min access lifetime).
  userStore: new WebStorageStateStore({ store: window.localStorage }),
  automaticSilentRenew: true,
  monitorSession: false,
});

export type { User };
