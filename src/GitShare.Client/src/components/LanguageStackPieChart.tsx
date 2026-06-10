import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts';
import type { LanguageMetric } from '../types';
import { useLocale } from '../i18n/LocaleProvider';

interface LanguageStackPieChartProps {
  languages: LanguageMetric[];
  variant?: 'default' | 'compact';
  fillHeight?: boolean;
  embedded?: boolean;
  className?: string;
}

interface PieSlice {
  name: string;
  value: number;
  color: string;
  glow: string;
}

const PALETTE: Array<{ color: string; glow: string }> = [
  { color: '#38bdf8', glow: 'rgba(56, 189, 248, 0.35)' },
  { color: '#34d399', glow: 'rgba(52, 211, 153, 0.35)' },
  { color: '#a78bfa', glow: 'rgba(167, 139, 250, 0.35)' },
  { color: '#fbbf24', glow: 'rgba(251, 191, 36, 0.3)' },
  { color: '#fb7185', glow: 'rgba(251, 113, 133, 0.3)' },
  { color: '#2dd4bf', glow: 'rgba(45, 212, 191, 0.3)' },
];

const LANGUAGE_COLORS: Record<string, { color: string; glow: string }> = {
  JavaScript: { color: '#facc15', glow: 'rgba(250, 204, 21, 0.28)' },
  TypeScript: { color: '#3b82f6', glow: 'rgba(59, 130, 246, 0.32)' },
  'C#': { color: '#4ade80', glow: 'rgba(74, 222, 128, 0.32)' },
  Java: { color: '#f97316', glow: 'rgba(249, 115, 22, 0.28)' },
  Python: { color: '#60a5fa', glow: 'rgba(96, 165, 250, 0.32)' },
  Go: { color: '#22d3ee', glow: 'rgba(34, 211, 238, 0.28)' },
  Rust: { color: '#fb923c', glow: 'rgba(251, 146, 60, 0.28)' },
  Ruby: { color: '#f87171', glow: 'rgba(248, 113, 113, 0.28)' },
  PHP: { color: '#818cf8', glow: 'rgba(129, 140, 248, 0.28)' },
  Kotlin: { color: '#c084fc', glow: 'rgba(192, 132, 252, 0.28)' },
  Swift: { color: '#f43f5e', glow: 'rgba(244, 63, 94, 0.28)' },
  HTML: { color: '#f87171', glow: 'rgba(248, 113, 113, 0.25)' },
  CSS: { color: '#a78bfa', glow: 'rgba(167, 139, 250, 0.25)' },
  Shell: { color: '#86efac', glow: 'rgba(134, 239, 172, 0.25)' },
  C: { color: '#94a3b8', glow: 'rgba(148, 163, 184, 0.25)' },
  'C++': { color: '#f472b6', glow: 'rgba(244, 114, 182, 0.28)' },
};

const CHART_SURFACE = 'rgba(9, 9, 11, 0.96)';

function slugify(value: string): string {
  return value.replace(/\W+/g, '-').toLowerCase() || 'lang';
}

function paletteForLanguage(language: string, index: number): { color: string; glow: string } {
  const exact = LANGUAGE_COLORS[language];
  if (exact) {
    return exact;
  }

  const normalized = language.trim().toLowerCase();
  const matched = Object.entries(LANGUAGE_COLORS).find(
    ([key]) => key.toLowerCase() === normalized,
  );
  if (matched) {
    return matched[1];
  }

  return PALETTE[index % PALETTE.length];
}

function roundLanguagePercent(value: number): number {
  return Math.round(value * 10) / 10;
}

function formatLanguagePercent(value: number): string {
  const rounded = roundLanguagePercent(value);
  return Number.isInteger(rounded) ? String(rounded) : rounded.toFixed(1);
}

function buildSlices(languages: LanguageMetric[], otherLabel: string): PieSlice[] {
  const sorted = [...languages]
    .filter((item) => item.Percentage > 0)
    .sort((a, b) => b.Percentage - a.Percentage);

  if (sorted.length === 0) {
    return [];
  }

  const top = sorted.slice(0, 6);
  const rest = sorted.slice(6);
  const otherSum = rest.reduce((sum, item) => sum + item.Percentage, 0);

  const slices: PieSlice[] = top.map((item, index) => {
    const palette = paletteForLanguage(item.Language, index);
    return {
      name: item.Language,
      value: item.Percentage,
      color: palette.color,
      glow: palette.glow,
    };
  });

  if (otherSum > 0) {
    slices.push({
      name: otherLabel,
      value: roundLanguagePercent(otherSum),
      color: '#71717a',
      glow: 'rgba(113, 113, 122, 0.25)',
    });
  }

  return slices;
}

