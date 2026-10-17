import { PageHeader } from '@shelter/ui/components/page-header';
import { useTranslation } from 'react-i18next';
import { Permissions } from '../../../lib/auth/permissions';
import { hasPermission, useSession } from '../../../lib/auth/session';
import { InvitationsPanel, InviteDialog } from '../components/InvitationsPanel';
import { StaffMembersTable } from '../components/StaffMembersTable';

/** Staff of the active organization. Reading needs `platform.staff.read`; every change `platform.staff.manage`. */
export function StaffPage() {
  const { t } = useTranslation('platform');
  const { data: session } = useSession();
  const canManage = hasPermission(session, Permissions.staffManage);

  return (
    <div className="flex flex-col gap-8">
      <PageHeader
        title={t('staff.title')}
        description={t('staff.description')}
        actions={canManage && <InviteDialog />}
      />
      <section aria-labelledby="members-title" className="flex flex-col gap-3">
        <h2 id="members-title" className="font-display text-lg font-semibold">
          {t('staff.members')}
        </h2>
        <StaffMembersTable canManage={canManage} />
      </section>
      <section aria-labelledby="invitations-title" className="flex flex-col gap-3">
        <h2 id="invitations-title" className="font-display text-lg font-semibold">
          {t('invitations.title')}
        </h2>
        <InvitationsPanel canManage={canManage} />
      </section>
    </div>
  );
}
