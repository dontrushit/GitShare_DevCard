import { Clock } from 'lucide-react';
import type { DevCardProfile } from '../types';
import { useLocale } from '../i18n/LocaleProvider';

function formatAnalyzedAt(iso: string, locale: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return iso;
  }

  return new Intl.DateTimeFormat(locale === 'ru' ? 'ru-RU' : 'en-US', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(date);
}

interface ProfileCacheBannerProps {
  profile: DevCardProfile;
  compact?: boolean;
}

export function ProfileCacheBanner({ profile, compact = false }: ProfileCacheBannerProps) {
  const { t, locale } = useLocale();
  const analyzedAt = profile.AnalyzedAtUtc?.trim();

  if (!analyzedAt) {
    return null;
  }

  const formatted = formatAnalyzedAt(analyzedAt, locale);
  const message = profile.ServedFromCache
    ? t('profile.cacheFromCache', { date: formatted })
    : t('profile.cacheFresh', { date: formatted });

  return (
    <p
      className={`flex items-center gap-1 text-zinc-500 ${
        compact ? 'mt-1 text-[10px]' : 'mt-1.5 text-[11px]'
      }`}
      role="status"
    >
      <Clock className={`shrink-0 ${compact ? 'h-3 w-3' : 'h-3.5 w-3.5'}`} aria-hidden />
      <span>{message}</span>
    </p>
  );
}
