import type { GitHubActivityTelemetry } from '../types';
import { useLocale } from '../i18n/LocaleProvider';

const FORMAT_CONVENTIONAL = 'Conventional Commits compliant';
const FORMAT_DESCRIPTIVE = 'Descriptive / Non-standard';
const FORMAT_UNSTRUCTURED = 'Unstructured / Low-density';

const CARD_BASE =
  'flex h-full min-h-0 w-full min-w-0 flex-col overflow-hidden rounded-xl border border-zinc-800 bg-zinc-900/40 p-5 lg:p-6';

/** Прокрутка как в панелях аудита — заполняет оставшуюся высоту карточки */
const COMMIT_LIST_SCROLL =
  'commit-history-scroll custom-scrollbar min-h-[8rem] flex-1 overflow-y-auto overscroll-y-contain rounded-lg border border-zinc-800 bg-zinc-950/50 p-2 touch-pan-y';

interface GitTelemetryCardProps {
  telemetry?: GitHubActivityTelemetry | null;
  gitFormatStandard?: string;
  engineeringFocus?: string;
  isLoading?: boolean;
  className?: string;
}

function normalizeFormatStandard(value?: string): string {
  if (!value?.trim()) {
    return FORMAT_UNSTRUCTURED;
  }

  const v = value.trim();
  if (v.includes('Conventional') && v.includes('compliant')) {
    return FORMAT_CONVENTIONAL;
  }
  if (v.includes('Descriptive') || v.includes('Non-standard')) {
    return FORMAT_DESCRIPTIVE;
  }
  if (v.includes('Unstructured') || v.includes('Low-density')) {
    return FORMAT_UNSTRUCTURED;
  }
  if (v.startsWith('High')) {
    return FORMAT_CONVENTIONAL;
  }
  if (v.startsWith('Medium')) {
    return FORMAT_DESCRIPTIVE;
  }
  if (v.startsWith('Low')) {
    return FORMAT_UNSTRUCTURED;
  }

  return v;
}

function badgeStyles(standard: string): string {
  if (standard === FORMAT_CONVENTIONAL) {
    return 'border border-emerald-900/50 bg-emerald-950/40 text-emerald-400';
  }

  if (standard === FORMAT_DESCRIPTIVE) {
    return 'border border-amber-900/50 bg-amber-950/40 text-amber-400';
  }

  return 'border border-zinc-700 bg-zinc-800 text-zinc-300';
}

function shortBadgeLabel(standard: string): string {
  if (standard === FORMAT_CONVENTIONAL) {
    return 'Conventional';
  }
  if (standard === FORMAT_DESCRIPTIVE) {
    return 'Non-standard';
  }
  return 'Low-density';
}

function readCommitMessages(telemetry?: GitHubActivityTelemetry | null): string[] {
  const raw =
    telemetry?.RecentCommitMessages ??
    (telemetry as { recentCommitMessages?: string[] } | null | undefined)?.recentCommitMessages ??
    [];

  return raw
    .map((m) => String(m).split('\n')[0]?.trim() ?? '')
    .filter(Boolean);
}

function TelemetrySkeleton() {
  return (
    <div className="flex min-h-0 flex-1 flex-col gap-4 animate-pulse" aria-busy="true">
      <div className="flex items-center justify-between gap-2">
        <div className="h-3 w-36 rounded bg-zinc-800" />
        <div className="h-6 w-24 rounded bg-zinc-800" />
      </div>
      <div className="min-h-[8rem] flex-1 space-y-2 rounded-lg border border-zinc-800 bg-zinc-950/50 p-2">
        <div className="h-10 rounded-lg bg-zinc-950/40" />
        <div className="h-10 rounded-lg bg-zinc-950/40" />
        <div className="h-10 rounded-lg bg-zinc-950/40" />
      </div>
    </div>
  );
}

export function GitTelemetryCard({
  telemetry,
  gitFormatStandard,
  engineeringFocus,
  isLoading = false,
  className = '',
}: GitTelemetryCardProps) {
  const { t } = useLocale();
  if (isLoading) {
    return (
      <section className={`${CARD_BASE} ${className}`.trim()}>
        <TelemetrySkeleton />
      </section>
    );
  }

  const working = telemetry?.CommitsInWorkingHours ?? 0;
  const off = telemetry?.CommitsInOffHours ?? 0;
  const total = working + off;
  const standard = normalizeFormatStandard(gitFormatStandard);
  const messages = readCommitMessages(telemetry);

  const hasSignal = total > 0 || messages.length > 0 || Boolean(gitFormatStandard?.trim());

  if (!hasSignal) {
    return null;
  }

  const focusText = engineeringFocus?.trim();

  return (
    <section className={`${CARD_BASE} ${className}`.trim()}>
      <header className="mb-3 flex shrink-0 items-start justify-between gap-3 border-b border-zinc-800/80 pb-3">
        <h3 className="text-[10px] font-semibold uppercase tracking-[0.14em] text-zinc-500">
          {t('telemetry.title')}
        </h3>
        <span
          className={`shrink-0 rounded px-2.5 py-1 text-[10px] font-medium uppercase tracking-wide ${badgeStyles(standard)}`}
        >
          {shortBadgeLabel(standard)}
        </span>
      </header>

      <div className="flex min-h-0 flex-1 flex-col">
        <p className="mb-2 shrink-0 text-[10px] font-medium uppercase tracking-wider text-zinc-500">
          {t('telemetry.commitSamples')}
          {messages.length > 0 ? (
            <span className="ml-1.5 tabular-nums text-zinc-600">({messages.length})</span>
          ) : null}
        </p>

        <div
          className={COMMIT_LIST_SCROLL}
          tabIndex={0}
          role="region"
          aria-label={t('telemetry.commitListAria')}
        >
          <ul className="space-y-2 pb-1 font-mono text-xs leading-relaxed text-zinc-300 sm:text-sm">
            {messages.map((line, index) => (
              <li
                key={`${index}-${line.slice(0, 40)}`}
                className="break-words rounded-md border border-zinc-700/80 bg-zinc-900/80 px-3 py-2 text-zinc-300"
                title={line}
              >
                {line}
              </li>
            ))}
          </ul>
        </div>
      </div>

      {focusText ? (
        <p className="mt-2 line-clamp-2 shrink-0 text-xs leading-relaxed text-zinc-500 lg:hidden">
          {focusText}
        </p>
      ) : null}
    </section>
  );
}
