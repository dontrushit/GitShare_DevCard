import { Loader2 } from 'lucide-react';
import { useLocale } from '../i18n/LocaleProvider';

export function LoadingState() {
  const { t } = useLocale();

  return (
    <section className="space-y-2">
      <header className="flex items-center justify-center gap-2 py-3">
        <Loader2 className="h-4 w-4 animate-spin text-slate-400" />
        <span className="text-xs text-foreground/70">{t('loading.analyzing')}</span>
      </header>
      <article className="card-panel space-y-2 p-3">
        <span className="block h-10 animate-pulse rounded-md bg-slate-800/60" />
        <span className="block h-16 animate-pulse rounded-md bg-slate-800/60" />
        <span className="block h-24 animate-pulse rounded-md bg-slate-800/60" />
        <span className="grid grid-cols-2 gap-2">
          <span className="block h-14 animate-pulse rounded-md bg-slate-800/60" />
          <span className="block h-14 animate-pulse rounded-md bg-slate-800/60" />
          <span className="block h-14 animate-pulse rounded-md bg-slate-800/60" />
          <span className="block h-14 animate-pulse rounded-md bg-slate-800/60" />
        </span>
      </article>
    </section>
  );
}
