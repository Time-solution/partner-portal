import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { hasAnyPermission, hasPermission } from "@/lib/permissions";
import { apiLogin, apiLogout, type LoginRequest, type LoginResponse } from "@/lib/api";
import { REDIRECT_PATH } from "@/lib/auth/oidcConfig";
import { userManager, type User } from "@/lib/auth/userManager";
import { fetchAbpSession } from "@/lib/auth/session";
import type { AuthUser } from "./types";

type AuthStatus = "loading" | "authenticated" | "unauthenticated";

interface AuthContextValue {
  user: AuthUser | null;
  status: AuthStatus;
  isAuthenticated: boolean;
  isLoading: boolean;
  /** Owned login: validate credentials, then start Auth Code + PKCE. */
  signIn: (credentials: LoginRequest) => Promise<LoginResponse>;
  logout: () => Promise<void>;
  can: (permission: string) => boolean;
  canAny: (permissions: readonly string[]) => boolean;
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined);

function profileFallback(oidcUser: User) {
  const profile = oidcUser.profile as Record<string, unknown>;
  const rawRoles = profile.role ?? profile.roles;
  const roles = Array.isArray(rawRoles)
    ? (rawRoles as string[])
    : typeof rawRoles === "string"
      ? [rawRoles]
      : [];
  return {
    id: typeof profile.sub === "string" ? profile.sub : "",
    name: typeof profile.name === "string" ? profile.name : undefined,
    username:
      typeof profile.preferred_username === "string" ? profile.preferred_username : undefined,
    roles,
  };
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [status, setStatus] = useState<AuthStatus>("loading");

  const hydrate = useCallback(async (oidcUser: User) => {
    const fallback = profileFallback(oidcUser);
    try {
      const hydrated = await fetchAbpSession(oidcUser.access_token, fallback);
      setUser(hydrated);
    } catch {
      // Backend config unavailable: fall back to claims from the token.
      setUser({
        id: fallback.id,
        name: fallback.name ?? fallback.username ?? "",
        username: fallback.username ?? "",
        roles: fallback.roles,
        permissions: [],
      });
    }
    setStatus("authenticated");
  }, []);

  useEffect(() => {
    let cancelled = false;

    const init = async () => {
      try {
        if (window.location.pathname === REDIRECT_PATH) {
          const oidcUser = await userManager.signinRedirectCallback();
          window.history.replaceState({}, document.title, "/");
          if (!cancelled) await hydrate(oidcUser);
          return;
        }

        const existing = await userManager.getUser();
        if (existing && !existing.expired) {
          if (!cancelled) await hydrate(existing);
        } else if (!cancelled) {
          setStatus("unauthenticated");
        }
      } catch {
        if (!cancelled) {
          setStatus("unauthenticated");
          window.history.replaceState({}, document.title, "/");
        }
      }
    };

    void init();

    const onExpired = () => {
      setUser(null);
      setStatus("unauthenticated");
    };
    userManager.events.addAccessTokenExpired(onExpired);
    userManager.events.addSilentRenewError(onExpired);

    return () => {
      cancelled = true;
      userManager.events.removeAccessTokenExpired(onExpired);
      userManager.events.removeSilentRenewError(onExpired);
    };
  }, [hydrate]);

  const signIn = useCallback(async (credentials: LoginRequest): Promise<LoginResponse> => {
    const result = await apiLogin(credentials);
    if (result.success) {
      // Cookie established; complete the OIDC flow (navigates away).
      await userManager.signinRedirect();
    }
    return result;
  }, []);

  const logout = useCallback(async () => {
    await apiLogout();
    setUser(null);
    setStatus("unauthenticated");
    try {
      await userManager.signoutRedirect();
    } catch {
      await userManager.removeUser();
    }
  }, []);

  const can = useCallback(
    (permission: string) => (user ? hasPermission(user.permissions, permission) : false),
    [user],
  );

  const canAny = useCallback(
    (permissions: readonly string[]) =>
      user ? hasAnyPermission(user.permissions, permissions) : false,
    [user],
  );

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      status,
      isAuthenticated: status === "authenticated" && user !== null,
      isLoading: status === "loading",
      signIn,
      logout,
      can,
      canAny,
    }),
    [user, status, signIn, logout, can, canAny],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return ctx;
}
