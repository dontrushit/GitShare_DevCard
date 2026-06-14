import { AlertTriangle } from 'lucide-react';
import type { DevCardProfile } from '../types';
import { useLocale } from '../i18n/LocaleProvider';

interface LowConfidenceNoticeProps {
  profile: DevCardProfile;
  compact?: boolean;
}

export function LowConfidenceNotice({ profile, compact = false }: LowConfidenceNoticeProps) {
  const { t } = useLocale();
  const level = profile.ProgrammerLevel;

  if (!level?.IsLowConfidence) {
    return null;
  }

  const confidence =
    level.SignalConfidence != null ? level.SignalConfidence.toFixed(2) : undefined;

  return (
    <aside
      className={`flex gap-2 rounded-md border border-amber-900/50 bg-amber-950/30 ${
        compact ? 'mt-1.5 px-2 py-1' : 'mt-2 px-2.5 py-1.5'
      }`}
      role="note"
      aria-label={t('profile.lowConfidence')}
    >
      <AlertTriangle
        className={`shrink-0 text-amber-500/90 ${compact ? 'mt-0.5 h-3 w-3' : 'h-3.5 w-3.5'}`}
        aria-hidden
      />
      <p className={`leading-snug text-amber-200/80 ${compact ? 'text-[10px]' : 'text-[11px]'}`}>
        {t('profile.lowConfidenceBanner', { value: confidence ?? '—' })}
      </p>
    </aside>
  );
}
