import {
  useEnablePlatformSessionMfa,
  useSetupPlatformSessionMfa,
} from '@shelter/api-client/hooks/platform';
import type { MfaSetupResponse } from '@shelter/api-client/model';
import { Button } from '@shelter/ui/components/button';
import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';
import { formatSharedKey } from '../../../lib/auth/mfa';
import { resetSession } from '../../../lib/auth/session';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { FormField } from '../../../lib/forms/FormField';
import { requiredText } from '../../../lib/forms/schemas';
import { useZodForm } from '../../../lib/forms/useZodForm';

const codeSchema = z.object({ code: requiredText() });

/**
 * TOTP enrollment: a new key (setup key + `otpauth://` link), confirmed by a code, then the recovery codes,
 * shown once. `onStart` runs once a key is issued; `onDone` when the user says they saved the codes. Starting is an explicit click: each
 * setup replaces the key and rotates the security stamp.
 */
export function MfaEnrollment({ onStart, onDone }: { onStart?: () => void; onDone: () => void }) {
  const { t } = useTranslation('platform');
  const queryClient = useQueryClient();
  const setup = useSetupPlatformSessionMfa();
  const enable = useEnablePlatformSessionMfa();
  const [key, setKey] = useState<MfaSetupResponse>();
  const form = useZodForm(codeSchema, { defaultValues: { code: '' } });
  const [recoveryCodes, setRecoveryCodes] = useState<string[]>();

  if (recoveryCodes) {
    return (
      <div className="flex flex-col gap-4">
        <FormAlert tone="info">{t('mfa.enabled')}</FormAlert>
        <h2 className="font-semibold">{t('mfa.recoveryCodes.title')}</h2>
        <p className="text-sm text-muted-foreground">{t('mfa.recoveryCodes.body')}</p>
        <ul
          aria-label={t('mfa.recoveryCodes.title')}
          className="grid grid-cols-2 gap-2 rounded-md bg-muted p-3 font-mono text-sm"
        >
          {recoveryCodes.map((recoveryCode) => (
            <li key={recoveryCode}>{recoveryCode}</li>
          ))}
        </ul>
        <Button onClick={onDone}>{t('mfa.recoveryCodes.saved')}</Button>
      </div>
    );
  }

  if (!key) {
    return (
      <div className="flex flex-col gap-4">
        <p className="text-sm text-muted-foreground">{t('mfa.intro')}</p>
        {setup.isError && <FormAlert tone="error">{t('mfa.setupFailed')}</FormAlert>}
        <Button
          disabled={setup.isPending}
          onClick={() =>
            setup.mutate(undefined, {
              onSuccess: (result) => {
                setKey(result);
                onStart?.();
              },
            })
          }
        >
          {t('mfa.start')}
        </Button>
      </div>
    );
  }

  const submit = form.handleSubmit((data) =>
    enable.mutate(
      { data },
      {
        onSuccess: (result) => {
          setRecoveryCodes(result.recoveryCodes);
          // The session is now signed in with MFA: permissions and enrollment state change.
          void resetSession(queryClient);
        },
      },
    ),
  );

  return (
    <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
      <ol className="flex list-decimal flex-col gap-3 pl-5 text-sm">
        <li>
          {t('mfa.step.scan')}{' '}
          <a href={key.authenticatorUri} className="underline underline-offset-4">
            {t('mfa.openApp')}
          </a>
        </li>
        <li className="flex flex-col gap-1">
          <span>{t('mfa.step.key')}</span>
          <code
            aria-label={t('mfa.setupKey')}
            className="rounded-md bg-muted px-2 py-1 font-mono break-all"
          >
            {formatSharedKey(key.sharedKey)}
          </code>
        </li>
        <li>{t('mfa.step.code')}</li>
      </ol>
      {enable.isError && <FormAlert tone="error">{t('mfa.invalidCode')}</FormAlert>}
      <FormField
        label={t('mfa.code')}
        inputMode="numeric"
        autoComplete="one-time-code"
        required
        error={form.formState.errors.code}
        {...form.register('code')}
      />
      <Button type="submit" disabled={enable.isPending}>
        {t('mfa.confirm')}
      </Button>
    </form>
  );
}
