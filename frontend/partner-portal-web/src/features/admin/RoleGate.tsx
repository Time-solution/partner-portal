import type { ReactNode } from "react";
import { useAuth } from "@/features/auth/AuthContext";

interface RoleGateProps {
  permissions: string | readonly string[];
  children: ReactNode;
  fallback?: ReactNode;
}

/** Renders children only when the current user holds (any of) the permission(s). */
export function RoleGate({ permissions, children, fallback = null }: RoleGateProps) {
  const { canAny } = useAuth();
  const list = typeof permissions === "string" ? [permissions] : permissions;
  return <>{canAny(list) ? children : fallback}</>;
}
