import {
  useEnablePlatformSessionMfa,
  useSetupPlatformSessionMfa,
} from '@shelter/api-client/hooks/platform';
import type { MfaSetupResponse } from '@shelter/api-client/model';
import { Alert } from '@shelter/ui/components/alert';
import { Button } from '@shelter/ui/components/button';
import { toast } from '@shelter/ui/components/toaster';
import { useQueryClient } from '@tanstack/react-query';
import { Copy, ExternalLink, ShieldCheck } from 'lucide-react';
import { useState, type ReactNode } from 'react';
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
    const copy = async () => {
      try {
        await navigator.clipboard.writeText(recoveryCodes.join('\n'));
        toast.success(t('mfa.recoveryCodes.copied'));
      } catch {
        toast.error(t('mfa.recoveryCodes.copyFailed'));
      }
    };
    return (
      <div className="flex flex-col gap-4">
        <Alert tone="success">{t('mfa.enabled')}</Alert>
        <div className="flex flex-col gap-1">
          <h2 className="font-semibold">{t('mfa.recoveryCodes.title')}</h2>
          <p className="text-sm text-muted-foreground">{t('mfa.recoveryCodes.body')}</p>
        </div>
        <ul
          aria-label={t('mfa.recoveryCodes.title')}
          className="grid grid-cols-2 gap-x-6 gap-y-2 rounded-lg border bg-subtle px-4 py-3 font-mono text-sm tracking-wide"
        >
          {recoveryCodes.map((recoveryCode) => (
            <li key={recoveryCode}>{recoveryCode}</li>
          ))}
        </ul>
        <div className="flex flex-wrap gap-2">
          <Button onClick={onDone}>{t('mfa.recoveryCodes.saved')}</Button>
          <Button variant="outline" onClick={() => void copy()}>
            <Copy aria-hidden="true" />
            {t('mfa.recoveryCodes.copy')}
          </Button>
        </div>
      </div>
    );
  }

  if (!key) {
    return (
      <div className="flex flex-col items-start gap-4">
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
          <ShieldCheck aria-hidden="true" />
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
    <form className="flex flex-col gap-5" onSubmit={submit} noValidate>
      <ol className="flex flex-col gap-4 text-sm">
        <Step n={1}>
          <span>{t('mfa.step.scan')}</span>
          <Button asChild variant="outline" size="sm" className="self-start">
            <a href={key.authenticatorUri}>
              <ExternalLink aria-hidden="true" />
              {t('mfa.openApp')}
            </a>
          </Button>
        </Step>
        <Step n={2}>
          <span>{t('mfa.step.key')}</span>
          <code
            aria-label={t('mfa.setupKey')}
            className="self-start rounded-lg border bg-subtle px-3 py-2 font-mono text-[0.9375rem] tracking-wider break-all"
          >
            {formatSharedKey(key.sharedKey)}
          </code>
        </Step>
        <Step n={3}>
          <span>{t('mfa.step.code')}</span>
        </Step>
      </ol>
      {enable.isError && <FormAlert tone="error">{t('mfa.invalidCode')}</FormAlert>}
      <FormField
        label={t('mfa.code')}
        inputMode="numeric"
        autoComplete="one-time-code"
        required
        className="max-w-56"
        error={form.formState.errors.code}
        {...form.register('code')}
      />
      <Button type="submit" className="self-start" disabled={enable.isPending}>
        {t('mfa.confirm')}
      </Button>
    </form>
  );
}

/** One numbered setup step: the steps are a real sequence. */
function Step({ n, children }: { n: number; children: ReactNode }) {
  return (
    <li className="flex gap-3">
      <span
        aria-hidden="true"
        className="tabular flex size-6 shrink-0 items-center justify-center rounded-full bg-secondary text-xs font-semibold text-secondary-foreground"
      >
        {n}
      </span>
      <div className="flex min-w-0 flex-col gap-2 pt-0.5">{children}</div>
    </li>
  );
}
