import { Badge } from '@shelter/ui/components/badge';
import { Card, CardAction, CardContent, CardHeader, CardTitle } from '@shelter/ui/components/card';
import { PageHeader } from '@shelter/ui/components/page-header';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSession } from '../../../lib/auth/session';
import { MfaEnrollment } from '../components/MfaEnrollment';

/**
 * The signed-in user's own security settings: optional MFA for roles that do not require it. `enrolling`
 * keeps the enrollment on screen after the session reports MFA on, so the recovery codes can be shown.
 */
export function SecurityPage() {
  const { t } = useTranslation('platform');
  const { data: session } = useSession();
  const [enrolling, setEnrolling] = useState(false);
  const mfaOn = session?.user.mfaEnabled === true;

  return (
    <div className="flex max-w-2xl flex-col gap-6">
      <PageHeader title={t('security.title')} description={t('security.description')} />
      <Card aria-labelledby="mfa-title">
        <CardHeader>
          <CardTitle id="mfa-title">{t('mfa.title')}</CardTitle>
          <CardAction>
            <Badge dot tone={mfaOn ? 'success' : 'neutral'}>
              {mfaOn ? t('security.on') : t('security.off')}
            </Badge>
          </CardAction>
        </CardHeader>
        <CardContent>
          {mfaOn && !enrolling ? (
            <p className="text-sm text-muted-foreground">{t('security.mfaOn')}</p>
          ) : (
            <div className="flex flex-col gap-4">
              {!enrolling && <p className="text-sm">{t('security.mfaOff')}</p>}
              <MfaEnrollment
                onStart={() => setEnrolling(true)}
                onDone={() => setEnrolling(false)}
              />
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
