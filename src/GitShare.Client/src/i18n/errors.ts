import { ProfileFetchError } from '../api';
import type { Translator } from './translate';

export function translateApiError(error: unknown, t: Translator): string {
  if (error instanceof ProfileFetchError) {
    if (error.status === 404) {
      return t('errors.notFound');
    }
    if (error.kind === 'force_refresh_rate_limit') {
      return t('errors.forceRefreshRateLimit');
    }
    if (error.status === 403 || error.kind === 'github_rate_limit') {
      return t('errors.rateLimit');
    }
    if (error.status === 429 || error.kind === 'ai_rate_limit') {
      return t('errors.modelsRateLimit');
    }
    if (error.status === 502 && error.message.includes('AI Bridge Failed')) {
      return error.message;
    }
    if (error.status >= 500) {
      return t('errors.server');
    }
    return t('errors.unreachable');
  }

  if (error instanceof TypeError) {
    return t('errors.network');
  }

  if (error instanceof Error && error.message) {
    return error.message;
  }

  return t('errors.unknown');
}
