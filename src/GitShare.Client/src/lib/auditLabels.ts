import type { ProjectAuditDetail } from '../types';
import type { AppLocale } from '../i18n/types';
import { messages } from '../i18n/messages';

const UNDEFINED_FRAMEWORK_MARKERS = ['не определён', 'не определен', 'not defined', 'unknown'];

function labels(locale: AppLocale) {
  return messages[locale].auditLabels;
}

export function isUndefinedFramework(framework: string): boolean {
  const lower = framework.trim().toLowerCase();
  return (
    !lower ||
    UNDEFINED_FRAMEWORK_MARKERS.some((marker) => lower.includes(marker))
  );
}

/** Бейдж стека для UI: без пугающего «не определён». */
const FRAMEWORK_I18N_RU: Record<string, string> = {
  '.NET, Console': '.NET, консоль',
  'DevOps (IaC/Containers)': 'DevOps (IaC/контейнеры)',
};

const LAYOUT_I18N_RU: Record<string, string> = {
  'Console Utility': 'Консольная утилита',
  'Flat Monolith': 'Плоский монолит',
  'Flat Monolith (WinForms)': 'Плоский монолит (WinForms)',
};

export function formatFrameworkBadge(
  framework: string,
  projectClass?: string,
  locale: AppLocale = 'ru',
): string {
  const trimmed = framework.trim();
  if (!isUndefinedFramework(trimmed)) {
    if (locale === 'ru' && FRAMEWORK_I18N_RU[trimmed]) {
      return FRAMEWORK_I18N_RU[trimmed];
    }

    return trimmed;
  }

  const cls = projectClass?.trim() ?? '';
  const L = labels(locale);

  if (cls.includes('Utility') || cls.includes('Automation') || cls.includes('QA') || cls.includes('Testing')) {
    return L.polyglot;
  }

  if (cls.includes('DocOps') || cls.includes('Knowledge')) {
    return L.docOps;
  }

  return L.specialized;
}

/** Подпись типа проекта (если есть с бэкенда). */
const BADGE_BASE =
  'inline-flex max-w-full truncate rounded-md border px-2 py-0.5 text-[10px] font-medium uppercase tracking-wide';

/** Матовый бейдж технологического стека (Java, Playwright, .NET …). */
export const stackBadgeClass = `${BADGE_BASE} border-zinc-700/60 bg-zinc-800/80 text-zinc-300`;

/** Стек DevOps / IaC — чуть холоднее, без ярких акцентов. */
export const devOpsStackBadgeClass = `${BADGE_BASE} border-slate-800/60 bg-slate-950/80 text-slate-400`;

export function stackBadgeClassFor(frameworkLabel: string): string {
  const lower = frameworkLabel.toLowerCase();
  if (
    lower.includes('devops') ||
    lower.includes('terraform') ||
    lower.includes('kubernetes') ||
    lower.includes('docker')
  ) {
    return devOpsStackBadgeClass;
  }
  return stackBadgeClass;
}

/** Приглушённый бейдж layout / архитектурного паттерна. */
export const layoutBadgeClass = `${BADGE_BASE} border-zinc-800/80 bg-zinc-900/50 font-normal normal-case tracking-normal text-zinc-500`;

export function projectClassBadgeClass(projectClass?: string): string {
  const cls = projectClass?.trim() ?? '';

  if (cls.includes('QA') || cls.includes('Testing')) {
    return `${BADGE_BASE} border-teal-900/40 bg-teal-950/40 text-teal-400/90`;
  }

  if (cls.includes('DocOps') || cls.includes('Knowledge')) {
    return `${BADGE_BASE} border-slate-800/60 bg-slate-950/80 text-slate-400`;
  }

  if (cls.includes('Utility') || cls.includes('Automation')) {
    return `${BADGE_BASE} border-zinc-700/50 bg-zinc-900/70 text-zinc-400`;
  }

  if (cls === 'Production App') {
    return `${BADGE_BASE} border-zinc-700/50 bg-zinc-800/50 text-zinc-400`;
  }

  return `${BADGE_BASE} border-zinc-700/60 bg-zinc-800/80 text-zinc-300`;
}

function isPetDesktop(projectClass: string, framework?: string): boolean {
  if (!projectClass.includes('Utility') && !projectClass.includes('Automation')) {
    return false;
  }

  const stack = (framework ?? '').toLowerCase();
  return stack.includes('winforms') || stack.includes('wpf') || stack.includes('desktop');
}

export function formatProjectClassLabel(
  projectClass?: string,
  locale: AppLocale = 'ru',
  framework?: string,
): string | null {
  const cls = projectClass?.trim();
  if (!cls) {
    return null;
  }

  switch (cls) {
    case 'Production App':
      return messages[locale].architecture.productionClass;
    case 'Utility / Automation':
      return isPetDesktop(cls, framework)
        ? messages[locale].architecture.petDesktopClass
        : labels(locale).utility;
    case 'QA / Testing':
      return labels(locale).qa;
    case 'DocOps / Knowledge Base':
      return messages[locale].architecture.docOpsClass;
    default:
      return cls;
  }
}

export function formatLayoutBadge(layout: string, projectClass?: string, locale: AppLocale = 'ru'): string {
  let normalized = layout
    .trim()
    .replace(/\s*\((?:critical|warning|minor|clean|none)\)\s*/gi, '')
    .replace(/\bfat monolith\b/gi, 'Flat Monolith')
    .trim();

  const lower = normalized.toLowerCase();

  if (
    lower === 'flat monolith' &&
    projectClass &&
    projectClass !== 'Production App'
  ) {
    if (projectClass.includes('DocOps')) {
      return labels(locale).materials;
    }
    if (projectClass.includes('QA') || projectClass.includes('Utility')) {
      return labels(locale).scriptProject;
    }
    return labels(locale).compactStructure;
  }

  if (locale === 'ru' && LAYOUT_I18N_RU[normalized]) {
    return LAYOUT_I18N_RU[normalized];
  }

  return normalized;
}

export function emptyKeyFilesHint(project: ProjectAuditDetail, locale: AppLocale = 'ru'): string {
  const cls = project.ProjectClass ?? '';
  const L = labels(locale);

  if (cls.includes('DocOps')) {
    return L.knowledgeRepo;
  }

  if (cls.includes('QA') || cls.includes('Testing')) {
    return L.testProject;
  }

  if (cls.includes('Utility') || cls.includes('Automation')) {
    return L.utilityNoLayers;
  }

  if (isUndefinedFramework(project.Framework)) {
    return L.stackByFiles;
  }

  return L.keyFilesHeuristic;
}
