import { useForgotPlatformSessionPassword } from '@shelter/api-client/hooks/platform';
import { Button } from '@shelter/ui/components/button';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { z } from 'zod';
import { paths } from '../../../lib/auth/paths';
import { isRateLimited } from '../../../lib/auth/session';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { FormField } from '../../../lib/forms/FormField';
import { emailAddress } from '../../../lib/forms/schemas';
import { useZodForm } from '../../../lib/forms/useZodForm';

const forgotSchema = z.object({ email: emailAddress() });

/** Asks for a reset link. The answer is the same whether or not the email has an account. */
export function ForgotPasswordPage() {
  const { t } = useTranslation('platform');
  const forgot = useForgotPlatformSessionPassword();
  const form = useZodForm(forgotSchema, { defaultValues: { email: '' } });

  const submit = form.handleSubmit((data) => forgot.mutate({ data }));

  return (
    <>
      <h1 className="text-2xl font-semibold">{t('forgot.title')}</h1>
      {forgot.isSuccess ? (
        <FormAlert tone="info">{t('forgot.sent')}</FormAlert>
      ) : (
        <>
          <p className="text-sm text-muted-foreground">{t('forgot.intro')}</p>
          {forgot.isError && (
            <FormAlert tone="error">
              {isRateLimited(forgot.error)
                ? t('common.tooManyAttempts')
                : t('common.unexpectedError')}
            </FormAlert>
          )}
          <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
            <FormField
              label={t('fields.email')}
              type="email"
              autoComplete="username"
              required
              error={form.formState.errors.email}
              {...form.register('email')}
            />
            <Button type="submit" disabled={forgot.isPending}>
              {t('forgot.submit')}
            </Button>
          </form>
        </>
      )}
      <Link to={paths.login} className="text-sm underline underline-offset-4">
        {t('common.backToLogin')}
      </Link>
    </>
  );
}
