import { Loader2 } from 'lucide-react';
import { APP_LOCALES, LOCALE_LABELS } from '../i18n/types';
import { useLocale } from '../i18n/LocaleProvider';

interface LocaleSwitcherProps {
  loading?: boolean;
}

export function LocaleSwitcher({ loading = false }: LocaleSwitcherProps) {
  const { locale, setLocale, t } = useLocale();

  return (
    <div className="inline-flex shrink-0 items-center gap-1.5">
      <div
        className="inline-flex shrink-0 items-center gap-1 rounded-lg border border-zinc-800 bg-zinc-900/60 p-0.5"
        role="group"
        aria-label={t('locale.switcherLabel')}
        aria-busy={loading}
      >
        {APP_LOCALES.map((code) => {
          const active = locale === code;
          return (
            <button
              key={code}
              type="button"
              onClick={() => setLocale(code)}
              disabled={loading}
              className={`rounded-md px-2.5 py-1 text-[11px] font-medium uppercase tracking-wide transition disabled:cursor-wait disabled:opacity-60 ${
                active
                  ? 'bg-zinc-700 text-zinc-100'
                  : 'text-zinc-500 hover:bg-zinc-800/80 hover:text-zinc-300'
              }`}
              aria-pressed={active}
              title={LOCALE_LABELS[code]}
            >
              {code}
            </button>
          );
        })}
      </div>
      {loading ? (
        <span
          className="inline-flex items-center gap-1 text-zinc-500"
          role="status"
          aria-live="polite"
        >
          <Loader2 className="h-3 w-3 shrink-0 animate-spin" aria-hidden />
          <span className="sr-only">{t('locale.loading')}</span>
        </span>
      ) : null}
    </div>
  );
}
