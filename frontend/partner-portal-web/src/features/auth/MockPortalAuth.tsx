import { useCallback, useState } from "react";
import {
  createContext,
  useContext,
  useMemo,
  type ReactNode,
} from "react";
import { hasAnyPermission, hasPermission } from "@/lib/permissions";
import {
  permissionsForRole,
  type PortalRole,
} from "@/lib/rbac/portalRoles";
import type { AuthUser } from "@/features/auth/types";

interface MockPortalAuthValue {
  user: AuthUser | null;
  role: PortalRole;
  isAuthenticated: boolean;
  login: (role: PortalRole) => void;
  setRole: (role: PortalRole) => void;
  can: (permission: string) => boolean;
  canAny: (permissions: readonly string[]) => boolean;
  logout: () => void;
}

export const MockPortalAuthContext = createContext<MockPortalAuthValue | undefined>(undefined);

const mockUsers: Record<PortalRole, { name: string; username: string }> = {
  PlatformAdmin: { name: "Admin User", username: "admin@zahy.sa" },
  Accountant: { name: "Omar Finance", username: "accountant@zahy.sa" },
  PartnerSuccessManager: { name: "Sara Al-Qahtani", username: "psm@zahy.sa" },
  PartnerFinance: { name: "Partner Finance", username: "finance@partner.sa" },
};

function buildUser(role: PortalRole): AuthUser {
  const profile = mockUsers[role];
  return {
    id: `mock-${role}`,
    name: profile.name,
    username: profile.username,
    roles: [role],
    permissions: [...permissionsForRole(role)],
  };
}

export function MockPortalAuthProvider({ children }: { children: ReactNode }) {
  const [isAuthenticated, setIsAuthenticated] = useState(true);
  const [role, setRoleState] = useState<PortalRole>("PlatformAdmin");
  const user = useMemo(
    () => (isAuthenticated ? buildUser(role) : null),
    [isAuthenticated, role],
  );

  const login = useCallback((next: PortalRole) => {
    setRoleState(next);
    setIsAuthenticated(true);
  }, []);

  const setRole = useCallback((next: PortalRole) => setRoleState(next), []);

  const can = useCallback(
    (permission: string) => hasPermission(user?.permissions ?? [], permission),
    [user?.permissions],
  );

  const canAny = useCallback(
    (permissions: readonly string[]) => hasAnyPermission(user?.permissions ?? [], permissions),
    [user?.permissions],
  );

  const logout = useCallback(() => {
    setIsAuthenticated(false);
  }, []);

  const value = useMemo(
    () => ({ user, role, isAuthenticated, login, setRole, can, canAny, logout }),
    [user, role, isAuthenticated, login, setRole, can, canAny, logout],
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
