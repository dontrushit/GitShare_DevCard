import { Info } from 'lucide-react';
import { useLocale } from '../i18n/LocaleProvider';
import { HoverPortalPopover } from './ui/HoverPortalPopover';

interface ProfileAnalysisDisclaimerProps {
  compact?: boolean;
  dense?: boolean;
  prominent?: boolean;
  iconOnly?: boolean;
}

export function ProfileAnalysisDisclaimer({
  compact = false,
  dense = false,
  prominent = false,
  iconOnly = false,
}: ProfileAnalysisDisclaimerProps) {
  const { t } = useLocale();
  const isTight = compact || dense;

  if (iconOnly) {
    return (
      <HoverPortalPopover
        id="profile-analysis-disclaimer"
        width={300}
        estimatedHeight={150}
        trigger={
          <span
            className="inline-flex h-4 w-4 shrink-0 items-center justify-center rounded-full border border-zinc-700/80 text-zinc-500 transition-colors hover:border-zinc-500 hover:text-zinc-300"
            role="note"
            aria-label={t('profile.disclaimerAria')}
          >
            <Info className="h-2.5 w-2.5" aria-hidden />
          </span>
        }
      >
        <p className="px-3 py-2 text-[10px] leading-relaxed text-zinc-400">
          {t('profile.disclaimer')}
        </p>
      </HoverPortalPopover>
    );
  }

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
