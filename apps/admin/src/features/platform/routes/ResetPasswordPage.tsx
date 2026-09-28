import { useResetPlatformSessionPassword } from '@shelter/api-client/hooks/platform';
import { Button } from '@shelter/ui/components/button';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router';
import { z } from 'zod';
import type { AuthState } from '../../../lib/auth/navigation';
import { paths } from '../../../lib/auth/paths';
import { isRateLimited } from '../../../lib/auth/session';
import {
  MIN_PASSWORD_LENGTH,
  newPasswordFields,
  passwordsMatch,
} from '../../../lib/auth/passwords';
import { useLinkFragment } from '../../../lib/auth/useLinkFragment';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { FormField } from '../../../lib/forms/FormField';
import { applyValidationErrors, validationErrors } from '../../../lib/forms/serverErrors';
import { useZodForm } from '../../../lib/forms/useZodForm';

const resetSchema = z.object(newPasswordFields).refine(passwordsMatch.check, passwordsMatch.params);

/** From the emailed link `/reset-password#user={id}&token={token}`. */
export function ResetPasswordPage() {
  const { t } = useTranslation('platform');
  const navigate = useNavigate();
  const fragment = useLinkFragment();
  const [userId] = useState(() => fragment.get('user'));
  const [token] = useState(() => fragment.get('token'));
  const reset = useResetPlatformSessionPassword();
  const form = useZodForm(resetSchema, { defaultValues: { password: '', confirmation: '' } });
  const { errors } = form.formState;

  // A 400 without field errors means the link itself was refused (expired, used, or tampered with).
  const rateLimited = isRateLimited(reset.error);
  const linkRejected = reset.isError && !rateLimited && !validationErrors(reset.error);

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

  const submit = form.handleSubmit(({ password }) =>
    reset.mutate(
      { data: { userId, token, newPassword: password } },
      {
        onSuccess: () => {
          const state: AuthState = { notice: 'passwordReset' };
          void navigate(paths.login, { replace: true, state });
        },
        onError: (error) =>
          applyValidationErrors(error, form.setError, {
            newPassword: ['password', 'platform:password.rejected'],
          }),
      },
    ),
  );

  return (
    <>
      <h1 className="text-2xl font-semibold">{t('reset.title')}</h1>
      {rateLimited && <FormAlert tone="error">{t('common.tooManyAttempts')}</FormAlert>}
      <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
        <FormField
          label={t('fields.newPassword')}
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
        <Button type="submit" disabled={reset.isPending}>
          {t('reset.submit')}
        </Button>
      </form>
    </>
  );
}
