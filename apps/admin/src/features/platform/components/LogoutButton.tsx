import { Button } from '@shelter/ui/components/button';
import { LogOut } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useLogout } from './useLogout';

/** Sign-out outside the app shell (organization picker, forced MFA enrollment). The shell uses the user menu. */
export function LogoutButton({ className }: { className?: string }) {
  const { t } = useTranslation('platform');
  const { logout, isPending } = useLogout();

  return (
    <Button variant="ghost" size="sm" className={className} disabled={isPending} onClick={logout}>
      <LogOut aria-hidden="true" />
      {t('logout')}
    </Button>
  );
}
