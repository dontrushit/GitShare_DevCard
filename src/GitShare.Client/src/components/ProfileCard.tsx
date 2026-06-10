import { FolderGit2, MapPin, Star } from 'lucide-react';
import type { DevCardProfile } from '../types';
import { useLocale, useLevelTitle } from '../i18n/LocaleProvider';
import { ProfileAnalysisDisclaimer } from './ProfileAnalysisDisclaimer';
import { HoverPortalPopover } from './ui/HoverPortalPopover';

interface ProfileCardProps {
  profile: DevCardProfile;
  variant?: 'mobile' | 'desktop';
  className?: string;
  embedded?: boolean;
}

const LEVEL_STYLES: Record<string, string> = {
  trainee: 'border-zinc-700 bg-zinc-800/80 text-zinc-400',
  junior: 'border-sky-800/60 bg-sky-950/50 text-sky-300',
  middle: 'border-emerald-800/60 bg-emerald-950/40 text-emerald-300',
  senior: 'border-violet-800/60 bg-violet-950/40 text-violet-300',
  lead: 'border-amber-800/60 bg-amber-950/40 text-amber-300',
  principal: 'border-rose-800/60 bg-rose-950/40 text-rose-200',
};

const PROGRAMMER_LEVEL_LADDER = [
  { code: 'trainee', range: '< 20' },
  { code: 'junior', range: '20–37' },
  { code: 'middle', range: '38–54' },
  { code: 'senior', range: '55–71' },
  { code: 'lead', range: '72–87' },
  { code: 'principal', range: '88+' },
] as const;

function LevelAssessmentSummary({
  profile,
  className = '',
}: {
  profile: DevCardProfile;
  className?: string;
}) {
  const { t } = useLocale();
  const summary = profile.ProgrammerLevel?.AssessmentSummary?.trim();
  if (!summary) {
    return null;
  }

  return (
    <p className={className}>
      <span className="sr-only">{t('profile.assessmentSummary')}: </span>
      {summary}
    </p>
  );
}

function ProgrammerLevelBadge({
  profile,
  size = 'default',
}: {
  profile: DevCardProfile;
  size?: 'default' | 'lg';
}) {
  const { t } = useLocale();
  const level = profile.ProgrammerLevel;
  const levelTitle = useLevelTitle(level?.Code, level?.Title);
  if (!level?.Title) {
    return null;
  }
  const style = LEVEL_STYLES[level.Code] ?? LEVEL_STYLES.junior;
  const confidencePart =
    level.IsLowConfidence && level.SignalConfidence != null
      ? t('profile.lowConfidenceSuffix', { value: level.SignalConfidence.toFixed(2) })
      : '';
  const scorePart =
    level.RawScore != null && level.RawScore !== level.Score
      ? t('profile.rawScore', { score: level.Score, raw: level.RawScore })
      : t('profile.score', { score: level.Score });
  const ladderId = `level-ladder-${profile.Username}`;

  const isLarge = size === 'lg';

  return (
    <HoverPortalPopover
      id={ladderId}
      width={208}
      estimatedHeight={300}
      trigger={
        <span
          className={`inline-flex shrink-0 items-center gap-1 border font-semibold uppercase tracking-wide ${style} ${
            isLarge
              ? 'rounded-lg px-3 py-1 text-sm'
              : 'rounded-md px-2 py-0.5 text-[11px]'
          }`}
        >
          {levelTitle}
          {level.IsLowConfidence ? (
            <span
              className="normal-case font-normal opacity-75"
              aria-label={t('profile.lowConfidence')}
            >
              ~
            </span>
          ) : null}
        </span>
      }
    >
      <p className="px-2.5 pb-1 text-[9px] font-medium uppercase tracking-wider text-zinc-500">
        {t('profile.levelLadder')}
      </p>
      <ol className="flex flex-col gap-0.5">
        {PROGRAMMER_LEVEL_LADDER.map((tier) => {
          const isCurrent = tier.code === level.Code;
          const tierStyle = LEVEL_STYLES[tier.code];
          return (
            <li
              key={tier.code}
              className={`flex items-center justify-between gap-2 px-2.5 py-1 text-[11px] ${
                isCurrent ? 'bg-zinc-800/80' : ''
              }`}
            >
              <span
                className={`rounded border px-1.5 py-px font-semibold uppercase tracking-wide ${tierStyle} ${
                  isCurrent ? 'ring-1 ring-zinc-500' : 'opacity-70'
                }`}
              >
                {t(`levels.${tier.code}`)}
              </span>
              <span className="shrink-0 tabular-nums text-[10px] text-zinc-500">{tier.range}</span>
            </li>
          );
        })}
      </ol>
      <p className="mt-1.5 border-t border-zinc-800 px-2.5 pt-1.5 text-[10px] leading-snug text-zinc-400">
        <span className="font-medium text-zinc-300">
          {levelTitle} · {scorePart}
          {confidencePart}
        </span>
        {(level.AssessmentSummary?.trim() || level.Rationale) ? (
          <span className="mt-0.5 block text-zinc-500 leading-snug">
            {level.AssessmentSummary?.trim() || level.Rationale}
          </span>
        ) : null}
      </p>
    </HoverPortalPopover>
  );
}

function StatTile({
  label,
  value,
  compact = false,
}: {
  label: string;
  value: string | number;
  compact?: boolean;
}) {
  if (compact) {
    return (
      <div className="rounded-md border border-zinc-800/80 bg-zinc-950/40 px-2.5 py-2">
        <p className="text-[10px] font-medium uppercase leading-tight tracking-wide text-zinc-500">
          {label}
        </p>
        <p className="mt-0.5 text-sm font-semibold tabular-nums text-zinc-100">{value}</p>
      </div>
    );
  }

  return (
    <div className="rounded-lg border border-zinc-800 bg-zinc-950/50 px-3 py-2.5">
      <p className="text-[10px] uppercase tracking-wide text-zinc-600">{label}</p>
      <p className="mt-0.5 text-sm font-medium text-zinc-200">{value}</p>
    </div>
  );
}

