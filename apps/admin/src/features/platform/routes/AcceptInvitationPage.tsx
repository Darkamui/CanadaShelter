import {
  lookupPlatformInvitation,
  useAcceptPlatformInvitation,
} from '@shelter/api-client/hooks/platform';
import { Button } from '@shelter/ui/components/button';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useLocation, useNavigate } from 'react-router';
import { z } from 'zod';
import { readAuthState, type AuthState } from '../../../lib/auth/navigation';
import { paths } from '../../../lib/auth/paths';
import {
  MIN_PASSWORD_LENGTH,
  newPasswordFields,
  passwordsMatch,
} from '../../../lib/auth/passwords';
import { resetSession, statusOf, useSession } from '../../../lib/auth/session';
import { useLinkFragment } from '../../../lib/auth/useLinkFragment';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { FormField } from '../../../lib/forms/FormField';
import { requiredText } from '../../../lib/forms/schemas';
import { applyValidationErrors, validationErrors } from '../../../lib/forms/serverErrors';
import { useZodForm } from '../../../lib/forms/useZodForm';

/**
 * From the emailed link `/accept-invitation#token={token}`. A new email creates its account here; an email
 * that already has one must be signed in as that account (the server checks); the secret then comes back
 * from sign-in in router state.
 */
export function AcceptInvitationPage() {
  const { t } = useTranslation('platform');
  const location = useLocation();
  const fragment = useLinkFragment();
  const [token] = useState(
    () => fragment.get('token') ?? readAuthState(location.state).invitationToken ?? null,
  );
  const lookup = useQuery({
    queryKey: ['invitation-lookup', token],
    queryFn: ({ signal }) => lookupPlatformInvitation({ token }, { signal }),
    enabled: Boolean(token),
    retry: false,
    gcTime: 0,
  });

  if (!token || lookup.isError) {
    return (
      <>
        <h1 className="text-2xl font-semibold">{t('accept.title')}</h1>
        <FormAlert tone="error">
          {!token || statusOf(lookup.error) === 404
            ? t('accept.invalidLink')
            : t('common.unexpectedError')}
        </FormAlert>
      </>
    );
  }

  if (!lookup.data) {
    return <p role="status">{t('common.loading')}</p>;
  }

  const invitation = lookup.data;
  return (
    <>
      <h1 className="text-2xl font-semibold">
        {t('accept.heading', { organization: invitation.organizationName })}
      </h1>
      <p className="text-sm text-muted-foreground">
        {t('accept.for', { email: invitation.email })}
      </p>
      {invitation.accountExists ? (
        <ExistingAccount token={token} email={invitation.email} />
      ) : (
        <NewAccount token={token} email={invitation.email} />
      )}
    </>
  );
}

const newAccountSchema = z
  .object({ displayName: requiredText('platform:accept.nameRequired'), ...newPasswordFields })
  .refine(passwordsMatch.check, passwordsMatch.params);

function NewAccount({ token, email }: { token: string; email: string }) {
  const { t } = useTranslation('platform');
  const navigate = useNavigate();
  const accept = useAcceptPlatformInvitation();
  const form = useZodForm(newAccountSchema, {
    defaultValues: { displayName: '', password: '', confirmation: '' },
  });
  const { errors } = form.formState;

  const submit = form.handleSubmit(({ displayName, password }) =>
    accept.mutate(
      { data: { token, displayName, password } },
      {
        onSuccess: () => {
          const state: AuthState = { notice: 'accountCreated', email };
          void navigate(paths.login, { replace: true, state });
        },
        onError: (error) =>
          applyValidationErrors(error, form.setError, {
            displayName: ['displayName', 'platform:accept.nameRequired'],
            password: ['password', 'platform:password.rejected'],
          }),
      },
    ),
  );

  return (
    <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
      <p className="text-sm">{t('accept.newAccount')}</p>
      {/* Field errors show on their fields; anything else here. */}
      {accept.isError && !validationErrors(accept.error) && (
        <FormAlert tone="error">{acceptError(accept.error, t)}</FormAlert>
      )}
      <FormField
        label={t('fields.displayName')}
        autoComplete="name"
        required
        maxLength={200}
        error={errors.displayName}
        {...form.register('displayName')}
      />
      <FormField
        label={t('fields.password')}
        type="password"
        autoComplete="new-password"
        required
        minLength={MIN_PASSWORD_LENGTH}
        hint={t('password.hint', { count: MIN_PASSWORD_LENGTH })}
        error={errors.password}
        {...form.register('password')}
      />
      <FormField
        label={t('fields.confirmPassword')}
        type="password"
        autoComplete="new-password"
        required
        error={errors.confirmation}
        {...form.register('confirmation')}
      />
      <Button type="submit" disabled={accept.isPending}>
        {t('accept.createAndJoin')}
      </Button>
    </form>
  );
}

function ExistingAccount({ token, email }: { token: string; email: string }) {
  const { t } = useTranslation('platform');
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const session = useSession();
  const accept = useAcceptPlatformInvitation();

  if (session.isPending) {
    return <p role="status">{t('common.loading')}</p>;
  }

  if (!session.data) {
    // Come back here after signing in; the secret travels in router state, never in a URL.
    const state: AuthState = { from: paths.acceptInvitation, invitationToken: token, email };
    return (
      <div className="flex flex-col gap-4">
        <p className="text-sm">{t('accept.signInFirst')}</p>
        <Button asChild>
          <Link to={paths.login} state={state}>
            {t('accept.signIn')}
          </Link>
        </Button>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <p className="text-sm">{t('accept.signedInAs', { name: session.data.user.displayName })}</p>
      {accept.isError && <FormAlert tone="error">{acceptError(accept.error, t)}</FormAlert>}
      <Button
        disabled={accept.isPending}
        onClick={() =>
          accept.mutate(
            { data: { token, displayName: null, password: null } },
            {
              onSuccess: async () => {
                await resetSession(queryClient);
                await navigate(paths.organizations, { replace: true });
              },
            },
          )
        }
      >
        {t('accept.join')}
      </Button>
    </div>
  );
}

function acceptError(error: unknown, t: (key: string) => string): string {
  switch (statusOf(error)) {
    case 400:
      return t('password.rejected');
    case 403:
      return t('accept.wrongAccount');
    case 404:
      return t('accept.invalidLink');
    case 409:
      return t('accept.alreadyMember');
    default:
      return t('common.unexpectedError');
  }
}
