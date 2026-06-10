import type { AppLocale } from '../i18n/types';
import type { StructuredAuditResponse } from '../types';

const CYRILLIC = /[\u0400-\u04FF]/g;

function collectNarrative(audit: StructuredAuditResponse): string {
  const parts: string[] = [];
  if (audit.CoreEngineeringFocus) {
    parts.push(audit.CoreEngineeringFocus);
  }

  for (const project of audit.Projects ?? []) {
    parts.push(project.TechnicalDebt, project.InterviewTrapQuestion);
    parts.push(...(project.Pros ?? []), ...(project.Cons ?? []));
  }

  return parts.filter(Boolean).join(' ');
}

export function auditContainsCyrillic(audit: StructuredAuditResponse | null | undefined): boolean {
  if (!audit) {
    return false;
  }

  const combined = collectNarrative(audit);
  const matches = combined.match(CYRILLIC);
  return (matches?.length ?? 0) >= 8;
}

export function auditIsPredominantlyEnglish(audit: StructuredAuditResponse | null | undefined): boolean {
  if (!audit) {
    return false;
  }

  const combined = collectNarrative(audit);
  if (combined.length < 48) {
    return false;
  }

  const cyrillic = combined.match(CYRILLIC)?.length ?? 0;
  if (cyrillic >= 8) {
    return false;
  }

  const latinLetters = [...combined].filter(
    (c) => /\p{L}/u.test(c) && !/[\u0400-\u04FF]/.test(c),
  ).length;
  return latinLetters >= 40;
}

export function auditMatchesLocale(
  audit: StructuredAuditResponse | null | undefined,
  locale: AppLocale,
): boolean {
  if (!audit) {
    return true;
  }

  return locale === 'en' ? !auditIsPredominantlyEnglish(audit) : auditContainsCyrillic(audit);
}

export function profileContentLocaleMatches(
  profileLocale: string | undefined,
  locale: AppLocale,
): boolean {
  return (profileLocale ?? 'ru').toLowerCase() === locale;
}
