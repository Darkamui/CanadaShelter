import { useForgotPlatformSessionPassword } from '@shelter/api-client/hooks/platform';
import { Button } from '@shelter/ui/components/button';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';
import { paths } from '../../../lib/auth/paths';
import { isRateLimited } from '../../../lib/auth/session';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { FormField } from '../../../lib/forms/FormField';
import { emailAddress } from '../../../lib/forms/schemas';
import { useZodForm } from '../../../lib/forms/useZodForm';
import { AuthBackLink, AuthFooter, AuthHeader } from '../../../lib/page/AuthParts';

const forgotSchema = z.object({ email: emailAddress() });

/** Asks for a reset link. The answer is the same whether or not the email has an account. */
export function ForgotPasswordPage() {
  const { t } = useTranslation('platform');
  const forgot = useForgotPlatformSessionPassword();
  const form = useZodForm(forgotSchema, { defaultValues: { email: '' } });

  const submit = form.handleSubmit((data) => forgot.mutate({ data }));

  return (
    <>
      <AuthHeader
        title={t('forgot.title')}
        description={forgot.isSuccess ? undefined : t('forgot.intro')}
      />
      {forgot.isSuccess ? (
        <FormAlert tone="info" title={t('forgot.sentTitle')}>
          {t('forgot.sent')}
        </FormAlert>
      ) : (
        <>
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
            <Button type="submit" size="lg" className="mt-1" disabled={forgot.isPending}>
              {t('forgot.submit')}
            </Button>
          </form>
        </>
      )}
      <AuthFooter>
        <AuthBackLink to={paths.login}>{t('common.backToLogin')}</AuthBackLink>
      </AuthFooter>
    </>
  );
}
