import { Button } from '@shelter/ui/components/button';
import { useTranslation } from 'react-i18next';
import { useLogout } from './useLogout';

/** Sign-out outside the app shell (organization picker, forced MFA enrollment). The shell uses the user menu. */
export function LogoutButton() {
  const { t } = useTranslation('platform');
  const { logout, isPending } = useLogout();

  return (
    <Button variant="ghost" size="sm" disabled={isPending} onClick={logout}>
      {t('logout')}
    </Button>
  );
}
