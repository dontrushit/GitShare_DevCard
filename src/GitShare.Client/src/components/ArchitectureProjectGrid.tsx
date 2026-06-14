import { ChevronDown } from 'lucide-react';
import { useState } from 'react';
import { useLocale, useLevelTitle } from '../i18n/LocaleProvider';
import {
  emptyKeyFilesHint,
  formatFrameworkBadge,
  formatLayoutBadge,
  formatProjectClassLabel,
  layoutBadgeClass,
  projectClassBadgeClass,
  stackBadgeClassFor,
} from '../lib/auditLabels';
import type { ProjectAuditDetail, RepositoryLevelInfo } from '../types';

const REPO_LEVEL_STYLES: Record<string, string> = {
  trainee: 'border-zinc-600 bg-zinc-800/80 text-zinc-300',
  junior: 'border-sky-700/60 bg-sky-950/50 text-sky-300',
  middle: 'border-violet-700/60 bg-violet-950/40 text-violet-300',
  senior: 'border-amber-700/60 bg-amber-950/40 text-amber-300',
};

function fileMicroIcon(fileName: string): string {
  const lower = fileName.toLowerCase();
  if (lower.endsWith('.cs') || lower.endsWith('.tsx') || lower.endsWith('.ts')) {
    return '</>';
  }
  if (
    lower.includes('config') ||
    lower.endsWith('.json') ||
    lower.endsWith('.config') ||
    lower.includes('appsettings')
  ) {
    return '{}';
  }
  if (lower.endsWith('.sql')) {
    return '§';
  }
  if (lower.endsWith('.xaml')) {
    return '◇';
  }
  return '·';
}

function RepoLevelBadge({ level }: { level: RepositoryLevelInfo }) {
  const levelTitle = useLevelTitle(level.Code, level.Title);
  const style = REPO_LEVEL_STYLES[level.Code] ?? REPO_LEVEL_STYLES.junior;

  return (
    <span
      className={`inline-flex shrink-0 items-center gap-1 rounded-md border px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide ${style}`}
      title={level.Rationale ?? `${level.Score}/100`}
    >
      {levelTitle}
      <span className="font-normal normal-case opacity-80">{level.Score}</span>
    </span>
  );
}

function ArchitectureAssessmentPanel({ project }: { project: ProjectAuditDetail }) {
  const { t } = useLocale();
  const summary = project.ArchitectureSummary?.trim();
  const strengths = project.Pros ?? [];
  const risks = project.Cons ?? [];

  return (
    <div className="space-y-3">
      {summary ? (
        <p className="rounded-lg border border-zinc-800 bg-zinc-950/50 px-3 py-3 text-xs leading-relaxed text-zinc-300">
          {summary}
        </p>
      ) : null}

      {strengths.length === 0 && risks.length === 0 ? (
        <p className="rounded-lg border border-dashed border-zinc-800 px-3 py-4 text-center text-xs text-zinc-600">
          {t('architecture.assessmentMissing')}
        </p>
      ) : (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <div className="rounded-lg border border-zinc-800 bg-zinc-950/40 p-3">
            <h5 className="mb-2.5 text-[10px] font-semibold uppercase tracking-[0.12em] text-emerald-400">
              {t('architecture.strengths')}
            </h5>
            <ul className="space-y-2 text-xs leading-relaxed text-zinc-400">
              {strengths.length > 0 ? (
                strengths.map((item) => (
                  <li key={item} className="flex gap-2 break-words">
                    <span className="mt-1.5 h-1 w-1 shrink-0 rounded-full bg-emerald-500/70" aria-hidden />
                    <span>{item}</span>
                  </li>
                ))
              ) : (
                <li className="text-zinc-600">—</li>
              )}
            </ul>
          </div>

          <div className="rounded-lg border border-zinc-800 bg-zinc-950/40 p-3">
            <h5 className="mb-2.5 text-[10px] font-semibold uppercase tracking-[0.12em] text-rose-400">
              {t('architecture.risks')}
            </h5>
            <ul className="space-y-2 text-xs leading-relaxed text-zinc-400">
              {risks.length > 0 ? (
                risks.map((item) => (
                  <li key={item} className="flex gap-2 break-words">
                    <span className="mt-1.5 h-1 w-1 shrink-0 rounded-full bg-rose-500/70" aria-hidden />
                    <span>{item}</span>
                  </li>
                ))
              ) : (
                <li className="text-zinc-500">{t('architecture.noConfirmedRisks')}</li>
              )}
            </ul>
          </div>
        </div>
      )}
    </div>
  );
}

