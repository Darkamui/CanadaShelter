import {
  getListPlatformInvitationsQueryKey,
  useCreatePlatformInvitation,
  useListPlatformInvitations,
  useResendPlatformInvitation,
  useRevokePlatformInvitation,
} from '@shelter/api-client/hooks/platform';
import type { InvitationResponse } from '@shelter/api-client/model';
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
  DialogTrigger,
} from '@shelter/ui/components/dialog';
import { EmptyState } from '@shelter/ui/components/empty-state';
import { toast } from '@shelter/ui/components/toaster';
import { useQueryClient } from '@tanstack/react-query';
import { createColumnHelper } from '@tanstack/react-table';
import { Ban, MailPlus, Send, UserPlus } from 'lucide-react';
import { useMemo, useState } from 'react';
import { Controller } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';
import { ROLE_KEYS } from '../../../lib/auth/permissions';
import { statusOf } from '../../../lib/auth/session';
import { formatDate } from '../../../lib/format';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { FormField, SelectField } from '../../../lib/forms/FormField';
import { emailAddress } from '../../../lib/forms/schemas';
import { applyValidationErrors, validationErrors } from '../../../lib/forms/serverErrors';
import { useZodForm } from '../../../lib/forms/useZodForm';
import { useDataTableLabels } from '../../../lib/list/useDataTableLabels';
import { RoleCheckboxes } from './RoleCheckboxes';
import { RoleBadges } from './StaffMembersTable';

const helper = createColumnHelper<DataTableFeatures, InvitationResponse>();

const refreshInvitations = (queryClient: ReturnType<typeof useQueryClient>) =>
  queryClient.invalidateQueries({ queryKey: getListPlatformInvitationsQueryKey() });

/** Open invitations, with resend and revoke for `platform.staff.manage`. */
export function InvitationsPanel({ canManage }: { canManage: boolean }) {
  const { t, i18n } = useTranslation('platform');
  const invitations = useListPlatformInvitations();
  const queryClient = useQueryClient();
  const resend = useResendPlatformInvitation();
  const revoke = useRevokePlatformInvitation();
  const labels = useDataTableLabels(t('invitations.caption'));
  const busy = resend.isPending || revoke.isPending;

  const columns = useMemo(() => {
    const onError = (e: unknown) =>
      toast.error(statusOf(e) === 409 ? t('invitations.closed') : t('common.unexpectedError'));

    return helper.columns([
      helper.accessor('email', {
        header: t('fields.email'),
        cell: (info) => <span className="font-medium">{info.getValue()}</span>,
      }),
      helper.accessor('roles', {
        header: t('staff.roles'),
        cell: (info) => <RoleBadges roles={info.getValue()} />,
      }),
      helper.accessor('status', {
        header: t('staff.status'),
        cell: (info) =>
          info.getValue() === 'expired' ? (
            <Badge dot tone="warning">
              {t('invitations.statuses.expired')}
            </Badge>
          ) : (
            <Badge dot tone="info">
              {t('invitations.statuses.pending')}
            </Badge>
          ),
      }),
      helper.accessor('expiresAt', {
        header: t('invitations.expires'),
        cell: (info) => formatDate(info.getValue(), i18n.language),
      }),
      ...(canManage
        ? [
            helper.display({
              id: 'actions',
              header: () => <span className="sr-only">{t('staff.actions')}</span>,
              cell: ({ row: { original: invitation } }) => (
                <span className="flex justify-end gap-0.5">
                  <IconAction
                    label={t('invitations.resendTo', { email: invitation.email })}
                    hint={t('invitations.resend')}
                    disabled={busy}
                    onClick={() =>
                      resend.mutate(
                        { invitationId: invitation.id },
                        {
                          onSuccess: async () => {
                            toast.success(t('invitations.sent', { email: invitation.email }));
                            await refreshInvitations(queryClient);
                          },
                          onError,
                        },
                      )
                    }
                  >
                    <Send />
                  </IconAction>
                  <IconAction
                    label={t('invitations.revokeFor', { email: invitation.email })}
                    hint={t('invitations.revoke')}
                    disabled={busy}
                    onClick={() =>
                      revoke.mutate(
                        { invitationId: invitation.id },
                        {
                          onSuccess: async () => {
                            toast.success(t('invitations.revoked'));
                            await refreshInvitations(queryClient);
                          },
                          onError,
                        },
                      )
                    }
                  >
                    <Ban />
                  </IconAction>
                </span>
              ),
            }),
          ]
        : []),
    ]);
  }, [t, i18n.language, canManage, busy, resend, revoke, queryClient]);

  return (
    <DataTable
      columns={columns}
      data={invitations.data ?? []}
      getRowId={(invitation) => invitation.id}
      status={invitations.status}
      labels={{ ...labels, error: t('invitations.loadFailed') }}
      columnClassNames={{
        roles: hideBelow.lg,
        expiresAt: hideBelow.md,
        actions: 'w-px whitespace-nowrap',
      }}
      emptyState={
        <EmptyState
          icon={<MailPlus />}
          title={t('invitations.none')}
          description={t('invitations.noneBody')}
        />
      }
    />
  );
}

