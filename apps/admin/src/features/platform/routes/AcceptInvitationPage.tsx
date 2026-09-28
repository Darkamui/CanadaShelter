import {
  lookupPlatformInvitation,
  useAcceptPlatformInvitation,
} from '@shelter/api-client/hooks/platform';
import { Button } from '@shelter/ui/components/button';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useLocation, useNavigate } from 'react-router';
import { readAuthState, type AuthState } from '../../../lib/auth/navigation';
import { paths } from '../../../lib/auth/paths';
import { MIN_PASSWORD_LENGTH, passwordProblem } from '../../../lib/auth/passwords';
import { resetSession, statusOf, useSession } from '../../../lib/auth/session';
import { useLinkFragment } from '../../../lib/auth/useLinkFragment';
import { FormAlert } from '../components/FormAlert';
import { FormField } from '../components/FormField';

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

function NewAccount({ token, email }: { token: string; email: string }) {
  const { t } = useTranslation('platform');
  const navigate = useNavigate();
  const accept = useAcceptPlatformInvitation();
  const [displayName, setDisplayName] = useState('');
  const [password, setPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [submitted, setSubmitted] = useState(false);

  const nameMissing = displayName.trim().length === 0;
  const problem = passwordProblem(password, confirmation);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    setSubmitted(true);
    if (nameMissing || problem) return;
    accept.mutate(
      { data: { token, displayName: displayName.trim(), password } },
      {
        onSuccess: () => {
          const state: AuthState = { notice: 'accountCreated', email };
          void navigate(paths.login, { replace: true, state });
        },
      },
    );
  };

  return (
    <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
      <p className="text-sm">{t('accept.newAccount')}</p>
      {accept.isError && <FormAlert tone="error">{acceptError(accept.error, t)}</FormAlert>}
      <FormField
        label={t('fields.displayName')}
        name="displayName"
        autoComplete="name"
        required
        maxLength={200}
        error={submitted && nameMissing ? t('accept.nameRequired') : undefined}
        value={displayName}
        onChange={(event) => setDisplayName(event.target.value)}
      />
      <FormField
        label={t('fields.password')}
        name="password"
        type="password"
        autoComplete="new-password"
        required
        minLength={MIN_PASSWORD_LENGTH}
        hint={t('password.hint', { count: MIN_PASSWORD_LENGTH })}
        error={submitted && problem === 'tooShort' ? t('password.tooShort') : undefined}
        value={password}
        onChange={(event) => setPassword(event.target.value)}
      />
      <FormField
        label={t('fields.confirmPassword')}
        name="confirmPassword"
        type="password"
        autoComplete="new-password"
        required
        error={submitted && problem === 'mismatch' ? t('password.mismatch') : undefined}
        value={confirmation}
        onChange={(event) => setConfirmation(event.target.value)}
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
