import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Outlet } from 'react-router';
import { useSession } from '../../lib/auth/session';
import { Header } from './Header';
import { Sidebar } from './Sidebar';

/** UI preference only. */
const SIDEBAR_STORAGE_KEY = 'shelter.sidebar';

function readCollapsed(): boolean {
  try {
    return localStorage.getItem(SIDEBAR_STORAGE_KEY) === 'collapsed';
  } catch {
    return false;
  }
}

function storeCollapsed(collapsed: boolean) {
  try {
    localStorage.setItem(SIDEBAR_STORAGE_KEY, collapsed ? 'collapsed' : 'expanded');
  } catch {
    // Storage blocked: the choice lasts for this page only.
  }
}

/** Staff layout. Rendered under `RequireSession` and `RequireOrganization`, so the session is loaded. */
export function AppShell() {
  const { t } = useTranslation();
  const { data: session } = useSession();
  const [collapsed, setCollapsed] = useState(readCollapsed);
  if (!session) return null;

  return (
    <div className="flex min-h-svh">
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:fixed focus:top-3 focus:left-3 focus:z-50 focus:rounded-md focus:bg-card focus:px-4 focus:py-2 focus:shadow-raised focus:ring-[3px] focus:ring-ring/45"
      >
        {t('skipToContent')}
      </a>
      {/* The column paints the sidebar colour down long pages; the sidebar itself stays in view. */}
      <div className="hidden shrink-0 bg-sidebar lg:block">
        <Sidebar
          permissions={session.permissions}
          collapsed={collapsed}
          onToggle={() => {
            setCollapsed(!collapsed);
            storeCollapsed(!collapsed);
          }}
        />
      </div>
      <div className="flex min-w-0 flex-1 flex-col">
        <Header session={session} />
        <main
          id="main"
          tabIndex={-1}
          className="flex-1 px-4 py-6 outline-none sm:px-6 lg:px-8 lg:py-8"
        >
          <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  );
}
