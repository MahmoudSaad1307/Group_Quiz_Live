export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

export class ApiError extends Error {
  public readonly status: number;
  public readonly problem?: ProblemDetails;

  constructor(status: number, message: string, problem?: ProblemDetails) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.problem = problem;
  }
}

function getCookie(name: string): string | undefined {
  if (typeof document === "undefined") return undefined;
  const match = document.cookie
    .split("; ")
    .find((row) => row.startsWith(`${name}=`));
  return match ? decodeURIComponent(match.slice(name.length + 1)) : undefined;
}

async function ensureCsrfToken(): Promise<string | undefined> {
  const existing = getCookie("XSRF-TOKEN");
  if (existing) return existing;

  const res = await fetch("/api/auth/csrf", {
    method: "GET",
    credentials: "include",
  });
  if (!res.ok) return undefined;
  const data = (await res.json()) as { token?: string };
  return data.token ?? getCookie("XSRF-TOKEN");
}

export async function apiFetch<T>(
  path: string,
  options: RequestInit = {},
): Promise<T> {
  const method = (options.method ?? "GET").toUpperCase();
  const isMutating = method !== "GET" && method !== "HEAD" && method !== "OPTIONS";

  const headers = new Headers(options.headers);
  if (!headers.has("Content-Type") && options.body && !(options.body instanceof FormData)) {
    headers.set("Content-Type", "application/json");
  }

  if (isMutating && !path.startsWith("/api/auth/login")) {
    const csrf = await ensureCsrfToken();
    if (csrf) {
      headers.set("X-XSRF-TOKEN", csrf);
    }
  }

  const response = await fetch(path, {
    ...options,
    method,
    headers,
    credentials: "include",
  });

  if (!response.ok) {
    let problem: ProblemDetails | undefined;
    try {
      problem = (await response.json()) as ProblemDetails;
    } catch {
      // Non-JSON error body
    }
    const message =
      problem?.detail ?? problem?.title ?? `Request failed (${response.status})`;
    throw new ApiError(response.status, message, problem);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export interface AdminProfile {
  id: string;
  email: string;
  displayName: string;
}

export const authApi = {
  login: (email: string, password: string) =>
    apiFetch<AdminProfile>("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ email, password }),
    }),
  logout: () =>
    apiFetch<void>("/api/auth/logout", {
      method: "POST",
    }),
  me: () => apiFetch<AdminProfile>("/api/auth/me"),
};
