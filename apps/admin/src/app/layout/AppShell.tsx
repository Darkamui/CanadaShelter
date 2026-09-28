import { useTranslation } from 'react-i18next';
import { Outlet } from 'react-router';
import { useSession } from '../../lib/auth/session';
import { Header } from './Header';
import { Sidebar } from './Sidebar';

/** Staff layout. Rendered under `RequireSession` and `RequireOrganization`, so the session is loaded. */
export function AppShell() {
  const { t } = useTranslation();
  const { data: session } = useSession();
  if (!session) return null;

  return (
    <div className="flex min-h-svh">
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:absolute focus:top-2 focus:left-2 focus:z-50 focus:rounded-md focus:bg-background focus:px-3 focus:py-2"
      >
        {t('skipToContent')}
      </a>
      <Sidebar permissions={session.permissions} />
      <div className="flex min-w-0 flex-1 flex-col">
        <Header session={session} />
        <main id="main" tabIndex={-1} className="flex-1 p-6 outline-none">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
