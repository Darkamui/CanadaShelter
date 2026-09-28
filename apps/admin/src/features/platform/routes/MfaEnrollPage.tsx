import { useTranslation } from 'react-i18next';
import { useLocation, useNavigate } from 'react-router';
import { afterSignIn, readAuthState } from '../../../lib/auth/navigation';
import { LogoutButton } from '../components/LogoutButton';
import { MfaEnrollment } from '../components/MfaEnrollment';

/** Forced enrollment: the role requires MFA, and the server refuses everything else until it is on. */
export function MfaEnrollPage() {
  const { t } = useTranslation('platform');
  const location = useLocation();
  const navigate = useNavigate();

  return (
    <>
      <h1 className="text-2xl font-semibold">{t('mfa.required.title')}</h1>
      <p className="text-sm">{t('mfa.required.body')}</p>
      <MfaEnrollment
        onDone={() => void navigate(afterSignIn(readAuthState(location.state)), { replace: true })}
      />
      <div>
        <LogoutButton />
      </div>
    </>
  );
}
