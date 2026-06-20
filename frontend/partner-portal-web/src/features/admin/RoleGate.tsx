import type { ReactNode } from "react";
import { usePortalSession } from "@/features/auth/usePortalSession";

interface RoleGateProps {
  permissions: string | readonly string[];
  children: ReactNode;
  fallback?: ReactNode;
}

/** Renders children only when the current user holds (any of) the permission(s). */
export function RoleGate({ permissions, children, fallback = null }: RoleGateProps) {
  const { canAny } = usePortalSession();
  const list = typeof permissions === "string" ? [permissions] : permissions;
  return <>{canAny(list) ? children : fallback}</>;
}
