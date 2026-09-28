import { Navigate, Outlet, useLocation } from 'react-router';
import { currentPath, type AuthState } from '../../lib/auth/navigation';
import { paths } from '../../lib/auth/paths';
import { useSession } from '../../lib/auth/session';

/**
 * Inside {@link RequireSession}: an active organization, then MFA when the role requires it (the server
 * answers 403 to everything else until then).
 */
export function RequireOrganization() {
  const location = useLocation();
  const { data } = useSession();
  if (!data) return null;

  const state: AuthState = { from: currentPath(location) };
  if (!data.activeOrganizationId) {
    return <Navigate to={paths.organizations} replace state={state} />;
  }
  if (data.mfaEnrollmentRequired) {
    return <Navigate to={paths.mfaEnroll} replace state={state} />;
  }

  return <Outlet />;
}
