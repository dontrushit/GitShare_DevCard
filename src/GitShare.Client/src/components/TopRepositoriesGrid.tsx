import { ExternalLink, Star } from 'lucide-react';
import { useMemo } from 'react';
import { useLocale } from '../i18n/LocaleProvider';
import type { RepoSummary } from '../types';

interface TopRepositoriesGridProps {
  repositories: RepoSummary[];
}

interface GroupedRepository extends RepoSummary {
  OccurrenceCount: number;
}

function groupRepositories(repositories: RepoSummary[]): GroupedRepository[] {
  const groups = new Map<string, { repo: RepoSummary; count: number }>();

  for (const repo of repositories) {
    const key = repo.Name.trim().toLowerCase();
    const existing = groups.get(key);

    if (!existing) {
      groups.set(key, { repo, count: 1 });
      continue;
    }

    existing.count += 1;
    if (repo.Stars > existing.repo.Stars) {
      existing.repo = repo;
    }
  }

  return Array.from(groups.values()).map(({ repo, count }) => ({
    ...repo,
    OccurrenceCount: count,
  }));
}

export function TopRepositoriesGrid({ repositories }: TopRepositoriesGridProps) {
  const { t } = useLocale();
  const grouped = useMemo(
    () => groupRepositories(repositories),
    [repositories],
  );

  if (grouped.length === 0) {
    return null;
  }

  return (
    <section className="space-y-1.5">
      <h3 className="px-0.5 text-[11px] font-medium uppercase tracking-wide text-foreground/60">
        {t('repos.title')}
      </h3>
      <ul className="grid grid-cols-1 items-stretch gap-1.5 transition-all duration-300 sm:grid-cols-2 lg:grid-cols-2 xl:grid-cols-3">
        {grouped.map((repo) => (
          <li
            key={`${repo.Name}-${repo.Url}`}
            className="card-panel flex h-full min-h-[7.25rem] flex-col gap-1 p-2 transition-all duration-300 hover:-translate-y-0.5 hover:border-slate-600 active:-translate-y-0.5 active:border-slate-600"
          >
            <a
              href={repo.Url}
              target="_blank"
              rel="noreferrer"
              className="flex items-center gap-1 text-xs font-semibold text-slate-200 hover:text-slate-100 hover:underline"
            >
              <span className="truncate">{repo.Name}</span>
              {repo.OccurrenceCount > 1 ? (
                <span className="shrink-0 rounded bg-slate-800 px-1 py-0.5 text-[9px] font-medium text-slate-400">
                  (x{repo.OccurrenceCount})
                </span>
              ) : null}
              <ExternalLink className="ml-auto h-3 w-3 shrink-0" />
            </a>
            <p
              className={
                repo.Description
                  ? 'line-clamp-2 h-8 text-[10px] leading-snug text-foreground/55'
                  : 'line-clamp-2 h-8 text-sm italic text-slate-500'
              }
            >
              {repo.Description || t('repos.noDescription')}
            </p>
            <footer className="mt-auto flex items-center justify-between gap-1">
              <span className="inline-flex items-center gap-0.5 text-[10px] text-foreground/70">
                <Star className="h-3 w-3 text-slate-400" />
                {repo.Stars.toLocaleString()}
              </span>
              {repo.Language ? (
                <span className="rounded bg-slate-800 px-1.5 py-0.5 text-[9px] text-slate-500">
                  {repo.Language}
                </span>
              ) : null}
            </footer>
          </li>
        ))}
      </ul>
    </section>
  );
}
