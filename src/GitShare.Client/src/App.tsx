import { useCallback, useEffect, useRef, useState } from 'react';
import { fetchDevCardProfile, ProfileFetchError } from './api';
import { translateApiError } from './i18n/errors';
import type { AppLocale } from './i18n/types';
import { useLocale } from './i18n/LocaleProvider';
import { LocaleSwitcher } from './components/LocaleSwitcher';
import { CommitRhythmChart } from './components/CommitRhythmChart';
import {
  DashboardContainer,
  DashboardFullWidth,
  DashboardMobileStack,
} from './components/DashboardContainer';
import { DesktopBentoGrid } from './components/DesktopBentoGrid';
import { EmptyState } from './components/EmptyState';
import { ErrorAlert } from './components/ErrorAlert';
import { LanguageStackPieChart } from './components/LanguageStackPieChart';
import { LoadingState } from './components/LoadingState';
import { StructuredAuditCard } from './components/StructuredAuditCard';
import { ProfileCard } from './components/ProfileCard';
import { SearchBar } from './components/SearchBar';
import { TopRepositoriesGrid } from './components/TopRepositoriesGrid';
import { getValidCachedProfile, setCachedProfile } from './lib/profileLocaleCache';
import type { DevCardProfile } from './types';

type ViewState = 'idle' | 'loading' | 'success' | 'error';

