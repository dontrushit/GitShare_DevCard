import { Check, Copy } from 'lucide-react';

import { useCallback, useState, type ReactNode } from 'react';

import { useLocale } from '../../i18n/LocaleProvider';
import type { ProjectAuditDetail } from '../../types';
import { DebtSeverityBadge } from './DebtSeverityBadge';



const LIST_CLASS = 'min-w-0 divide-y divide-zinc-800/80';

const ITEM_CLASS = 'py-3 first:pt-0 last:pb-0';

const REPO_TITLE_CLASS = 'truncate text-sm font-semibold text-zinc-100';

const BODY_CLASS = 'text-xs leading-relaxed text-zinc-500';



function RepoHeader({

  repoName,

  trailing,

}: {

  repoName: string;

  trailing: ReactNode;

}) {

  return (

    <div className="mb-2 flex items-center justify-between gap-3">

      <h4 className={REPO_TITLE_CLASS}>{repoName}</h4>

      <div className="shrink-0">{trailing}</div>

    </div>

  );

}



export function DebtPanel({ projects }: { projects: ProjectAuditDetail[] }) {

  return (

    <div className={LIST_CLASS}>

      {projects.map((project) => {

        const severity = project.DebtSeverity || 'Warning';

        return (

          <article key={project.RepoName} className={ITEM_CLASS}>

            <RepoHeader

              repoName={project.RepoName}

              trailing={

                <DebtSeverityBadge

                  severity={severity}

                  repoName={project.RepoName}

                  technicalDebt={project.TechnicalDebt}

                />

              }

            />

            <p className={BODY_CLASS}>{project.TechnicalDebt}</p>

          </article>

        );

      })}

    </div>

  );

}



function CopyTrapButton({ text }: { text: string }) {

  const { t } = useLocale();
  const [copied, setCopied] = useState(false);



  const copyQuestion = useCallback(async () => {

    try {

      await navigator.clipboard.writeText(text);

      setCopied(true);

      window.setTimeout(() => setCopied(false), 1500);

    } catch {

      /* clipboard unavailable */

    }

  }, [text]);



  return (

    <button

      type="button"

      onClick={() => void copyQuestion()}

      className="inline-flex items-center gap-1 rounded border border-zinc-700 bg-zinc-800/60 px-2 py-0.5 text-[10px] font-medium text-zinc-300 transition-colors hover:border-zinc-600 hover:text-zinc-100"

    >

      {copied ? (

        <>

          <Check className="h-3 w-3 text-emerald-400" />

          <span className="text-emerald-400">OK</span>

        </>

      ) : (

        <>

          <Copy className="h-3 w-3" />

          {t('audit.copy')}

        </>

      )}

    </button>

  );

}



export function InterviewPanel({ projects }: { projects: ProjectAuditDetail[] }) {

  return (

    <div className={LIST_CLASS}>

      {projects.map((project) => (

        <article key={project.RepoName} className={ITEM_CLASS}>

          <RepoHeader

            repoName={project.RepoName}

            trailing={<CopyTrapButton text={project.InterviewTrapQuestion} />}

          />

          <p className={BODY_CLASS}>{project.InterviewTrapQuestion}</p>

        </article>

      ))}

    </div>

  );

}


