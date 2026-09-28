import { useLogoutPlatformSession } from '@shelter/api-client/hooks/platform';
import { Button } from '@shelter/ui/components/button';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import type { AuthState } from '../../../lib/auth/navigation';
import { paths } from '../../../lib/auth/paths';

export function LogoutButton() {
  const { t } = useTranslation('platform');
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const logout = useLogoutPlatformSession();

  return (
    <Button
      variant="outline"
      size="sm"
      disabled={logout.isPending}
      onClick={() =>
        logout.mutate(undefined, {
          onSettled: () => {
            const state: AuthState = { notice: 'signedOut' };
            void navigate(paths.login, { replace: true, state });
            queryClient.clear();
          },
        })
      }
    >
      {t('logout')}
    </Button>
  );
}
