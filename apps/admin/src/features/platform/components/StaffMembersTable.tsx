import {
  getListPlatformStaffQueryKey,
  useChangePlatformStaffRoles,
  useListPlatformStaff,
  useReactivatePlatformStaff,
  useSuspendPlatformStaff,
} from '@shelter/api-client/hooks/platform';
import type { StaffMemberResponse } from '@shelter/api-client/model';
import { Button } from '@shelter/ui/components/button';
import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { isRoleKey, type RoleKey } from '../../../lib/auth/permissions';
import { statusOf } from '../../../lib/auth/session';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { RoleCheckboxes } from './RoleCheckboxes';

export function StaffMembersTable({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation('platform');
  const staff = useListPlatformStaff();
  const queryClient = useQueryClient();
  const changeRoles = useChangePlatformStaffRoles();
  const suspend = useSuspendPlatformStaff();
  const reactivate = useReactivatePlatformStaff();
  const [editing, setEditing] = useState<{ membershipId: string; roles: RoleKey[] }>();
  const [error, setError] = useState<string>();

  const onError = (e: unknown) =>
    setError(statusOf(e) === 409 ? t('staff.lastAdministrator') : t('common.unexpectedError'));
  const onSuccess = async () => {
    setError(undefined);
    setEditing(undefined);
    await queryClient.invalidateQueries({ queryKey: getListPlatformStaffQueryKey() });
  };
  const busy = changeRoles.isPending || suspend.isPending || reactivate.isPending;

  if (staff.isPending) return <p role="status">{t('common.loading')}</p>;
  if (staff.isError) return <FormAlert tone="error">{t('staff.loadFailed')}</FormAlert>;

  return (
    <div className="flex flex-col gap-3">
      <h2 className="text-lg font-semibold">{t('staff.members')}</h2>
      {error && <FormAlert tone="error">{error}</FormAlert>}
      <div className="overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead className="border-b text-muted-foreground">
            <tr>
              <th className="py-2 pr-4 font-medium">{t('fields.displayName')}</th>
              <th className="py-2 pr-4 font-medium">{t('fields.email')}</th>
              <th className="py-2 pr-4 font-medium">{t('staff.roles')}</th>
              <th className="py-2 pr-4 font-medium">{t('staff.status')}</th>
              {canManage && <th className="py-2 font-medium">{t('staff.actions')}</th>}
            </tr>
          </thead>
          <tbody>
            {staff.data.map((member) => (
              <tr key={member.membershipId} className="border-b align-top">
                <td className="py-2 pr-4">{member.displayName}</td>
                <td className="py-2 pr-4">{member.email}</td>
                <td className="py-2 pr-4">
                  {editing?.membershipId === member.membershipId ? (
                    <RoleCheckboxes
                      legend={t('staff.rolesOf', { name: member.displayName })}
                      value={editing.roles}
                      onChange={(roles) => setEditing({ ...editing, roles })}
                      disabled={busy}
                    />
                  ) : (
                    <RoleList roles={member.roles} />
                  )}
                </td>
                <td className="py-2 pr-4">{t(`staff.statuses.${statusKey(member)}`)}</td>
                {canManage && (
                  <td className="py-2">
                    <div className="flex flex-wrap gap-2">
                      {editing?.membershipId === member.membershipId ? (
                        <>
                          <Button
                            size="sm"
                            disabled={busy || editing.roles.length === 0}
                            onClick={() =>
                              changeRoles.mutate(
                                {
                                  membershipId: member.membershipId,
                                  data: { roles: editing.roles },
                                },
                                { onSuccess, onError },
                              )
                            }
                          >
                            {t('common.save')}
                          </Button>
                          <Button size="sm" variant="ghost" onClick={() => setEditing(undefined)}>
                            {t('common.cancel')}
                          </Button>
                        </>
                      ) : (
                        <Button
                          size="sm"
                          variant="outline"
                          disabled={busy}
                          aria-label={t('staff.editRolesOf', { name: member.displayName })}
                          onClick={() =>
                            setEditing({
                              membershipId: member.membershipId,
                              roles: member.roles.filter(isRoleKey),
                            })
                          }
                        >
                          {t('staff.editRoles')}
                        </Button>
                      )}
                      {statusKey(member) === 'active' ? (
                        <Button
                          size="sm"
                          variant="outline"
                          disabled={busy}
                          aria-label={t('staff.suspendName', { name: member.displayName })}
                          onClick={() =>
                            suspend.mutate(
                              { membershipId: member.membershipId },
                              { onSuccess, onError },
                            )
                          }
                        >
                          {t('staff.suspend')}
                        </Button>
                      ) : (
                        <Button
                          size="sm"
                          variant="outline"
                          disabled={busy}
                          aria-label={t('staff.reactivateName', { name: member.displayName })}
                          onClick={() =>
                            reactivate.mutate(
                              { membershipId: member.membershipId },
                              { onSuccess, onError },
                            )
                          }
                        >
                          {t('staff.reactivate')}
                        </Button>
                      )}
                    </div>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

/** Role labels; unknown keys (grant nothing) are shown as is. */
export function RoleList({ roles }: { roles: readonly string[] }) {
  const { t } = useTranslation('platform');
  return <>{roles.map((role) => (isRoleKey(role) ? t(`roles.${role}`) : role)).join(', ')}</>;
}

function statusKey(member: StaffMemberResponse): 'active' | 'suspended' {
  return member.status.toLowerCase() === 'active' ? 'active' : 'suspended';
}
