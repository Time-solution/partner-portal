import type { AuthUser } from "@/features/auth/types";
import { apiBaseUrl } from "./oidcConfig";

export interface AbpApplicationConfig {
  currentUser?: {
    id?: string;
    userName?: string;
    name?: string;
    roles?: string[];
    isAuthenticated?: boolean;
  };
  auth?: {
    grantedPolicies?: Record<string, boolean>;
  };
}

export interface AuthUserFallback {
  id?: string;
  name?: string;
  username?: string;
  roles?: string[];
}

/**
 * Maps ABP's application-configuration response to the app's AuthUser. The
 * granted policies map (permission name -> bool) becomes the permission list
 * that drives role-gated navigation, keeping the UI aligned with the backend.
 */
export function mapAbpConfigToAuthUser(
  config: AbpApplicationConfig,
  fallback: AuthUserFallback = {},
): AuthUser {
  const currentUser = config.currentUser ?? {};
  const grantedPolicies = config.auth?.grantedPolicies ?? {};
  const permissions = Object.keys(grantedPolicies).filter((key) => grantedPolicies[key]);

  return {
    id: currentUser.id ?? fallback.id ?? "",
    name: currentUser.name ?? currentUser.userName ?? fallback.name ?? "",
    username: currentUser.userName ?? fallback.username ?? "",
    roles: currentUser.roles ?? fallback.roles ?? [],
    permissions,
  };
}

export async function fetchAbpSession(
  accessToken: string,
  fallback: AuthUserFallback = {},
): Promise<AuthUser> {
  const response = await fetch(`${apiBaseUrl}/api/abp/application-configuration`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });

  if (!response.ok) {
    throw new Error(`Failed to load application configuration (${response.status})`);
  }

  const data = (await response.json()) as AbpApplicationConfig;
  return mapAbpConfigToAuthUser(data, fallback);
}
