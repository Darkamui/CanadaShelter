import { useLoginPlatformSessionMfa } from '@shelter/api-client/hooks/platform';
import { Button } from '@shelter/ui/components/button';
import { useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useLocation, useNavigate } from 'react-router';
import { afterSignIn, readAuthState, stateAfterSignIn } from '../../../lib/auth/navigation';
import { paths } from '../../../lib/auth/paths';
import { resetSession } from '../../../lib/auth/session';
import { FormAlert } from '../components/FormAlert';
import { FormField } from '../components/FormField';

/** Second login step: an authenticator code, or a recovery code. */
export function MfaChallengePage() {
  const { t } = useTranslation('platform');
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const state = readAuthState(location.state);
  const verify = useLoginPlatformSessionMfa();
  const [useRecovery, setUseRecovery] = useState(false);
  const [code, setCode] = useState('');

  const submit = (event: FormEvent) => {
    event.preventDefault();
    const value = code.trim();
    verify.mutate(
      {
        data: useRecovery
          ? { code: null, recoveryCode: value }
          : { code: value, recoveryCode: null },
      },
      {
        onSuccess: async () => {
          await resetSession(queryClient);
          await navigate(afterSignIn(state), { replace: true, state: stateAfterSignIn(state) });
        },
      },
    );
  };

  return (
    <>
      <h1 className="text-2xl font-semibold">{t('mfaChallenge.title')}</h1>
      <p className="text-sm text-muted-foreground">
        {useRecovery ? t('mfaChallenge.recoveryIntro') : t('mfaChallenge.intro')}
      </p>
      {verify.isError && (
        <FormAlert tone="error">
          {t('mfaChallenge.invalid')}{' '}
          <Link to={paths.login} className="underline underline-offset-4">
            {t('mfaChallenge.backToLogin')}
          </Link>
        </FormAlert>
      )}
      <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
        <FormField
          key={useRecovery ? 'recovery' : 'code'}
          label={useRecovery ? t('mfaChallenge.recoveryCode') : t('mfa.code')}
          name="code"
          inputMode={useRecovery ? 'text' : 'numeric'}
          autoComplete="one-time-code"
          autoFocus
          required
          value={code}
          onChange={(event) => setCode(event.target.value)}
        />
        <Button type="submit" disabled={verify.isPending || code.trim().length === 0}>
          {t('mfaChallenge.submit')}
        </Button>
      </form>
      <Button
        variant="link"
        className="self-start px-0"
        onClick={() => {
          setUseRecovery(!useRecovery);
          setCode('');
          verify.reset();
        }}
      >
        {useRecovery ? t('mfaChallenge.useCode') : t('mfaChallenge.useRecovery')}
      </Button>
    </>
  );
}
