import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSession } from '../../../lib/auth/session';
import { FormAlert } from '../components/FormAlert';
import { MfaEnrollment } from '../components/MfaEnrollment';

/**
 * The signed-in user's own security settings: optional MFA for roles that do not require it. `enrolling`
 * keeps the enrollment on screen after the session reports MFA on, so the recovery codes can be shown.
 */
export function SecurityPage() {
  const { t } = useTranslation('platform');
  const { data: session } = useSession();
  const [enrolling, setEnrolling] = useState(false);

  return (
    <section className="flex max-w-lg flex-col gap-4">
      <h1 className="text-2xl font-semibold">{t('security.title')}</h1>
      <h2 className="font-semibold">{t('mfa.title')}</h2>
      {session?.user.mfaEnabled && !enrolling ? (
        <FormAlert tone="info">{t('security.mfaOn')}</FormAlert>
      ) : (
        <>
          {!enrolling && <p className="text-sm text-muted-foreground">{t('security.mfaOff')}</p>}
          <MfaEnrollment onStart={() => setEnrolling(true)} onDone={() => setEnrolling(false)} />
        </>
      )}
    </section>
  );
}
