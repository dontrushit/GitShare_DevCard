import type { ReactNode } from 'react';

interface DashboardContainerProps {
  header: ReactNode;
  children: ReactNode;
}

export function DashboardContainer({ header, children }: DashboardContainerProps) {
  return (
    <main className="grid h-auto w-full min-w-0 grid-cols-1 gap-4 overflow-x-hidden overflow-y-visible bg-surface p-4 transition-all duration-300 lg:h-screen lg:grid-cols-12 lg:grid-rows-[auto_minmax(0,1fr)] lg:gap-4 lg:overflow-hidden lg:p-4 lg:px-5 xl:px-6">
      <header className="min-w-0 shrink-0 lg:col-span-12">{header}</header>
      {children}
    </main>
  );
}

export function DashboardMobileStack({ children }: { children: ReactNode }) {
  return <div className="flex min-w-0 flex-col gap-4 lg:hidden">{children}</div>;
}

export function DashboardFullWidth({ children }: { children: ReactNode }) {
  return <section className="min-w-0 lg:col-span-12">{children}</section>;
}
