import { useForgotPlatformSessionPassword } from '@shelter/api-client/hooks/platform';
import { Button } from '@shelter/ui/components/button';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { paths } from '../../../lib/auth/paths';
import { FormAlert } from '../components/FormAlert';
import { FormField } from '../components/FormField';

/** Asks for a reset link. The answer is the same whether or not the email has an account. */
export function ForgotPasswordPage() {
  const { t } = useTranslation('platform');
  const forgot = useForgotPlatformSessionPassword();
  const [email, setEmail] = useState('');

  const submit = (event: FormEvent) => {
    event.preventDefault();
    forgot.mutate({ data: { email: email.trim() } });
  };

  return (
    <>
      <h1 className="text-2xl font-semibold">{t('forgot.title')}</h1>
      {forgot.isSuccess ? (
        <FormAlert tone="info">{t('forgot.sent')}</FormAlert>
      ) : (
        <>
          <p className="text-sm text-muted-foreground">{t('forgot.intro')}</p>
          {forgot.isError && <FormAlert tone="error">{t('common.unexpectedError')}</FormAlert>}
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
            <Button type="submit" disabled={forgot.isPending || email.trim().length === 0}>
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
