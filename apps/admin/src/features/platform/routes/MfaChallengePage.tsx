import { useLoginPlatformSessionMfa } from '@shelter/api-client/hooks/platform';
import { Button } from '@shelter/ui/components/button';
import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation, useNavigate } from 'react-router';
import { z } from 'zod';
import { afterSignIn, readAuthState, stateAfterSignIn } from '../../../lib/auth/navigation';
import { paths } from '../../../lib/auth/paths';
import { isRateLimited, resetSession } from '../../../lib/auth/session';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { FormField } from '../../../lib/forms/FormField';
import { requiredText } from '../../../lib/forms/schemas';
import { useZodForm } from '../../../lib/forms/useZodForm';
import { AuthBackLink, AuthFooter, AuthHeader } from '../../../lib/page/AuthParts';

const codeSchema = z.object({ code: requiredText() });

/** Second login step: an authenticator code, or a recovery code. */
export function MfaChallengePage() {
  const { t } = useTranslation('platform');
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const state = readAuthState(location.state);
  const verify = useLoginPlatformSessionMfa();
  const [useRecovery, setUseRecovery] = useState(false);
  const form = useZodForm(codeSchema, { defaultValues: { code: '' } });

  const submit = form.handleSubmit(({ code }) =>
    verify.mutate(
      { data: useRecovery ? { code: null, recoveryCode: code } : { code, recoveryCode: null } },
      {
        onSuccess: async () => {
          await resetSession(queryClient);
          await navigate(afterSignIn(state), { replace: true, state: stateAfterSignIn(state) });
        },
      },
    ),
  );

  return (
    <>
      <AuthHeader
        title={t('mfaChallenge.title')}
        description={useRecovery ? t('mfaChallenge.recoveryIntro') : t('mfaChallenge.intro')}
      />
      {verify.isError && (
        <FormAlert tone="error">
          {isRateLimited(verify.error) ? t('common.tooManyAttempts') : t('mfaChallenge.invalid')}
        </FormAlert>
      )}
      <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
        <FormField
          key={useRecovery ? 'recovery' : 'code'}
          label={useRecovery ? t('mfaChallenge.recoveryCode') : t('mfa.code')}
          inputMode={useRecovery ? 'text' : 'numeric'}
          autoComplete="one-time-code"
          autoFocus
          required
          error={form.formState.errors.code}
          {...form.register('code')}
        />
        <Button type="submit" size="lg" className="mt-1" disabled={verify.isPending}>
          {t('mfaChallenge.submit')}
        </Button>
      </form>
      <Button
        variant="link"
        className="-mt-3 self-start px-0"
        onClick={() => {
          setUseRecovery(!useRecovery);
          form.reset();
          verify.reset();
        }}
      >
        {useRecovery ? t('mfaChallenge.useCode') : t('mfaChallenge.useRecovery')}
      </Button>
      <AuthFooter>
        <AuthBackLink to={paths.login}>{t('mfaChallenge.backToLogin')}</AuthBackLink>
      </AuthFooter>
    </>
  );
}
