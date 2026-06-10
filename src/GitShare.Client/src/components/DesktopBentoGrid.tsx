import type { ReactNode } from 'react';
import { ArchitectureProjectGrid } from './ArchitectureProjectGrid';
import { DebtPanel, InterviewPanel } from './audit/AuditPanels';
import { LanguageStackPieChart } from './LanguageStackPieChart';
import { ProfileCard } from './ProfileCard';
import { useLocale } from '../i18n/LocaleProvider';
import type { DevCardProfile, StructuredAuditResponse } from '../types';

const BENTO_GRID =
  'hidden min-h-0 w-full lg:col-span-12 lg:row-start-2 lg:row-end-3 lg:grid lg:grid-cols-3 lg:gap-4 lg:overflow-hidden';

const PANEL_SHELL =
  'flex min-h-0 w-full min-w-0 flex-col overflow-hidden rounded-xl border border-zinc-800 bg-zinc-900/40';

interface DesktopBentoGridProps {
  profile?: DevCardProfile;
  auditData: StructuredAuditResponse | null;
  isLoading?: boolean;
}

function DesktopBentoSkeleton() {
  return (
    <div className={BENTO_GRID} aria-busy="true">
      <aside className="flex min-h-0 flex-col overflow-hidden rounded-xl border border-zinc-800 bg-zinc-900/40">
        <div className="shrink-0 w-full animate-pulse bg-zinc-900/20 py-32" />
        <div className="min-h-0 flex-1 w-full animate-pulse bg-zinc-900/20" />
      </aside>
      <section className="flex min-h-0 flex-col gap-3">
        <div className="h-4 w-2/5 animate-pulse rounded bg-zinc-800" />
        <div className="min-h-0 flex-1 animate-pulse rounded-xl border border-zinc-800 bg-zinc-900/40" />
      </section>
      <section className="flex min-h-0 flex-col gap-4">
        <div className="h-[calc(50%-0.5rem)] animate-pulse rounded-xl border border-zinc-800 bg-zinc-900/40" />
        <div className="h-[calc(50%-0.5rem)] animate-pulse rounded-xl border border-zinc-800 bg-zinc-900/40" />
      </section>
    </div>
  );
}

function PanelTitle({ children }: { children: ReactNode }) {
  return (
    <h3 className="shrink-0 border-b border-zinc-800/80 px-5 py-3.5 text-[10px] font-semibold uppercase tracking-[0.12em] text-zinc-500">
      {children}
    </h3>
  );
}

export function DesktopBentoGrid({ profile, auditData, isLoading = false }: DesktopBentoGridProps) {
  const { t } = useLocale();
  const projects = auditData?.Projects ?? [];
  const hasProjects = projects.length > 0;

  if (!isLoading && !profile) {
    return null;
  }

  if (isLoading) {
    return <DesktopBentoSkeleton />;
  }

  if (!profile) {
    return null;
  }

  const hasLanguages = profile.LanguageStack.length > 0;

  return (
    <div className={BENTO_GRID}>
      <aside className="flex min-h-0 min-w-0 flex-col overflow-hidden rounded-xl border border-zinc-800 bg-zinc-900/40">
        <ProfileCard profile={profile} variant="desktop" embedded className="shrink-0" />
        {hasLanguages ? (
          <>
            <div className="mx-3 border-t border-zinc-800/80" aria-hidden />
            <LanguageStackPieChart
              languages={profile.LanguageStack}
              variant="compact"
              fillHeight
              embedded
              className="min-h-0 flex-1"
            />
          </>
        ) : null}
      </aside>

      {hasProjects ? (
        <>
          <section className={`${PANEL_SHELL} min-h-0`}>
            <PanelTitle>{t('desktop.architecturePassport')}</PanelTitle>
            <div className="custom-scrollbar min-h-0 flex-1 overflow-y-auto px-5 pb-5">
              <ArchitectureProjectGrid projects={projects} />
            </div>
          </section>

          <section className="grid min-h-0 min-w-0 grid-rows-2 gap-4">
            <div className={`${PANEL_SHELL} min-h-0`}>
              <PanelTitle>{t('desktop.architectureQuality')}</PanelTitle>
              <div className="custom-scrollbar min-h-0 flex-1 overflow-y-auto px-5 pb-4 pt-1">
                <DebtPanel projects={projects} />
              </div>
            </div>

            <div className={`${PANEL_SHELL} min-h-0`}>
              <PanelTitle>{t('desktop.interviewQuestions')}</PanelTitle>
              <div className="custom-scrollbar min-h-0 flex-1 overflow-y-auto px-5 pb-4 pt-1">
                <InterviewPanel projects={projects} />
              </div>
            </div>
          </section>
        </>
      ) : null}
    </div>
  );
}
