import { useResetPlatformSessionPassword } from '@shelter/api-client/hooks/platform';
import { Button } from '@shelter/ui/components/button';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router';
import type { AuthState } from '../../../lib/auth/navigation';
import { paths } from '../../../lib/auth/paths';
import { MIN_PASSWORD_LENGTH, passwordProblem } from '../../../lib/auth/passwords';
import { useLinkFragment } from '../../../lib/auth/useLinkFragment';
import { FormAlert } from '../components/FormAlert';
import { FormField } from '../components/FormField';

/** From the emailed link `/reset-password#user={id}&token={token}`. */
export function ResetPasswordPage() {
  const { t } = useTranslation('platform');
  const navigate = useNavigate();
  const fragment = useLinkFragment();
  const [userId] = useState(() => fragment.get('user'));
  const [token] = useState(() => fragment.get('token'));
  const reset = useResetPlatformSessionPassword();
  const [password, setPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [submitted, setSubmitted] = useState(false);

  const problem = passwordProblem(password, confirmation);
  const linkRejected = reset.isError && !hasValidationErrors(reset.error.problem);

  if (!userId || !token || linkRejected) {
    return (
      <>
        <h1 className="text-2xl font-semibold">{t('reset.title')}</h1>
        <FormAlert tone="error">{t('reset.invalidLink')}</FormAlert>
        <Link to={paths.forgotPassword} className="text-sm underline underline-offset-4">
          {t('reset.requestNew')}
        </Link>
      </>
    );
  }

  const submit = (event: FormEvent) => {
    event.preventDefault();
    setSubmitted(true);
    if (problem) return;
    reset.mutate(
      { data: { userId, token, newPassword: password } },
      {
        onSuccess: () => {
          const state: AuthState = { notice: 'passwordReset' };
          void navigate(paths.login, { replace: true, state });
        },
      },
    );
  };

  return (
    <>
      <h1 className="text-2xl font-semibold">{t('reset.title')}</h1>
      {reset.isError && <FormAlert tone="error">{t('password.rejected')}</FormAlert>}
      <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
        <FormField
          label={t('fields.newPassword')}
          name="newPassword"
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
        <Button type="submit" disabled={reset.isPending}>
          {t('reset.submit')}
        </Button>
      </form>
    </>
  );
}

function hasValidationErrors(problem: unknown): boolean {
  return typeof problem === 'object' && problem !== null && 'errors' in problem;
}
