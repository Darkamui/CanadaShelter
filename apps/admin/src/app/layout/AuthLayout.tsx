import { useTranslation } from 'react-i18next';
import { Outlet } from 'react-router';
import { LanguageSwitcher } from './LanguageSwitcher';

/** Sign-in, invitation, organization and enrollment pages: one centered card, no navigation. */
export function AuthLayout() {
  const { t } = useTranslation();

  return (
    <div className="flex min-h-svh flex-col">
      <header className="flex h-14 items-center justify-between border-b px-6">
        <span className="font-semibold">{t('appName')}</span>
        <LanguageSwitcher />
      </header>
      <main id="main" className="flex flex-1 items-start justify-center p-6 sm:pt-16">
        <div className="flex w-full max-w-md flex-col gap-6 rounded-lg border bg-background p-6 shadow-sm">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
