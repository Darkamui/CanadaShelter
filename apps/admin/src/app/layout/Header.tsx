import type { SessionResponse } from '@shelter/api-client/model';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { LogoutButton } from '../../features/platform/components/LogoutButton';
import { PingStatus } from '../../features/platform/components/PingStatus';
import { paths } from '../../lib/auth/paths';
import { activeOrganization } from '../../lib/auth/session';
import { LanguageSwitcher } from './LanguageSwitcher';

export function Header({ session }: { session: SessionResponse }) {
  const { t } = useTranslation();
  const organization = activeOrganization(session);

  return (
    <header className="flex min-h-14 flex-wrap items-center justify-between gap-3 border-b px-6 py-2">
      <div className="flex items-baseline gap-3">
        <span className="font-semibold">{t('appName')}</span>
        {organization && <span className="text-sm">{organization.organizationName}</span>}
        {session.memberships.length > 1 && (
          <Link to={paths.organizations} className="text-sm underline underline-offset-4">
            {t('account.switchOrganization')}
          </Link>
        )}
      </div>
      <div className="flex flex-wrap items-center gap-3">
        <PingStatus />
        <LanguageSwitcher />
        <nav aria-label={t('account.label')} className="flex items-center gap-3">
          <span className="text-sm">{session.user.displayName}</span>
          <Link to={paths.security} className="text-sm underline underline-offset-4">
            {t('account.security')}
          </Link>
          <LogoutButton />
        </nav>
      </div>
    </header>
  );
}
