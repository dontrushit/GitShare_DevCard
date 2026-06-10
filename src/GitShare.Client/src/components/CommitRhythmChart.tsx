import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import type { HourlyActivity } from '../types';
import { useLocale } from '../i18n/LocaleProvider';

interface CommitRhythmChartProps {
  rhythm: HourlyActivity[];
}

interface ChartPoint {
  Hour: number;
  CommitCount: number;
}

const CHART_ACCENT = '#94a3b8';
const CHART_SURFACE = 'rgba(15, 23, 42, 0.92)';

const KEY_HOUR_TICKS = [0, 4, 8, 12, 16, 20];

function formatHourTick(hour: number): string {
  return `${hour.toString().padStart(2, '0')}:00`;
}

function buildChartData(rhythm: HourlyActivity[]): ChartPoint[] {
  const byHour = new Map(rhythm.map((item) => [item.Hour, item.CommitCount]));

  return Array.from({ length: 24 }, (_, hour) => ({
    Hour: hour,
    CommitCount: byHour.get(hour) ?? 0,
  }));
}

function RhythmTooltip({
  active,
  payload,
  totalEventsLabel,
  activityLabel,
}: {
  active?: boolean;
  payload?: { payload: ChartPoint }[];
  totalEventsLabel: (count: number) => string;
  activityLabel: (count: number) => string;
}) {
  if (!active || !payload?.length) {
    return null;
  }

  const point = payload[0].payload;
  const hourLabel = formatHourTick(point.Hour);

  return (
    <div
      className="rounded-xl border border-slate-700 p-4 shadow-lg"
      style={{
        background: CHART_SURFACE,
        backdropFilter: 'blur(12px)',
        WebkitBackdropFilter: 'blur(12px)',
      }}
    >
      <p className="text-sm font-bold text-slate-200">{hourLabel}</p>
      <p className="mt-2 text-xs text-slate-500">{totalEventsLabel(point.CommitCount)}</p>
      <p className="mt-1 flex items-center gap-1.5 text-xs text-slate-400">
        <span
          className="inline-block h-2 w-2 shrink-0 rounded-full"
          style={{ backgroundColor: CHART_ACCENT }}
        />
        {activityLabel(point.CommitCount)}
      </p>
    </div>
  );
}

export function CommitRhythmChart({ rhythm }: CommitRhythmChartProps) {
  const { t } = useLocale();
  const data = buildChartData(rhythm);
  const rhythmTooltip = {
    totalEventsLabel: (count: number) => t('rhythm.totalEvents', { count }),
    activityLabel: (count: number) => t('rhythm.activity', { count }),
  };

  return (
    <section className="card-panel px-2 py-2 transition-all duration-300 lg:px-3 lg:py-3">
      <h3 className="mb-1 px-1 text-[11px] font-medium uppercase tracking-wide text-slate-500">
        {t('rhythm.title')}
      </h3>
      <div className="h-[160px] w-full transition-all duration-300 lg:h-[200px]">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart
            data={data}
            margin={{ top: 20, right: 15, left: -20, bottom: 0 }}
            barCategoryGap="12%"
          >
            <CartesianGrid
              strokeDasharray="3 3"
              stroke="#334155"
              opacity={0.4}
              vertical={false}
            />
            <XAxis
              dataKey="Hour"
              type="number"
              domain={[0, 23]}
              ticks={KEY_HOUR_TICKS}
              tickFormatter={formatHourTick}
              tick={{ fill: '#64748b', fontSize: 9 }}
              axisLine={false}
              tickLine={false}
              padding={{ left: 12, right: 12 }}
            />
            <YAxis
              allowDecimals={false}
              tick={{ fill: '#64748b', fontSize: 9 }}
              axisLine={false}
              tickLine={false}
              width={28}
            />
            <Tooltip
              content={<RhythmTooltip {...rhythmTooltip} />}
              cursor={{ fill: 'rgba(148, 163, 184, 0.08)' }}
            />
            <Bar
              dataKey="CommitCount"
              fill={CHART_ACCENT}
              radius={[3, 3, 0, 0]}
              maxBarSize={12}
            />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </section>
  );
}
