import {
  lookupPlatformInvitation,
  useAcceptPlatformInvitation,
} from '@shelter/api-client/hooks/platform';
import { Button } from '@shelter/ui/components/button';
import { Skeleton } from '@shelter/ui/components/skeleton';
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
import { isRateLimited, resetSession, statusOf, useSession } from '../../../lib/auth/session';
import { useLinkFragment } from '../../../lib/auth/useLinkFragment';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { FormField } from '../../../lib/forms/FormField';
import { requiredText } from '../../../lib/forms/schemas';
import { applyValidationErrors, validationErrors } from '../../../lib/forms/serverErrors';
import { useZodForm } from '../../../lib/forms/useZodForm';
import { AuthBackLink, AuthFooter, AuthHeader } from '../../../lib/page/AuthParts';

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
        <AuthHeader title={t('accept.title')} />
        <FormAlert tone="error">
          {!token || statusOf(lookup.error) === 404
            ? t('accept.invalidLink')
            : isRateLimited(lookup.error)
              ? t('common.tooManyAttempts')
              : t('common.unexpectedError')}
        </FormAlert>
        <AuthFooter>
          <AuthBackLink to={paths.login}>{t('common.backToLogin')}</AuthBackLink>
        </AuthFooter>
      </>
    );
  }

  if (!lookup.data) {
    return <AuthSkeleton label={t('common.loading')} />;
  }

  const invitation = lookup.data;
  return (
    <>
      <AuthHeader
        title={t('accept.heading', { organization: invitation.organizationName })}
        description={t('accept.for', { email: invitation.email })}
      />
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
      <Button type="submit" size="lg" className="mt-1" disabled={accept.isPending}>
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
    return <AuthSkeleton label={t('common.loading')} />;
  }

  if (!session.data) {
    // Come back here after signing in; the secret travels in router state, never in a URL.
    const state: AuthState = { from: paths.acceptInvitation, invitationToken: token, email };
    return (
      <div className="flex flex-col gap-4">
        <p className="text-sm">{t('accept.signInFirst')}</p>
        <Button asChild size="lg">
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
        size="lg"
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

/** Stands in for the card's content while the invitation or the session loads. */
function AuthSkeleton({ label }: { label: string }) {
  return (
    <div role="status" aria-label={label} className="flex flex-col gap-4">
      <Skeleton className="h-8 w-3/4" />
      <Skeleton className="h-4 w-1/2" />
      <Skeleton className="mt-2 h-11 w-full" />
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
    case 429:
      return t('common.tooManyAttempts');
    default:
      return t('common.unexpectedError');
  }
}
