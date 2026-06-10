import { RefreshCw, Search } from 'lucide-react';
import type { FormEvent } from 'react';
import { useLocale } from '../i18n/LocaleProvider';

interface SearchBarProps {
  value: string;
  onChange: (value: string) => void;
  onSubmit: () => void;
  onRefresh?: () => void;
  disabled?: boolean;
  className?: string;
}

export function SearchBar({
  value,
  onChange,
  onSubmit,
  onRefresh,
  disabled,
  className = '',
}: SearchBarProps) {
  const { t } = useLocale();
  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    onSubmit();
  };

  return (
    <form onSubmit={handleSubmit} className={`flex items-center gap-2 ${className}`.trim()}>
      <div className="relative flex flex-1 items-center">
        <Search className="pointer-events-none absolute left-2 h-3.5 w-3.5 text-foreground/40" />
        <input
          type="text"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={t('search.placeholder')}
          className="input-field pl-8"
          disabled={disabled}
          autoComplete="off"
          spellCheck={false}
        />
      </div>
      <button type="submit" className="btn-primary" disabled={disabled || !value.trim()}>
        {t('search.analyze')}
      </button>
      {onRefresh && (
        <button
          type="button"
          className="inline-flex shrink-0 items-center gap-1 rounded-lg border border-white/10 bg-white/5 px-2.5 py-2 text-xs text-foreground/80 transition hover:bg-white/10 hover:text-foreground disabled:opacity-40"
          onClick={onRefresh}
          disabled={disabled || !value.trim()}
          title={t('search.refreshTitle')}
        >
          <RefreshCw className="h-3.5 w-3.5" aria-hidden />
          <span className="hidden sm:inline">{t('search.refresh')}</span>
        </button>
      )}
    </form>
  );
}
