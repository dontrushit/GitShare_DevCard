export interface LanguageMetric {
  Language: string;
  Percentage: number;
}

export interface HourlyActivity {
  Hour: number;
  CommitCount: number;
}

export interface RepoSummary {
  Name: string;
  Description: string;
  Stars: number;
  Language: string;
  Url: string;
}

export type DebtSeverity = 'Critical' | 'Warning' | 'Minor' | 'CLEAN' | 'NONE';

export interface RepositoryLevelInfo {
  Code: string;
  Title: string;
  Score: number;
  Rationale?: string;
}

export interface ProjectAuditDetail {
  RepoName: string;
  ProjectClass?: string;
  Framework: string;
  LayoutType: string;
  KeyFiles: string[];
  TechnicalDebt: string;
  DebtSeverity: DebtSeverity | string;
  InterviewTrapQuestion: string;
  Pros?: string[];
  Cons?: string[];
  RepositoryLevel?: RepositoryLevelInfo | null;
  ArchitectureSummary?: string;
}

export interface StructuredAuditResponse {
  Projects: ProjectAuditDetail[];
  CoreEngineeringFocus: string;
  GitFormatStandard?: string;
  ExperienceProfile?: string;
  OpenSourceImpact?: string;
}

export interface GitHubActivityTelemetry {
  RecentCommitMessages: string[];
  CommitsInWorkingHours: number;
  CommitsInOffHours: number;
  ExternalPullRequests: string[];
}

export interface ProgrammerLevelInfo {
  Code: string;
  Title: string;
  Score: number;
  RawScore?: number;
  SignalConfidence?: number;
  IsLowConfidence?: boolean;
  Rationale: string;
  AssessmentSummary?: string;
  Disclaimer?: string;
}

export interface DevCardProfile {
  ContentLocale?: string;
  Username: string;
  AvatarUrl: string;
  Bio: string;
  Location: string;
  PublicRepos: number;
  TotalStars: number;
  TotalForks: number;
  ContributionRatio: number;
  OwnRepositoryCount: number;
  ForkedRepositoryCount: number;
  SmallPetProjects: number;
  MediumProjects: number;
  ProductionScaleProjects: number;
  AuditData: StructuredAuditResponse | null;
  ActivityTelemetry?: GitHubActivityTelemetry | null;
  LanguageStack: LanguageMetric[];
  CommitRhythm: HourlyActivity[];
  TopRepositories: RepoSummary[];
  ProgrammerLevel?: ProgrammerLevelInfo | null;
  AnalyzedAtUtc?: string | null;
  ServedFromCache?: boolean;
}
