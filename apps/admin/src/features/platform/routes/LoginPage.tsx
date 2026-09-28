import { useLoginPlatformSession } from '@shelter/api-client/hooks/platform';
import { Button } from '@shelter/ui/components/button';
import { useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useLocation, useNavigate } from 'react-router';
import { afterSignIn, readAuthState, stateAfterSignIn } from '../../../lib/auth/navigation';
import { paths } from '../../../lib/auth/paths';
import { isUnauthorized, resetSession } from '../../../lib/auth/session';
import { FormAlert } from '../components/FormAlert';
import { FormField } from '../components/FormField';

export function LoginPage() {
  const { t } = useTranslation('platform');
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const state = readAuthState(location.state);
  const login = useLoginPlatformSession();
  const [email, setEmail] = useState(state.email ?? '');
  const [password, setPassword] = useState('');

  const submit = (event: FormEvent) => {
    event.preventDefault();
    login.mutate(
      { data: { email: email.trim(), password } },
      {
        onSuccess: async (result) => {
          if (result.status === 'mfaRequired') {
            // Keep where to go for after the second step.
            await navigate(paths.loginMfa, {
              state: { from: state.from, invitationToken: state.invitationToken },
            });
            return;
          }
          await resetSession(queryClient);
          await navigate(afterSignIn(state), { replace: true, state: stateAfterSignIn(state) });
        },
      },
    );
  };

  return (
    <>
      <h1 className="text-2xl font-semibold">{t('login.title')}</h1>
      {state.notice && !login.isError && (
        <FormAlert tone="info">{t(`login.notice.${state.notice}`)}</FormAlert>
      )}
      {login.isError && (
        <FormAlert tone="error">
          {isUnauthorized(login.error) ? t('login.invalid') : t('common.unexpectedError')}
        </FormAlert>
      )}
      <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
        <FormField
          label={t('fields.email')}
          name="email"
          type="email"
          autoComplete="username"
          required
          value={email}
          onChange={(event) => setEmail(event.target.value)}
        />
        <FormField
          label={t('fields.password')}
          name="password"
          type="password"
          autoComplete="current-password"
          required
          value={password}
          onChange={(event) => setPassword(event.target.value)}
        />
        <Button type="submit" disabled={login.isPending}>
          {t('login.submit')}
        </Button>
      </form>
      <Link to={paths.forgotPassword} className="text-sm underline underline-offset-4">
        {t('login.forgot')}
      </Link>
    </>
  );
}
