import { Brain } from 'lucide-react';
import { useMemo, useState } from 'react';
import { ArchitectureProjectGrid } from './ArchitectureProjectGrid';
import { DebtPanel, InterviewPanel } from './audit/AuditPanels';
import { useLocale } from '../i18n/LocaleProvider';
import type { StructuredAuditResponse } from '../types';

interface StructuredAuditCardProps {
  auditData: StructuredAuditResponse | null;
  isLoading?: boolean;
}

function AuditSkeletonMobile({ tabCount }: { tabCount: number }) {
  return (
    <div className="space-y-2" aria-busy="true">
      <div className="flex gap-1 border-b border-slate-800 pb-1">
        {Array.from({ length: tabCount }, (_, id) => (
          <div key={id} className="h-7 flex-1 animate-pulse rounded bg-slate-800/60" />
        ))}
      </div>
      {[0, 1].map((i) => (
        <article
          key={i}
          className="animate-pulse rounded-lg border border-slate-800 bg-card p-4"
        >
          <div className="mb-2 h-3.5 w-2/5 rounded bg-slate-800" />
          <div className="mb-3 flex gap-1.5">
            <div className="h-4 w-16 rounded-full bg-slate-700/80" />
            <div className="h-4 w-20 rounded-full bg-slate-800/60" />
          </div>
          <div className="flex flex-wrap gap-1.5">
            <div className="h-5 w-24 rounded bg-slate-900/80" />
            <div className="h-5 w-20 rounded bg-slate-900/80" />
          </div>
        </article>
      ))}
    </div>
  );
}

export function StructuredAuditCard({ auditData, isLoading = false }: StructuredAuditCardProps) {
  const { t } = useLocale();
  const [activeTab, setActiveTab] = useState(0);
  const tabs = useMemo(
    () => [
      { id: 0, label: t('audit.architectureTab') },
      { id: 1, label: t('audit.debtTab') },
      { id: 2, label: t('audit.interviewTab') },
    ],
    [t],
  );
  const projects = auditData?.Projects ?? [];
  const hasData = projects.length > 0;

  if (!isLoading && !hasData) {
    return null;
  }

  return (
    <section className="card-panel min-w-0 p-3 transition-all duration-300 lg:hidden">
      <header className="mb-2.5 flex items-center gap-2">
        <Brain
          className={`h-4 w-4 shrink-0 text-accent-muted ${isLoading ? 'animate-pulse' : ''}`}
        />
        <div className="min-w-0">
          <h3 className="text-xs font-semibold tracking-wide text-slate-200">
            {isLoading ? t('audit.scanning') : t('audit.profileTitle')}
          </h3>
          {!isLoading && auditData?.CoreEngineeringFocus && (
            <p className="mt-0.5 text-[10px] leading-snug text-slate-500">
              {auditData.CoreEngineeringFocus}
            </p>
          )}
        </div>
      </header>

      {isLoading || !hasData ? (
        <AuditSkeletonMobile tabCount={tabs.length} />
      ) : (
        <>
          <div className="mb-2 flex gap-0.5 border-b border-slate-800">
            {tabs.map((tab) => {
              const isActive = activeTab === tab.id;
              return (
                <button
                  key={tab.id}
                  type="button"
                  onClick={() => setActiveTab(tab.id)}
                  className={`flex-1 px-1 py-2 text-[10px] font-medium transition-all duration-300 ${
                    isActive
                      ? 'border-b-2 border-slate-400 text-slate-200'
                      : 'text-slate-500 hover:text-slate-300'
                  }`}
                >
                  {tab.label}
                </button>
              );
            })}
          </div>

          {activeTab === 0 && <ArchitectureProjectGrid projects={projects} />}
          {activeTab === 1 && <DebtPanel projects={projects} />}
          {activeTab === 2 && <InterviewPanel projects={projects} />}
        </>
      )}
    </section>
  );
}
