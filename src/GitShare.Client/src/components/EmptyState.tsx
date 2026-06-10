import { Sparkles } from 'lucide-react';
import { useLocale } from '../i18n/LocaleProvider';

export function EmptyState() {
  const { t } = useLocale();

  return (
    <div className="card-panel flex flex-col items-center justify-center gap-2 px-4 py-8 text-center">
      <Sparkles className="h-5 w-5 text-slate-400" />
      <p className="text-sm text-foreground/80">{t('empty.prompt')}</p>
    </div>
  );
}
