import { useLogoutPlatformSession } from '@shelter/api-client/hooks/platform';
import { useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router';
import type { AuthState } from '../../../lib/auth/navigation';
import { paths } from '../../../lib/auth/paths';

/** Ends the session, then shows the login page with a "signed out" notice. */
export function useLogout() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const logout = useLogoutPlatformSession();

  return {
    isPending: logout.isPending,
    logout: () =>
      logout.mutate(undefined, {
        onSettled: () => {
          const state: AuthState = { notice: 'signedOut' };
          void navigate(paths.login, { replace: true, state });
          queryClient.clear();
        },
      }),
  };
}
