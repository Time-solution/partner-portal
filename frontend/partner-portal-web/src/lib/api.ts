import { userManager } from "./auth/userManager";
import { apiBaseUrl } from "./auth/oidcConfig";

export interface LoginRequest {
  userName: string;
  password: string;
  rememberMe: boolean;
}

export interface LoginResponse {
  success: boolean;
  requiresTwoFactor?: boolean;
  error?: string;
}

export interface PagedResult<T> {
  totalCount: number;
  items: T[];
}

export type PartnerStatus = "Pending" | "Active" | "Suspended" | "Closed";

export type PartnerType =
  | "Aggregator"
  | "ThreePL"
  | "Carrier"
  | "Service"
  | "Marketplace";

export interface PartnerListItem {
  id: string;
  type: PartnerType | number;
  status: PartnerStatus | number;
  legalName: string;
  tradeName?: string;
  primaryContactEmail: string;
  creationTime: string;
  maskedIban?: string;
}

export interface PartnerApproveResult {
  partner: { id: string; status: PartnerStatus; legalName: string };
  clientId: string;
  clientSecret: string;
  scopes: string[];
  ownerIdentityUserId?: string;
  ownerSetPasswordToken?: string;
}

export interface GetPartnersParams {
  skipCount?: number;
  maxResultCount?: number;
  status?: PartnerStatus;
  filter?: string;
}

async function getAccessToken(): Promise<string | null> {
  const user = await userManager.getUser();
  if (!user || user.expired) {
    return null;
  }
  return user.access_token;
}

export async function apiFetch<T>(
  path: string,
  init: RequestInit = {},
): Promise<T> {
  const token = await getAccessToken();
  if (!token) {
    throw new Error("NotAuthenticated");
  }

  const headers = new Headers(init.headers);
  headers.set("Authorization", `Bearer ${token}`);
  if (init.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers,
  });

  if (!response.ok) {
    const message = await response.text();
    throw new Error(message || `Request failed (${response.status})`);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

/** Owned login: establishes the ABP identity cookie on the backend origin. */
export async function apiLogin(request: LoginRequest): Promise<LoginResponse> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/account/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "include",
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      return { success: false, error: "ServerError" };
    }

    return (await response.json()) as LoginResponse;
  } catch {
    return { success: false, error: "NetworkError" };
  }
}

export async function apiLogout(): Promise<void> {
  try {
    await fetch(`${apiBaseUrl}/api/account/logout`, {
      method: "POST",
      credentials: "include",
    });
  } catch {
    // Best-effort: the OIDC end-session still clears local tokens.
  }
}

export async function fetchPartners(
  params: GetPartnersParams = {},
): Promise<PagedResult<PartnerListItem>> {
  const query = new URLSearchParams();
  if (params.skipCount != null) query.set("skipCount", String(params.skipCount));
  if (params.maxResultCount != null) {
    query.set("maxResultCount", String(params.maxResultCount));
  }
  if (params.status) query.set("status", params.status);
  if (params.filter) query.set("filter", params.filter);

  const qs = query.toString();
  return apiFetch<PagedResult<PartnerListItem>>(
    `/api/admin/partners${qs ? `?${qs}` : ""}`,
  );
}

export async function approvePartner(
  id: string,
  notes?: string,
): Promise<PartnerApproveResult> {
  return apiFetch<PartnerApproveResult>(`/api/admin/partners/${id}/approve`, {
    method: "POST",
    body: JSON.stringify(notes ? { notes } : {}),
  });
}

export async function rejectPartner(id: string, notes?: string): Promise<void> {
  await apiFetch(`/api/admin/partners/${id}/reject`, {
    method: "POST",
    body: JSON.stringify(notes ? { notes } : {}),
  });
}

export async function suspendPartner(id: string, notes?: string): Promise<void> {
  await apiFetch(`/api/admin/partners/${id}/suspend`, {
    method: "POST",
    body: JSON.stringify(notes ? { notes } : {}),
  });
}

export async function reactivatePartner(id: string, notes?: string): Promise<void> {
  await apiFetch(`/api/admin/partners/${id}/reactivate`, {
    method: "POST",
    body: JSON.stringify(notes ? { notes } : {}),
  });
}

export async function closePartner(id: string, notes?: string): Promise<void> {
  await apiFetch(`/api/admin/partners/${id}/close`, {
    method: "POST",
    body: JSON.stringify(notes ? { notes } : {}),
  });
}
