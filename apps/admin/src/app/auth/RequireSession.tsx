import { Button } from '@shelter/ui/components/button';
import { useTranslation } from 'react-i18next';
import { Navigate, Outlet, useLocation } from 'react-router';
import { currentPath, type AuthState } from '../../lib/auth/navigation';
import { paths } from '../../lib/auth/paths';
import { isUnauthorized, useSession } from '../../lib/auth/session';
import { PageStatus } from '../pages/PageStatus';

/** Signed in, or off to the login page (coming back here after). */
export function RequireSession() {
  const { t } = useTranslation();
  const location = useLocation();
  const session = useSession();

  if (session.isError && isUnauthorized(session.error)) {
    const state: AuthState = { from: currentPath(location) };
    return <Navigate to={paths.login} replace state={state} />;
  }

  if (!session.data) {
    return session.isError ? (
      <PageStatus>
        <p role="alert">{t('session.error')}</p>
        <Button variant="outline" onClick={() => void session.refetch()}>
          {t('session.retry')}
        </Button>
      </PageStatus>
    ) : (
      <PageStatus>
        <p role="status">{t('session.loading')}</p>
      </PageStatus>
    );
  }

  return <Outlet />;
}