function ArchitectureProjectCard({
  project,
  isExpanded,
  onToggle,
}: {
  project: ProjectAuditDetail;
  isExpanded: boolean;
  onToggle: () => void;
}) {
  const { t, locale } = useLocale();
  const basename = (path: string) => path.split(/[/\\]/).pop() ?? path;
  const frameworkLabel = formatFrameworkBadge(project.Framework, project.ProjectClass, locale);
  const layoutLabel = formatLayoutBadge(project.LayoutType, project.ProjectClass, locale);
  const classLabel = formatProjectClassLabel(project.ProjectClass, locale, project.Framework);

  return (
    <article
      className={`min-w-0 rounded-xl border bg-zinc-900/40 transition-colors duration-200 ${
        isExpanded
          ? 'border-zinc-600'
          : 'cursor-pointer border-zinc-800 hover:border-zinc-700'
      }`}
    >
      <button
        type="button"
        onClick={onToggle}
        className="flex w-full cursor-pointer flex-col p-4 text-left transition-colors"
        aria-expanded={isExpanded}
      >
        <header className="min-w-0">
          <div className="flex min-w-0 items-center justify-between gap-3">
            <h4 className="min-w-0 flex-1 truncate text-sm font-semibold tracking-wide text-zinc-200">
              {project.RepoName}
            </h4>
            <div className="flex shrink-0 items-center gap-2">
              {project.RepositoryLevel ? (
                <RepoLevelBadge level={project.RepositoryLevel} />
              ) : null}
              <ChevronDown
                className={`h-4 w-4 shrink-0 text-zinc-500 transition-transform duration-200 ${
                  isExpanded ? 'rotate-180' : ''
                }`}
                aria-hidden
              />
            </div>
          </div>
          <div className="mt-2.5 flex min-w-0 flex-wrap items-center gap-1.5">
            <span className={stackBadgeClassFor(frameworkLabel)} title={frameworkLabel}>
              {frameworkLabel}
            </span>
            {classLabel ? (
              <span
                className={projectClassBadgeClass(project.ProjectClass)}
                title={project.ProjectClass}
              >
                {classLabel}
              </span>
            ) : null}
            <span className={layoutBadgeClass} title={layoutLabel}>
              {layoutLabel}
            </span>
          </div>
        </header>

        <div className="mt-3 border-t border-zinc-800/80 pt-3">
          <span className="mb-2 block text-[10px] font-medium uppercase tracking-wider text-zinc-500">
            {t('architecture.keyComponents')}
          </span>
          {project.KeyFiles.length > 0 ? (
            <div className="flex min-w-0 flex-wrap gap-1.5">
              {project.KeyFiles.map((file) => {
                const shortName = basename(file);
                return (
                  <span
                    key={file}
                    title={file}
                    className="inline-flex max-w-full items-center gap-1 rounded-md border border-zinc-700/50 bg-zinc-800/50 px-2 py-0.5 font-mono text-[11px] text-zinc-400"
                  >
                    <span className="shrink-0 text-[9px] font-bold text-zinc-500" aria-hidden>
                      {fileMicroIcon(shortName)}
                    </span>
                    <span className="truncate">{shortName}</span>
                  </span>
                );
              })}
            </div>
          ) : (
            <p className="rounded-md border border-dashed border-zinc-800 bg-zinc-950/40 px-3 py-2 text-center text-xs leading-relaxed text-zinc-500">
              {emptyKeyFilesHint(project, locale)}
            </p>
          )}
        </div>
      </button>

      {isExpanded ? (
        <div className="border-t border-zinc-800 px-4 pb-4 pt-3">
          <ArchitectureAssessmentPanel project={project} />
        </div>
      ) : null}
    </article>
  );
}

interface ArchitectureProjectGridProps {
  projects: ProjectAuditDetail[];
}

export function ArchitectureProjectGrid({ projects }: ArchitectureProjectGridProps) {
  const [expandedRepo, setExpandedRepo] = useState<string | null>(null);

  return (
    <div className="grid min-w-0 grid-cols-1 gap-4">
      {projects.map((project) => {
        const isExpanded = expandedRepo === project.RepoName;
        return (
          <ArchitectureProjectCard
            key={project.RepoName}
            project={project}
            isExpanded={isExpanded}
            onToggle={() =>
              setExpandedRepo(isExpanded ? null : project.RepoName)
            }
          />
        );
      })}
    </div>
  );
}