function LanguageTooltip({
  active,
  payload,
  reposShareLabel,
}: {
  active?: boolean;
  payload?: { payload: PieSlice }[];
  reposShareLabel: (value: number) => string;
}) {
  if (!active || !payload?.length) {
    return null;
  }

  const slice = payload[0].payload;

  return (
    <div
      className="rounded-xl border border-zinc-700/80 px-3 py-2 shadow-2xl backdrop-blur-sm"
      style={{ background: CHART_SURFACE }}
    >
      <p className="text-sm font-medium text-zinc-100">{slice.name}</p>
      <p className="mt-0.5 text-xs text-zinc-400">{reposShareLabel(slice.value)}</p>
    </div>
  );
}

function LegendChip({ slice, isPrimary }: { slice: PieSlice; isPrimary: boolean }) {
  return (
    <span className="inline-flex items-center gap-1.5 rounded-md border border-zinc-800/80 bg-zinc-950/50 px-2 py-1 text-[10px]">
      <span
        className="h-2 w-2 shrink-0 rounded-full"
        style={{ backgroundColor: slice.color, boxShadow: `0 0 8px ${slice.glow}` }}
      />
      <span className={isPrimary ? 'font-medium text-zinc-200' : 'text-zinc-400'}>{slice.name}</span>
      <span className="tabular-nums text-zinc-500">{formatLanguagePercent(slice.value)}%</span>
    </span>
  );
}

function LegendRow({ slice, isPrimary }: { slice: PieSlice; isPrimary: boolean }) {
  return (
    <li className="space-y-1">
      <div className="flex items-center justify-between gap-2 text-[11px]">
        <span className="flex min-w-0 items-center gap-2">
          <span
            className="h-2 w-2 shrink-0 rounded-full ring-2 ring-zinc-900"
            style={{ backgroundColor: slice.color, boxShadow: `0 0 10px ${slice.glow}` }}
          />
          <span className={`truncate ${isPrimary ? 'font-medium text-zinc-200' : 'text-zinc-400'}`}>
            {slice.name}
          </span>
        </span>
        <span className="shrink-0 tabular-nums text-zinc-500">{formatLanguagePercent(slice.value)}%</span>
      </div>
      <div className="h-1 overflow-hidden rounded-full bg-zinc-800/80">
        <div
          className="h-full rounded-full transition-all duration-500"
          style={{
            width: `${slice.value}%`,
            background: `linear-gradient(90deg, ${slice.color}cc, ${slice.color})`,
            boxShadow: `0 0 12px ${slice.glow}`,
          }}
        />
      </div>
    </li>
  );
}

function DonutChart({
  slices,
  primary,
  multiSlice,
  pieRadii,
  className = '',
  primaryLabel,
  reposShareLabel,
}: {
  slices: PieSlice[];
  primary: PieSlice;
  multiSlice: boolean;
  pieRadii: { innerRadius: number | string; outerRadius: number | string };
  className?: string;
  primaryLabel: string;
  reposShareLabel: (value: number) => string;
}) {
  return (
    <div className={`relative ${className}`.trim()}>
      <ResponsiveContainer width="100%" height="100%">
        <PieChart>
          <defs>
            {slices.map((slice) => {
              const id = slugify(slice.name);
              return (
                <linearGradient key={slice.name} id={`lang-grad-${id}`} x1="0" y1="0" x2="1" y2="1">
                  <stop offset="0%" stopColor={slice.color} stopOpacity={0.95} />
                  <stop offset="100%" stopColor={slice.color} stopOpacity={0.55} />
                </linearGradient>
              );
            })}
          </defs>
          <Pie
            data={slices}
            dataKey="value"
            nameKey="name"
            cx="50%"
            cy="50%"
            innerRadius={pieRadii.innerRadius}
            outerRadius={pieRadii.outerRadius}
            paddingAngle={multiSlice ? 3 : 0}
            stroke="#09090b"
            strokeWidth={multiSlice ? 2 : 0}
            cornerRadius={multiSlice ? 4 : 0}
            isAnimationActive
            animationDuration={700}
          >
            {slices.map((slice) => (
              <Cell key={slice.name} fill={`url(#lang-grad-${slugify(slice.name)})`} />
            ))}
          </Pie>
          <Tooltip content={<LanguageTooltip reposShareLabel={reposShareLabel} />} />
        </PieChart>
      </ResponsiveContainer>

      <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center text-center">
        <span className="max-w-[5.5rem] truncate text-sm font-semibold tracking-tight text-zinc-100">
          {primary.name}
        </span>
        <span
          className="mt-0.5 text-xl font-semibold tabular-nums leading-none"
          style={{ color: primary.color }}
        >
          {formatLanguagePercent(primary.value)}%
        </span>
        {multiSlice ? (
          <span className="mt-1 text-[9px] uppercase tracking-wider text-zinc-600">{primaryLabel}</span>
        ) : null}
      </div>
    </div>
  );
}