export default function App() {
  const { t, locale } = useLocale();
  const [username, setUsername] = useState('');
  const [viewState, setViewState] = useState<ViewState>('idle');
  const [profile, setProfile] = useState<DevCardProfile | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [isLocaleLoading, setIsLocaleLoading] = useState(false);
  const fetchInFlight = useRef(false);
  const profileCache = useRef(new Map<string, DevCardProfile>());
  const sessionEpoch = useRef(0);

  const goHome = useCallback(() => {
    sessionEpoch.current += 1;
    fetchInFlight.current = false;
    setIsRefreshing(false);
    setIsLocaleLoading(false);
    setUsername('');
    setProfile(null);
    setErrorMessage(null);
    setViewState('idle');
  }, []);

  const applyProfile = useCallback(
    (trimmed: string, data: DevCardProfile, activeLocale: AppLocale) => {
      setCachedProfile(trimmed, activeLocale, data, profileCache.current);
      setProfile(data);
      setViewState('success');
      setErrorMessage(null);
    },
    [],
  );

  const runAnalysis = useCallback(
    async (
      targetUsername: string,
      options?: {
        forceRefresh?: boolean;
        background?: boolean;
        requestLocale?: AppLocale;
      },
    ) => {
      const trimmed = targetUsername.trim();
      if (!trimmed || fetchInFlight.current) {
        return;
      }

      const requestLocale = options?.requestLocale ?? locale;
      const background = options?.background === true;
      const epoch = sessionEpoch.current;
      fetchInFlight.current = true;

      if (options?.requestLocale !== undefined) {
        setIsLocaleLoading(true);
      }

      if (background) {
        setIsRefreshing(true);
      } else {
        setUsername(trimmed);
        setViewState('loading');
        setErrorMessage(null);
        setProfile(null);
      }

      try {
        const data = await fetchDevCardProfile(trimmed, {
          forceRefresh: options?.forceRefresh,
          locale: requestLocale,
        });

        if (epoch === sessionEpoch.current) {
          applyProfile(trimmed, data, requestLocale);
        }
      } catch (error) {
        if (epoch !== sessionEpoch.current) {
          return;
        }

        const message = translateApiError(error, t);
        const isForceRefreshLimit =
          error instanceof ProfileFetchError && error.kind === 'force_refresh_rate_limit';

        if (isForceRefreshLimit && options?.forceRefresh) {
          try {
            const cachedFromServer = await fetchDevCardProfile(trimmed, {
              locale: requestLocale,
            });
            if (epoch === sessionEpoch.current) {
              applyProfile(trimmed, cachedFromServer, requestLocale);
              setErrorMessage(message);
            }
            return;
          } catch {
            // fall through to keep-profile or error state
          }
        }

        const keepProfileVisible =
          profile !== null && (options?.background === true || isForceRefreshLimit);

        if (keepProfileVisible) {
          setErrorMessage(message);
          return;
        }

        if (!background) {
          setProfile(null);
        }
        setErrorMessage(message);
        setViewState('error');
      } finally {
        fetchInFlight.current = false;
        setIsRefreshing(false);
        if (options?.requestLocale !== undefined) {
          setIsLocaleLoading(false);
        }
      }
    },
    [applyProfile, locale, profile, t],
  );

  const analyze = useCallback(() => {
    void runAnalysis(username);
  }, [runAnalysis, username]);

  const refreshAnalysis = useCallback(() => {
    void runAnalysis(username, {
      forceRefresh: true,
      background: viewState === 'success' && profile !== null,
    });
  }, [runAnalysis, username, viewState, profile]);

  const prevLocale = useRef(locale);
  useEffect(() => {
    if (prevLocale.current === locale) {
      return;
    }

    prevLocale.current = locale;
    setErrorMessage(null);

    const trimmed = username.trim();
    if (!trimmed || (viewState !== 'success' && viewState !== 'error')) {
      return;
    }

    const cached = getValidCachedProfile(trimmed, locale, profileCache.current);
    if (cached) {
      setIsLocaleLoading(false);
      setProfile(cached);
      setViewState('success');
      setErrorMessage(null);
      return;
    }

    setIsLocaleLoading(true);

    if (viewState === 'success' && profile) {
      void runAnalysis(trimmed, {
        background: true,
        requestLocale: locale,
      });
      return;
    }

    if (viewState === 'error') {
      void runAnalysis(trimmed, { requestLocale: locale });
    }
  }, [locale, username, viewState, profile, runAnalysis]);

  const dashboardHeader = (
    <div className="space-y-2 transition-all duration-300">
      <div className="flex items-start justify-between gap-3">
        <button
          type="button"
          onClick={goHome}
          aria-label={t('app.homeAriaLabel')}
          className="min-w-0 cursor-pointer text-left text-sm font-semibold tracking-tight text-foreground transition-colors hover:text-slate-100 lg:text-base"
        >
          {t('app.title')}{' '}
          <span className="text-slate-400">{t('app.subtitle')}</span>
        </button>
        <LocaleSwitcher loading={isLocaleLoading} />
      </div>
      <SearchBar
        value={username}
        onChange={setUsername}
        onSubmit={analyze}
        onRefresh={
          username.trim() && (viewState === 'success' || viewState === 'error')
            ? refreshAnalysis
            : undefined
        }
        disabled={viewState === 'loading' || isRefreshing}
      />
    </div>
  );

  return (
    <DashboardContainer header={dashboardHeader}>
      {viewState === 'idle' && (
        <DashboardFullWidth>
          <EmptyState />
        </DashboardFullWidth>
      )}

      {viewState === 'error' && errorMessage && (
        <DashboardFullWidth>
          <ErrorAlert message={errorMessage} />
        </DashboardFullWidth>
      )}

      {viewState === 'loading' && (
        <>
          <DashboardMobileStack>
            <LoadingState />
            <StructuredAuditCard auditData={null} isLoading />
          </DashboardMobileStack>
          <DesktopBentoGrid auditData={null} isLoading />
        </>
      )}

      {viewState === 'success' && profile && (
        <>
          {errorMessage ? (
            <DashboardFullWidth>
              <ErrorAlert message={errorMessage} />
            </DashboardFullWidth>
          ) : null}
          <DashboardMobileStack>
            <ProfileCard profile={profile} />
            <StructuredAuditCard auditData={profile.AuditData} />
            <LanguageStackPieChart languages={profile.LanguageStack} />
            <CommitRhythmChart rhythm={profile.CommitRhythm} />
            <TopRepositoriesGrid repositories={profile.TopRepositories} />
          </DashboardMobileStack>
          <DesktopBentoGrid profile={profile} auditData={profile.AuditData} />
        </>
      )}
    </DashboardContainer>
  );
}
