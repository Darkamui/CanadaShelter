import {
  getListPlatformStaffQueryKey,
  useChangePlatformStaffRoles,
  useListPlatformStaff,
  useReactivatePlatformStaff,
  useSuspendPlatformStaff,
} from '@shelter/api-client/hooks/platform';
import type { StaffMemberResponse } from '@shelter/api-client/model';
import { Avatar, initialsOf } from '@shelter/ui/components/avatar';
import { Badge } from '@shelter/ui/components/badge';
import { Button } from '@shelter/ui/components/button';
import { IconAction } from '@shelter/ui/components/icon-action';
import { DataTable, hideBelow, type DataTableFeatures } from '@shelter/ui/components/data-table';
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@shelter/ui/components/dialog';
import { toast } from '@shelter/ui/components/toaster';
import { useQueryClient } from '@tanstack/react-query';
import { createColumnHelper } from '@tanstack/react-table';
import { Pause, Play, ShieldCheck } from 'lucide-react';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { isRoleKey, type RoleKey } from '../../../lib/auth/permissions';
import { statusOf } from '../../../lib/auth/session';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { useDataTableLabels } from '../../../lib/list/useDataTableLabels';
import { RoleCheckboxes } from './RoleCheckboxes';

const helper = createColumnHelper<DataTableFeatures, StaffMemberResponse>();

/** The organization's members with their roles and status; role changes, suspension and reactivation for managers. */
export function StaffMembersTable({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation('platform');
  const staff = useListPlatformStaff();
  const queryClient = useQueryClient();
  const suspend = useSuspendPlatformStaff();
  const reactivate = useReactivatePlatformStaff();
  const [editing, setEditing] = useState<StaffMemberResponse>();
  const labels = useDataTableLabels(t('staff.membersCaption'));
  const busy = suspend.isPending || reactivate.isPending;

  const columns = useMemo(() => {
    const refresh = () =>
      queryClient.invalidateQueries({ queryKey: getListPlatformStaffQueryKey() });
    const onError = (e: unknown) =>
      toast.error(statusOf(e) === 409 ? t('staff.lastAdministrator') : t('common.unexpectedError'));
    const toggle = (member: StaffMemberResponse) => {
      const active = statusKey(member) === 'active';
      (active ? suspend : reactivate).mutate(
        { membershipId: member.membershipId },
        {
          onSuccess: async () => {
            toast.success(
              t(active ? 'staff.suspendedToast' : 'staff.reactivatedToast', {
                name: member.displayName,
              }),
            );
            await refresh();
          },
          onError,
        },
      );
    };

    return helper.columns([
      helper.accessor('displayName', {
        header: t('fields.displayName'),
        cell: (info) => (
          <span className="flex items-center gap-3">
            <Avatar size="sm">{initialsOf(info.getValue())}</Avatar>
            <span className="font-medium whitespace-nowrap">{info.getValue()}</span>
          </span>
        ),
      }),
      helper.accessor('email', { header: t('fields.email') }),
      helper.accessor('roles', {
        header: t('staff.roles'),
        cell: (info) => <RoleBadges roles={info.getValue()} />,
      }),
      helper.accessor((member) => statusKey(member), {
        id: 'status',
        header: t('staff.status'),
        cell: (info) => (
          <Badge dot tone={info.getValue() === 'active' ? 'success' : 'neutral'}>
            {t(`staff.statuses.${info.getValue()}`)}
          </Badge>
        ),
      }),
      ...(canManage
        ? [
            helper.display({
              id: 'actions',
              header: () => <span className="sr-only">{t('staff.actions')}</span>,
              cell: ({ row: { original: member } }) => {
                const active = statusKey(member) === 'active';
                return (
                  <span className="flex justify-end gap-0.5">
                    <IconAction
                      label={t('staff.editRolesOf', { name: member.displayName })}
                      hint={t('staff.editRoles')}
                      disabled={busy}
                      onClick={() => setEditing(member)}
                    >
                      <ShieldCheck />
                    </IconAction>
                    <IconAction
                      label={t(active ? 'staff.suspendName' : 'staff.reactivateName', {
                        name: member.displayName,
                      })}
                      hint={active ? t('staff.suspend') : t('staff.reactivate')}
                      disabled={busy}
                      onClick={() => toggle(member)}
                    >
                      {active ? <Pause /> : <Play />}
                    </IconAction>
                  </span>
                );
              },
            }),
          ]
        : []),
    ]);
  }, [t, canManage, busy, suspend, reactivate, queryClient]);

  return (
    <>
      <DataTable
        columns={columns}
        data={staff.data ?? []}
        getRowId={(member) => member.membershipId}
        status={staff.status}
        labels={{ ...labels, error: t('staff.loadFailed') }}
        columnClassNames={{ email: hideBelow.lg, actions: 'w-px whitespace-nowrap' }}
      />
      {editing && (
        <EditRolesDialog
          key={editing.membershipId}
          member={editing}
          onClose={() => setEditing(undefined)}
        />
      )}
    </>
  );
}

function EditRolesDialog({
  member,
  onClose,
}: {
  member: StaffMemberResponse;
  onClose: () => void;
}) {
  const { t } = useTranslation('platform');
  const queryClient = useQueryClient();
  const changeRoles = useChangePlatformStaffRoles();
  const [roles, setRoles] = useState<RoleKey[]>(member.roles.filter(isRoleKey));

  const save = () =>
    changeRoles.mutate(
      { membershipId: member.membershipId, data: { roles } },
      {
        onSuccess: async () => {
          toast.success(t('staff.rolesSaved', { name: member.displayName }));
          onClose();
          await queryClient.invalidateQueries({ queryKey: getListPlatformStaffQueryKey() });
        },
      },
    );

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent closeLabel={t('common.close')}>
        <DialogHeader>
          <DialogTitle>{t('staff.editRolesOf', { name: member.displayName })}</DialogTitle>
          <DialogDescription>{t('staff.editRolesDescription')}</DialogDescription>
        </DialogHeader>
        <form
          className="flex flex-col gap-4"
          onSubmit={(event) => {
            event.preventDefault();
            save();
          }}
        >
          {changeRoles.isError && (
            <FormAlert tone="error">
              {statusOf(changeRoles.error) === 409
                ? t('staff.lastAdministrator')
                : t('common.unexpectedError')}
            </FormAlert>
          )}
          <RoleCheckboxes
            legend={t('staff.rolesOf', { name: member.displayName })}
            value={roles}
            onChange={setRoles}
            disabled={changeRoles.isPending}
          />
          <DialogFooter className="mt-1">
            <DialogClose asChild>
              <Button type="button" variant="ghost">
                {t('common.cancel')}
              </Button>
            </DialogClose>
            <Button type="submit" disabled={changeRoles.isPending || roles.length === 0}>
              {t('common.save')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

/** Role badges; unknown keys (grant nothing) are shown as is. */
export function RoleBadges({ roles }: { roles: readonly string[] }) {
  const { t } = useTranslation('platform');
  return (
    <span className="flex flex-wrap gap-1.5">
      {roles.map((role) => (
        <Badge key={role} tone={role === 'administrator' ? 'primary' : 'neutral'}>
          {isRoleKey(role) ? t(`roles.${role}`) : role}
        </Badge>
      ))}
    </span>
  );
}

function statusKey(member: StaffMemberResponse): 'active' | 'suspended' {
  return member.status.toLowerCase() === 'active' ? 'active' : 'suspended';
}
