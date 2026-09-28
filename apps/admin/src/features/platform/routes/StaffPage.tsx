import { useTranslation } from 'react-i18next';
import { Permissions } from '../../../lib/auth/permissions';
import { hasPermission, useSession } from '../../../lib/auth/session';
import { InvitationsPanel } from '../components/InvitationsPanel';
import { StaffMembersTable } from '../components/StaffMembersTable';

/** Staff of the active organization. Reading needs `platform.staff.read`; every change `platform.staff.manage`. */
export function StaffPage() {
  const { t } = useTranslation('platform');
  const { data: session } = useSession();
  const canManage = hasPermission(session, Permissions.staffManage);

  return (
    <section className="flex flex-col gap-8">
      <h1 className="text-2xl font-semibold">{t('staff.title')}</h1>
      <StaffMembersTable canManage={canManage} />
      <InvitationsPanel canManage={canManage} />
    </section>
  );
}
