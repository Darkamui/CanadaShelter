import { useTranslation } from 'react-i18next';
import { Outlet } from 'react-router';
import { BrandMark } from './BrandMark';
import { LanguageSwitcher } from './LanguageSwitcher';

/** Sign-in, invitation, organization and enrollment pages: the brand and one centered card, no navigation. */
export function AuthLayout() {
  const { t } = useTranslation();

  return (
    <div className="relative flex min-h-svh flex-col bg-background">
      {/* A quiet band of the sidebar's spruce behind the top of the card ties sign-in to the app. */}
      <div aria-hidden="true" className="absolute inset-x-0 top-0 h-72 bg-sidebar" />
      <header className="relative flex h-16 items-center justify-between px-4 text-sidebar-foreground sm:px-8">
        <span className="flex items-center gap-2.5">
          <BrandMark className="size-8 text-sidebar-indicator" />
          <span className="font-display text-[1.0625rem] font-semibold tracking-[-0.01em]">
            {t('appName')}
          </span>
        </span>
        <LanguageSwitcher className="text-sidebar-foreground/80 hover:bg-sidebar-accent hover:text-sidebar-foreground" />
      </header>
      <main
        id="main"
        className="relative flex flex-1 items-start justify-center px-4 pt-8 pb-12 sm:pt-14"
      >
        <div className="flex w-full max-w-md flex-col gap-6 rounded-2xl border bg-card p-6 shadow-raised sm:p-8 [&>h1]:font-display [&>h1]:text-[1.75rem] [&>h1]:leading-tight [&>h1]:font-semibold [&>h1]:tracking-[-0.015em]">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
