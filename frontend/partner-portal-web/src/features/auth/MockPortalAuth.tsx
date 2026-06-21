import { useCallback, useEffect, useState } from "react";
import { createContext, useContext, useMemo, type ReactNode } from "react";
import { hasAnyPermission, hasPermission } from "@/lib/permissions";
import { permissionsForRole, type PortalRole } from "@/lib/rbac/portalRoles";
import { getPortalDataSource } from "@/lib/data";
import type { Org, OrgUser } from "@/lib/org/orgModel";
import { userCan, type OrgContext, type OrgPermission } from "@/lib/org/orgModel";
import { buildOrgSession, sessionForRole, type OrgSession } from "@/features/auth/orgSession";
import type { AuthUser } from "@/features/auth/types";

export interface MockLoginResult {
  ok: boolean;
  landing?: string;
  error?: "invalid" | "required";
}

interface MockPortalAuthValue {
  user: AuthUser | null;
  role: PortalRole;
  isAuthenticated: boolean;
  /** Resolved multi-org session for the signed-in user. */
  org: Org | null;
  orgUser: OrgUser | null;
  orgCtx: OrgContext | null;
  scopedPartnerId?: string;
  /** Email/password login against seeded org users (mock — password not checked). */
  loginWithEmail: (email: string, password: string) => Promise<MockLoginResult>;
  /** Switch legacy role (demo convenience) — also re-scopes the data layer. */
  setRole: (role: PortalRole) => void;
  can: (permission: string) => boolean;
  canAny: (permissions: readonly string[]) => boolean;
  /** Granular org-permission check (layer 2) for the new org screens. */
  orgCan: (permission: OrgPermission) => boolean;
  logout: () => void;
}

export const MockPortalAuthContext = createContext<MockPortalAuthValue | undefined>(undefined);

function authUserFor(session: OrgSession): AuthUser {
  return {
    id: session.orgUser.id,
    name: session.orgUser.name,
    username: session.orgUser.email,
    roles: [session.role],
    // Legacy portal permissions keep the existing shell/route gating working.
    permissions: [...permissionsForRole(session.role)],
  };
}

export function MockPortalAuthProvider({ children }: { children: ReactNode }) {
  const ds = getPortalDataSource();
  const [session, setSession] = useState<OrgSession | null>(null);
  const [directory, setDirectory] = useState<{ orgs: Org[]; orgUsers: OrgUser[] }>({
    orgs: [],
    orgUsers: [],
  });

  // Load the org directory once so the role switcher can re-scope without a round-trip.
  useEffect(() => {
    let active = true;
    void ds.getAll().then((data) => {
      if (active) setDirectory({ orgs: data.orgs, orgUsers: data.orgUsers });
    });
    return () => {
      active = false;
    };
  }, [ds]);

  const applySession = useCallback(
    (next: OrgSession | null) => {
      ds.setActiveScope(next ? next.ctx : null);
      setSession(next);
    },
    [ds],
  );

  const loginWithEmail = useCallback(
    async (email: string, _password: string): Promise<MockLoginResult> => {
      if (!email.trim() || !_password.trim()) return { ok: false, error: "required" };
      const match = await ds.authenticate(email, _password);
      if (!match) return { ok: false, error: "invalid" };
      const next = buildOrgSession(match.org, match.orgUser);
      applySession(next);
      return { ok: true, landing: next.landing };
    },
    [ds, applySession],
  );

  const setRole = useCallback(
    (role: PortalRole) => {
      const next = sessionForRole(directory.orgs, directory.orgUsers, role);
      if (next) applySession(next);
    },
    [directory, applySession],
  );

  const logout = useCallback(() => {
    applySession(null);
  }, [applySession]);

  const user = useMemo(() => (session ? authUserFor(session) : null), [session]);

  const can = useCallback(
    (permission: string) => hasPermission(user?.permissions ?? [], permission),
    [user?.permissions],
  );
  const canAny = useCallback(
    (permissions: readonly string[]) => hasAnyPermission(user?.permissions ?? [], permissions),
    [user?.permissions],
  );
  const orgCan = useCallback(
    (permission: OrgPermission) => (session ? userCan(session.orgUser, permission) : false),
    [session],
  );

  const value = useMemo<MockPortalAuthValue>(
    () => ({
      user,
      role: session?.role ?? "PlatformAdmin",
      isAuthenticated: session !== null,
      org: session?.org ?? null,
      orgUser: session?.orgUser ?? null,
      orgCtx: session?.ctx ?? null,
      scopedPartnerId: session?.scopedPartnerId,
      loginWithEmail,
      setRole,
      can,
      canAny,
      orgCan,
      logout,
    }),
    [user, session, loginWithEmail, setRole, can, canAny, orgCan, logout],
  );

  return (
    <MockPortalAuthContext.Provider value={value}>{children}</MockPortalAuthContext.Provider>
  );
}

export function useMockPortalAuth(): MockPortalAuthValue {
  const ctx = useContext(MockPortalAuthContext);
  if (!ctx) throw new Error("useMockPortalAuth requires MockPortalAuthProvider");
  return ctx;
}

export function usePortalAuth(): {
  user: AuthUser | null;
  can: (p: string) => boolean;
  canAny: (p: readonly string[]) => boolean;
} {
  const mock = useContext(MockPortalAuthContext);
  if (mock) {
    return { user: mock.user, can: mock.can, canAny: mock.canAny };
  }
  throw new Error("usePortalAuth: wrap with MockPortalAuthProvider in mock mode");
}
