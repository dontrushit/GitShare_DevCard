import { HoverPortalPopover } from '../ui/HoverPortalPopover';
import { useLocale } from '../../i18n/LocaleProvider';

const SEVERITY_LADDER = [
  {
    code: 'CLEAN',
    label: 'Clean',
    dot: 'bg-emerald-500',
    badge: 'border-emerald-500/30 bg-emerald-500/10 text-emerald-300',
  },
  {
    code: 'Minor',
    label: 'Minor',
    dot: 'bg-emerald-500',
    badge: 'border-emerald-500/30 bg-emerald-500/10 text-emerald-300',
  },
  {
    code: 'Warning',
    label: 'Warning',
    dot: 'bg-amber-400',
    badge: 'border-amber-500/30 bg-amber-500/10 text-amber-200',
  },
  {
    code: 'Critical',
    label: 'Critical Risk',
    dot: 'bg-red-500',
    badge: 'border-red-500/30 bg-red-500/10 text-red-300',
  },
  {
    code: 'NONE',
    label: 'N/A',
    dot: 'bg-zinc-500',
    badge: 'border-zinc-600/40 bg-zinc-800/60 text-zinc-400',
  },
] as const;

function normalizeSeverity(severity: string): string {
  const trimmed = severity.trim();
  if (trimmed.toUpperCase() === 'CLEAN') {
    return 'CLEAN';
  }
  if (trimmed.toUpperCase() === 'NONE') {
    return 'NONE';
  }
  const match = SEVERITY_LADDER.find(
    (tier) => tier.code.toLowerCase() === trimmed.toLowerCase(),
  );
  return match?.code ?? 'Warning';
}

export function getSeverityBadgeStyle(severity: string) {
  const code = normalizeSeverity(severity);
  const tier = SEVERITY_LADDER.find((item) => item.code === code) ?? SEVERITY_LADDER[2];
  return { label: tier.label, dot: tier.dot, badge: tier.badge, code: tier.code };
}

interface DebtSeverityBadgeProps {
  severity: string;
  repoName: string;
  technicalDebt?: string;
}

export function DebtSeverityBadge({
  severity,
  repoName,
  technicalDebt,
}: DebtSeverityBadgeProps) {
  const { t } = useLocale();
  const normalized = normalizeSeverity(severity);
  const current = getSeverityBadgeStyle(normalized);
  const popoverId = `debt-severity-${repoName.replace(/\W+/g, '-')}`;

  return (
    <HoverPortalPopover
      id={popoverId}
      width={300}
      estimatedHeight={420}
      className="py-2.5"
      trigger={
        <span
          className={`inline-flex items-center gap-1 rounded border px-2 py-0.5 text-[9px] font-semibold uppercase tracking-wide ${current.badge}`}
        >
          <span className={`h-1.5 w-1.5 shrink-0 rounded-full ${current.dot}`} />
          {current.label}
        </span>
      }
    >
      <p className="px-3 pb-2 text-[10px] font-medium uppercase tracking-wider text-zinc-500">
        {t('audit.debtScale')}
      </p>
      <ol className="flex flex-col gap-1">
        {SEVERITY_LADDER.map((tier) => {
          const isCurrent = tier.code === normalized;
          const description = t(`audit.severity.${tier.code}`);
          return (
            <li
              key={tier.code}
              className={`px-3 py-2 ${isCurrent ? 'bg-zinc-800/80' : ''}`}
            >
              <div className="flex items-center gap-2">
                <span
                  className={`inline-flex shrink-0 items-center gap-1.5 rounded border px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide ${tier.badge} ${
                    isCurrent ? 'ring-1 ring-zinc-500' : 'opacity-70'
                  }`}
                >
                  <span className={`h-2 w-2 rounded-full ${tier.dot}`} />
                  {tier.label}
                </span>
              </div>
              <p className="mt-1.5 text-xs leading-relaxed text-zinc-400">{description}</p>
            </li>
          );
        })}
      </ol>
      {technicalDebt ? (
        <p className="mt-2 border-t border-zinc-800 px-3 pt-2.5 text-xs leading-relaxed text-zinc-400">
          <span className="font-medium text-zinc-200">{current.label} · {repoName}</span>
          <span className="mt-1 line-clamp-5 block text-zinc-500">{technicalDebt}</span>
        </p>
      ) : null}
    </HoverPortalPopover>
  );
}