export function LanguageStackPieChart({
  languages,
  variant = 'default',
  fillHeight = false,
  embedded = false,
  className = '',
}: LanguageStackPieChartProps) {
  const { t, formatLanguageCount } = useLocale();
  const slices = buildSlices(languages, t('languages.other'));
  const isCompact = variant === 'compact';
  const primary = slices[0];
  const multiSlice = slices.length > 1;
  const useStackedLayout = fillHeight && embedded;
  const reposShareLabel = (value: number) =>
    t('languages.reposShare', { value: formatLanguagePercent(value) });
  const donutLabels = {
    primaryLabel: t('languages.primary'),
    reposShareLabel,
  };

  const splitChartSize = isCompact ? 'h-[7.25rem] w-[7.25rem]' : 'h-[8.75rem] w-[8.75rem]';
  const splitPieRadii = {
    innerRadius: isCompact ? 34 : 40,
    outerRadius: isCompact ? 52 : 62,
  };
  const stackedPieRadii = { innerRadius: '56%', outerRadius: '90%' };

  if (slices.length === 0) {
    return (
      <section
        className={`rounded-xl border border-zinc-800/80 bg-zinc-900/40 px-4 py-3 ${className}`.trim()}
      >
        <p className="text-[11px] text-zinc-500">{t('languages.empty')}</p>
      </section>
    );
  }

  return (
    <section
      className={`overflow-hidden transition-all duration-300 ${
        embedded
          ? 'flex min-h-0 flex-1 flex-col bg-transparent px-3 pb-3 pt-2'
          : `rounded-xl border border-zinc-800/80 bg-gradient-to-br from-zinc-900/70 via-zinc-950/80 to-zinc-950 px-4 py-3.5 shadow-[inset_0_1px_0_rgba(255,255,255,0.03)] ${
              fillHeight ? 'flex h-full min-h-0 flex-col' : ''
            }`
      } ${className}`.trim()}
    >
      <header className="mb-1.5 flex shrink-0 items-center justify-between gap-2">
          <h3 className="text-[10px] font-semibold uppercase tracking-[0.14em] text-zinc-500">
            {t('languages.title')}
          </h3>
          {primary ? (
            <span className="rounded-full border border-zinc-800 bg-zinc-900/80 px-2 py-0.5 text-[10px] tabular-nums text-zinc-500">
              {formatLanguageCount(slices.length)}
            </span>
          ) : null}
        </header>

      {useStackedLayout ? (
        <div className="grid min-h-0 flex-1 grid-rows-[auto_minmax(0,1fr)] gap-2 overflow-hidden">
          <div className="relative z-10 flex shrink-0 flex-wrap gap-1 content-start">
            {slices.map((slice, index) => (
              <LegendChip key={slice.name} slice={slice} isPrimary={index === 0} />
            ))}
          </div>

          <div className="relative z-0 min-h-0 overflow-hidden">
            <div className="flex h-full min-h-0 items-center justify-center">
              <div className="aspect-square h-[min(100%,16.25rem)] w-[min(100%,16.25rem)] max-h-full max-w-full min-h-0 min-w-0">
                <DonutChart
                  slices={slices}
                  primary={primary}
                  multiSlice={multiSlice}
                  pieRadii={stackedPieRadii}
                  className="h-full w-full"
                  {...donutLabels}
                />
              </div>
            </div>
          </div>
        </div>
      ) : (
        <div className={`flex min-h-0 items-center gap-4 ${fillHeight ? 'flex-1' : ''} ${isCompact ? '' : 'gap-5'}`}>
          <DonutChart
            slices={slices}
            primary={primary}
            multiSlice={multiSlice}
            pieRadii={fillHeight ? { innerRadius: '58%', outerRadius: '88%' } : splitPieRadii}
            className={
              fillHeight
                ? 'aspect-square h-full min-h-[7.25rem] w-full max-w-[11rem] shrink-0'
                : splitChartSize
            }
            {...donutLabels}
          />

          <ul className="min-w-0 flex-1 space-y-2.5">
            {multiSlice ? (
              slices.map((slice, index) => (
                <LegendRow key={slice.name} slice={slice} isPrimary={index === 0} />
              ))
            ) : (
              <li className="pt-1">
                <p className="text-xs leading-relaxed text-zinc-500">
                  {t('languages.singleLangNote')}
                </p>
              </li>
            )}
          </ul>
        </div>
      )}
    </section>
  );
}
