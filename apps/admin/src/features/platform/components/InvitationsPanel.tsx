import {
  getListPlatformInvitationsQueryKey,
  useCreatePlatformInvitation,
  useListPlatformInvitations,
  useResendPlatformInvitation,
  useRevokePlatformInvitation,
} from '@shelter/api-client/hooks/platform';
import { Button } from '@shelter/ui/components/button';
import { Label } from '@shelter/ui/components/label';
import { useQueryClient } from '@tanstack/react-query';
import { useId, useState } from 'react';
import { Controller } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';
import { ROLE_KEYS } from '../../../lib/auth/permissions';
import { statusOf } from '../../../lib/auth/session';
import { formatDate } from '../../../lib/format';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { FormField } from '../../../lib/forms/FormField';
import { emailAddress } from '../../../lib/forms/schemas';
import { applyValidationErrors, validationErrors } from '../../../lib/forms/serverErrors';
import { useZodForm } from '../../../lib/forms/useZodForm';
import { RoleCheckboxes } from './RoleCheckboxes';
import { RoleList } from './StaffMembersTable';

/** Open invitations and, with `platform.staff.manage`, the invite form. */
export function InvitationsPanel({ canManage }: { canManage: boolean }) {
  const { t, i18n } = useTranslation('platform');
  const invitations = useListPlatformInvitations();
  const queryClient = useQueryClient();
  const resend = useResendPlatformInvitation();
  const revoke = useRevokePlatformInvitation();
  const [message, setMessage] = useState<{ tone: 'error' | 'info'; text: string }>();

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: getListPlatformInvitationsQueryKey() });
  const onError = (e: unknown) =>
    setMessage({
      tone: 'error',
      text: statusOf(e) === 409 ? t('invitations.closed') : t('common.unexpectedError'),
    });
  const busy = resend.isPending || revoke.isPending;

  return (
    <div className="flex flex-col gap-3">
      <h2 className="text-lg font-semibold">{t('invitations.title')}</h2>
      {canManage && (
        <InviteForm
          onInvited={async (email) => {
            setMessage({ tone: 'info', text: t('invitations.sent', { email }) });
            await refresh();
          }}
        />
      )}
      {message && <FormAlert tone={message.tone}>{message.text}</FormAlert>}
      {invitations.isPending ? (
        <p role="status">{t('common.loading')}</p>
      ) : invitations.isError ? (
        <FormAlert tone="error">{t('invitations.loadFailed')}</FormAlert>
      ) : invitations.data.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t('invitations.none')}</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="border-b text-muted-foreground">
              <tr>
                <th className="py-2 pr-4 font-medium">{t('fields.email')}</th>
                <th className="py-2 pr-4 font-medium">{t('staff.roles')}</th>
                <th className="py-2 pr-4 font-medium">{t('staff.status')}</th>
                <th className="py-2 pr-4 font-medium">{t('invitations.expires')}</th>
                {canManage && <th className="py-2 font-medium">{t('staff.actions')}</th>}
              </tr>
            </thead>
            <tbody>
              {invitations.data.map((invitation) => (
                <tr key={invitation.id} className="border-b">
                  <td className="py-2 pr-4">{invitation.email}</td>
                  <td className="py-2 pr-4">
                    <RoleList roles={invitation.roles} />
                  </td>
                  <td className="py-2 pr-4">
                    {invitation.status === 'expired'
                      ? t('invitations.statuses.expired')
                      : t('invitations.statuses.pending')}
                  </td>
                  <td className="py-2 pr-4">{formatDate(invitation.expiresAt, i18n.language)}</td>
                  {canManage && (
                    <td className="py-2">
                      <div className="flex flex-wrap gap-2">
                        <Button
                          size="sm"
                          variant="outline"
                          disabled={busy}
                          aria-label={t('invitations.resendTo', { email: invitation.email })}
                          onClick={() =>
                            resend.mutate(
                              { invitationId: invitation.id },
                              {
                                onSuccess: async () => {
                                  setMessage({
                                    tone: 'info',
                                    text: t('invitations.sent', { email: invitation.email }),
                                  });
                                  await refresh();
                                },
                                onError,
                              },
                            )
                          }
                        >
                          {t('invitations.resend')}
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          disabled={busy}
                          aria-label={t('invitations.revokeFor', { email: invitation.email })}
                          onClick={() =>
                            revoke.mutate(
                              { invitationId: invitation.id },
                              {
                                onSuccess: async () => {
                                  setMessage({ tone: 'info', text: t('invitations.revoked') });
                                  await refresh();
                                },
                                onError,
                              },
                            )
                          }
                        >
                          {t('invitations.revoke')}
                        </Button>
                      </div>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
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
  const languageId = useId();
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
          form.reset({ ...data, email: '', roles: ['staff'] });
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
      className="flex max-w-md flex-col gap-4 rounded-md border p-4"
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
      <div className="flex flex-col gap-2">
        <Label htmlFor={languageId}>{t('invitations.language')}</Label>
        <select
          id={languageId}
          className="h-9 rounded-md border bg-transparent px-3 text-sm"
          {...form.register('language')}
        >
          <option value="fr">{t('invitations.languages.fr')}</option>
          <option value="en">{t('invitations.languages.en')}</option>
        </select>
      </div>
      <Button type="submit" className="self-start" disabled={create.isPending}>
        {t('invitations.invite')}
      </Button>
    </form>
  );
}
