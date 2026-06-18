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
