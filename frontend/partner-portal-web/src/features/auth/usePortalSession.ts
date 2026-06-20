import { useContext } from "react";
import { AuthContext } from "./AuthContext";
import { MockPortalAuthContext } from "./MockPortalAuth";
import type { PortalRole } from "@/lib/rbac/portalRoles";
import { mockScopedPartnerId } from "@/lib/rbac/roleNavConfig";
import type { AuthUser } from "./types";

export interface PortalSession {
  user: AuthUser;
  role: PortalRole | string;
  setRole?: (role: PortalRole) => void;
  scopedPartnerId?: string;
  logout: () => void | Promise<void>;
  can: (permission: string) => boolean;
  canAny: (permissions: readonly string[]) => boolean;
  isMock: boolean;
}

export function usePortalSession(): PortalSession {
  const mock = useContext(MockPortalAuthContext);
  const auth = useContext(AuthContext);

  if (mock?.user) {
    return {
      user: mock.user,
      role: mock.role,
      setRole: mock.setRole,
      scopedPartnerId: mockScopedPartnerId(mock.role),
      logout: mock.logout,
      can: mock.can,
      canAny: mock.canAny,
      isMock: true,
    };
  }

  if (auth?.user) {
    return {
      user: auth.user,
      role: auth.user.roles[0] ?? "PlatformAdmin",
      logout: auth.logout,
      can: auth.can,
      canAny: auth.canAny,
      isMock: false,
    };
  }

  throw new Error("usePortalSession requires MockPortalAuthProvider or authenticated AuthProvider");
}

export function useMockPortalAuthState() {
  const mock = useContext(MockPortalAuthContext);
  if (!mock) throw new Error("useMockPortalAuthState requires MockPortalAuthProvider");
  return {
    isAuthenticated: mock.isAuthenticated,
    login: mock.login,
    role: mock.role,
  };
}
