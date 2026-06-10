import type { AppLocale } from '../i18n/types';
import type { DevCardProfile } from '../types';
import { auditMatchesLocale, profileContentLocaleMatches } from './auditLocale';

function cacheKey(username: string, locale: AppLocale): string {
  return `${username.trim().toLowerCase()}:${locale}`;
}

export function getValidCachedProfile(
  username: string,
  locale: AppLocale,
  store: Map<string, DevCardProfile>,
): DevCardProfile | null {
  const cached = store.get(cacheKey(username, locale));
  if (!cached) {
    return null;
  }

  if (
    !profileContentLocaleMatches(cached.ContentLocale, locale) ||
    !auditMatchesLocale(cached.AuditData, locale)
  ) {
    store.delete(cacheKey(username, locale));
    return null;
  }

  return cached;
}

export function setCachedProfile(
  username: string,
  locale: AppLocale,
  profile: DevCardProfile,
  store: Map<string, DevCardProfile>,
): void {
  store.set(cacheKey(username, locale), profile);
}
