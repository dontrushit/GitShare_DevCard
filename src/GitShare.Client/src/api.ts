import type { AppLocale } from './i18n/types';
import type { DevCardProfile } from './types';

function resolveApiBaseUrl(): string {
  // Dev: always same-origin + Vite proxy (/api → localhost:5188). Avoids CORS when opening :5173 or LAN IP.
  if (import.meta.env.DEV) {
    return '';
  }

  const configured = import.meta.env.VITE_API_URL?.trim();
  if (configured) {
    return configured.replace(/\/$/, '');
  }

  return 'http://localhost:5188';
}

export const API_BASE_URL = resolveApiBaseUrl();

export type ProfileErrorKind =
  | 'force_refresh_rate_limit'
  | 'ai_rate_limit'
  | 'github_rate_limit'
  | 'unknown';

interface ApiProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
}

export function parseApiProblemBody(body: string): { title?: string; detail?: string } {
  const trimmed = body.trim();
  if (!trimmed.startsWith('{')) {
    return { detail: trimmed || undefined };
  }

  try {
    const parsed = JSON.parse(trimmed) as ApiProblemDetails;
    return {
      title: typeof parsed.title === 'string' ? parsed.title.trim() : undefined,
      detail: typeof parsed.detail === 'string' ? parsed.detail.trim() : undefined,
    };
  } catch {
    return { detail: trimmed || undefined };
  }
}

export function classifyProfileError(
  status: number,
  title?: string,
  detail?: string,
): ProfileErrorKind {
  const hay = `${title ?? ''} ${detail ?? ''}`.toLowerCase();

  if (hay.includes('force refresh')) {
    return 'force_refresh_rate_limit';
  }

  if (hay.includes('github models') || hay.includes('ai rate limit') || hay.includes('ai bridge')) {
    return 'ai_rate_limit';
  }

  if (status === 403 || hay.includes('github rate limit')) {
    return 'github_rate_limit';
  }

  return 'unknown';
}

export class ProfileFetchError extends Error {
  readonly status: number;
  readonly kind: ProfileErrorKind;
  readonly problemTitle?: string;

  constructor(status: number, message: string, kind: ProfileErrorKind = 'unknown', problemTitle?: string) {
    super(message);
    this.name = 'ProfileFetchError';
    this.status = status;
    this.kind = kind;
    this.problemTitle = problemTitle;
  }
}

export async function fetchDevCardProfile(
  username: string,
  options?: { forceRefresh?: boolean; locale?: AppLocale },
): Promise<DevCardProfile> {
  const params = new URLSearchParams();
  if (options?.forceRefresh) {
    params.set('forceRefresh', 'true');
  }
  params.set('locale', options?.locale ?? 'ru');

  const query = params.toString();
  const url = `${API_BASE_URL}/api/profile/${encodeURIComponent(username.trim())}${
    query ? `?${query}` : ''
  }`;

  const response = await fetch(url);

  if (!response.ok) {
    const body = await response.text();
    const problem = parseApiProblemBody(body);
    const message = problem.detail || problem.title || body || response.statusText;
    const kind = classifyProfileError(response.status, problem.title, problem.detail);
    throw new ProfileFetchError(response.status, message, kind, problem.title);
  }

  return (await response.json()) as DevCardProfile;
}

export function getErrorMessage(error: unknown): string {
  if (error instanceof ProfileFetchError) {
    if (error.status === 404) {
      return 'Developer profile not found on GitHub.';
    }
    if (error.kind === 'force_refresh_rate_limit') {
      return 'Force refresh is allowed once every 10 minutes.';
    }
    if (error.status === 403 || error.kind === 'github_rate_limit') {
      return (
        error.message ||
        'Лимит запросов к GitHub API исчерпан. Пожалуйста, попробуйте позже или подключите персональный токен.'
      );
    }
    if (error.status === 429 || error.kind === 'ai_rate_limit') {
      return (
        error.message ||
        'GitHub Models API rate limit reached. Please wait a moment and try again.'
      );
    }
    if (error.status === 502 && error.message.includes('AI Bridge Failed')) {
      return error.message;
    }
    if (error.status >= 500) {
      return 'Server error while loading the profile. Ensure the API is running (dotnet run) and try Refresh.';
    }
    return 'Unable to reach the analytics server.';
  }

  if (error instanceof TypeError) {
    return 'Unable to reach the analytics server.';
  }

  return 'Unable to reach the analytics server.';
}