export function ProfileCard({
  profile,
  variant = 'mobile',
  className = '',
  embedded = false,
}: ProfileCardProps) {
  const ownPercent = Math.round(profile.ContributionRatio * 100);
  const isDesktop = variant === 'desktop';
  const prominentHeader = isDesktop && embedded;
  const { t } = useLocale();

  if (!isDesktop) {
    return (
      <section className="card-panel flex gap-3 p-3 transition-all duration-300">
        <img
          src={profile.AvatarUrl}
          alt={profile.Username}
          className="h-14 w-14 shrink-0 rounded-full border border-slate-700 object-cover"
        />
        <section className="min-w-0 flex-1">
          <div className="flex min-w-0 items-center gap-2">
            <h2 className="truncate text-base font-semibold text-foreground">{profile.Username}</h2>
            <ProgrammerLevelBadge profile={profile} />
          </div>
          {profile.Bio ? (
            <p className="mt-0.5 line-clamp-2 text-xs text-foreground/70">{profile.Bio}</p>
          ) : null}
          <LevelAssessmentSummary
            profile={profile}
            className="mt-1.5 text-xs leading-relaxed text-foreground/80"
          />
          {profile.Location ? (
            <p className="mt-1 flex items-center gap-1 text-xs text-foreground/60">
              <MapPin className="h-3 w-3 shrink-0" />
              <span className="truncate">{profile.Location}</span>
            </p>
          ) : null}
          <ProfileAnalysisDisclaimer compact />
          <p className="mt-2 flex flex-wrap gap-1.5">
            <span className="inline-flex items-center gap-1 rounded-md bg-slate-800/80 px-2 py-0.5 text-[11px] text-slate-300">
              <FolderGit2 className="h-3 w-3 text-slate-400" />
              Repos: {profile.PublicRepos}
            </span>
            <span className="inline-flex items-center gap-1 rounded-md bg-slate-800/80 px-2 py-0.5 text-[11px] text-slate-300">
              <Star className="h-3 w-3 text-slate-400" />
              Stars: {profile.TotalStars.toLocaleString()}
            </span>
          </p>
        </section>
      </section>
    );
  }

  return (
    <section
      className={`flex w-full min-w-0 flex-col ${
        embedded
          ? 'shrink-0 bg-transparent p-3 pb-2'
          : 'h-full rounded-xl border border-zinc-800 bg-zinc-900/40 p-4'
      } ${className}`.trim()}
    >
      <div
        className={`flex shrink-0 border-b border-zinc-800/80 ${
          prominentHeader ? 'gap-4 pb-3' : 'gap-4 pb-3'
        }`}
      >
        <img
          src={profile.AvatarUrl}
          alt={profile.Username}
          className={`shrink-0 rounded-full border border-zinc-700 object-cover ${
            prominentHeader ? 'h-20 w-20' : 'h-16 w-16'
          }`}
        />
        <div className="min-w-0 flex-1">
          <div
            className={`flex min-w-0 flex-wrap items-center ${
              prominentHeader ? 'gap-2.5' : 'gap-2'
            }`}
          >
            <h2
              className={`truncate font-semibold text-zinc-100 ${
                prominentHeader ? 'text-xl' : 'text-lg'
              }`}
            >
              {profile.Username}
            </h2>
            <ProgrammerLevelBadge profile={profile} size={prominentHeader ? 'lg' : 'default'} />
          </div>
          {profile.Bio ? (
            <p
              className={`mt-0.5 line-clamp-1 leading-snug text-zinc-400 ${
                prominentHeader ? 'text-sm' : 'text-xs'
              }`}
            >
              {profile.Bio}
            </p>
          ) : null}
          <LevelAssessmentSummary
            profile={profile}
            className={`mt-1.5 leading-relaxed text-zinc-300 ${
              prominentHeader ? 'text-sm' : 'text-xs'
            }`}
          />
          {profile.Location ? (
            <p
              className={`mt-1 flex items-center gap-1 text-zinc-500 ${
                prominentHeader ? 'text-sm' : 'text-xs'
              }`}
            >
              <MapPin className={`shrink-0 ${prominentHeader ? 'h-3.5 w-3.5' : 'h-3 w-3'}`} />
              <span className="truncate">{profile.Location}</span>
            </p>
          ) : null}
          <ProfileAnalysisDisclaimer dense prominent={prominentHeader} />
        </div>
      </div>

      <div className="mt-2.5 grid shrink-0 grid-cols-2 grid-rows-3 gap-1.5">
        <StatTile compact label={t('profile.stats.repos')} value={profile.PublicRepos} />
        <StatTile compact label={t('profile.stats.stars')} value={profile.TotalStars.toLocaleString()} />
        <StatTile compact label={t('profile.stats.totalForks')} value={profile.TotalForks.toLocaleString()} />
        <StatTile compact label={t('profile.stats.largeProjects')} value={profile.ProductionScaleProjects} />
        <StatTile
          compact
          label={t('profile.stats.petMed')}
          value={`${profile.SmallPetProjects} / ${profile.MediumProjects}`}
        />
        <StatTile
          compact
          label={t('profile.stats.ownShare', { percent: ownPercent })}
          value={t('profile.stats.ownForkValue', {
            own: profile.OwnRepositoryCount,
            forks: profile.ForkedRepositoryCount,
          })}
        />
      </div>
    </section>
  );
}
