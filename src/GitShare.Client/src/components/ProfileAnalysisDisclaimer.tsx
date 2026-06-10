import { Info } from 'lucide-react';
import { useLocale } from '../i18n/LocaleProvider';

interface ProfileAnalysisDisclaimerProps {
  compact?: boolean;
  dense?: boolean;
  prominent?: boolean;
}

export function ProfileAnalysisDisclaimer({
  compact = false,
  dense = false,
  prominent = false,
}: ProfileAnalysisDisclaimerProps) {
  const { t } = useLocale();
  const isTight = compact || dense;

  return (
    <aside
      className={`flex gap-2 rounded-lg border border-zinc-800/80 bg-zinc-950/40 ${
        prominent
          ? 'mt-2 px-2.5 py-1.5'
          : dense
            ? 'mt-2 px-2.5 py-1.5'
            : compact
              ? 'mt-2 px-2.5 py-2'
              : 'mt-3 px-3 py-2.5'
      }`}
      role="note"
      aria-label={t('profile.disclaimerAria')}
    >
      <Info
        className={`shrink-0 text-zinc-500 ${
          prominent
            ? 'mt-0.5 h-4 w-4'
            : isTight
              ? 'mt-0.5 h-3 w-3'
              : 'mt-0.5 h-3.5 w-3.5'
        }`}
        aria-hidden
      />
      <p
        className={`leading-snug text-zinc-500 ${
          prominent
            ? 'text-[11px] leading-relaxed'
            : dense
              ? 'line-clamp-2 text-[10px]'
              : compact
                ? 'text-[10px]'
                : 'text-[11px] leading-relaxed'
        }`}
      >
        {t('profile.disclaimer')}
      </p>
    </aside>
  );
}