/** The header action: opens the invite form in a dialog, and closes it with a toast once sent. */
export function InviteDialog() {
  const { t } = useTranslation('platform');
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>
          <UserPlus aria-hidden="true" />
          {t('invitations.inviteMember')}
        </Button>
      </DialogTrigger>
      <DialogContent closeLabel={t('common.close')}>
        <DialogHeader>
          <DialogTitle>{t('invitations.inviteMember')}</DialogTitle>
          <DialogDescription>{t('invitations.dialogDescription')}</DialogDescription>
        </DialogHeader>
        <InviteForm
          onInvited={async (email) => {
            setOpen(false);
            toast.success(t('invitations.sent', { email }));
            await refreshInvitations(queryClient);
          }}
        />
      </DialogContent>
    </Dialog>
  );
}

const inviteSchema = z.object({
  email: emailAddress(),
  roles: z.array(z.enum(ROLE_KEYS)).min(1, { error: 'platform:invitations.rolesRequired' }),
  language: z.enum(['fr', 'en']),
});

function InviteForm({ onInvited }: { onInvited: (email: string) => Promise<void> }) {
  const { t, i18n } = useTranslation('platform');
  const create = useCreatePlatformInvitation();
  const form = useZodForm(inviteSchema, {
    defaultValues: {
      email: '',
      roles: ['staff'],
      language: i18n.language === 'en-CA' ? 'en' : 'fr',
    },
  });
  const { errors } = form.formState;

  const submit = form.handleSubmit((data) =>
    create.mutate(
      { data },
      {
        onSuccess: async (invitation) => {
          await onInvited(invitation.email);
        },
        onError: (error) =>
          applyValidationErrors(error, form.setError, {
            email: ['email', 'platform:invitations.invalid'],
            roles: ['roles', 'platform:invitations.invalid'],
          }),
      },
    ),
  );

  return (
    <form
      className="flex flex-col gap-4"
      onSubmit={submit}
      noValidate
      aria-label={t('invitations.invite')}
    >
      {create.isError && !validationErrors(create.error) && (
        <FormAlert tone="error">
          {statusOf(create.error) === 409
            ? t('invitations.duplicate')
            : statusOf(create.error) === 400
              ? t('invitations.invalid')
              : t('common.unexpectedError')}
        </FormAlert>
      )}
      <FormField
        label={t('fields.email')}
        type="email"
        autoComplete="off"
        required
        error={errors.email}
        {...form.register('email')}
      />
      <Controller
        control={form.control}
        name="roles"
        render={({ field, fieldState }) => (
          <RoleCheckboxes
            legend={t('staff.roles')}
            value={field.value}
            onChange={field.onChange}
            error={fieldState.error}
          />
        )}
      />
      <SelectField label={t('invitations.language')} {...form.register('language')}>
        <option value="fr">{t('invitations.languages.fr')}</option>
        <option value="en">{t('invitations.languages.en')}</option>
      </SelectField>
      <DialogFooter className="mt-1">
        <DialogClose asChild>
          <Button type="button" variant="ghost">
            {t('common.cancel')}
          </Button>
        </DialogClose>
        <Button type="submit" disabled={create.isPending}>
          {t('invitations.invite')}
        </Button>
      </DialogFooter>
    </form>
  );
}
