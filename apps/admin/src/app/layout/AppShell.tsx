import { useTranslation } from 'react-i18next';
import { Outlet } from 'react-router';
import { Header } from './Header';
import { Sidebar } from './Sidebar';

export function AppShell() {
  const { t } = useTranslation();

  return (
    <div className="flex min-h-svh">
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:absolute focus:top-2 focus:left-2 focus:z-50 focus:rounded-md focus:bg-background focus:px-3 focus:py-2"
      >
        {t('skipToContent')}
      </a>
      <Sidebar />
      <div className="flex min-w-0 flex-1 flex-col">
        <Header />
        <main id="main" tabIndex={-1} className="flex-1 p-6 outline-none">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
