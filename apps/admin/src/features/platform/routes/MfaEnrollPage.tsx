import { useTranslation } from 'react-i18next';
import { useLocation, useNavigate } from 'react-router';
import { afterSignIn, readAuthState } from '../../../lib/auth/navigation';
import { AuthFooter, AuthHeader } from '../../../lib/page/AuthParts';
import { LogoutButton } from '../components/LogoutButton';
import { MfaEnrollment } from '../components/MfaEnrollment';

/** Forced enrollment: the role requires MFA, and the server refuses everything else until it is on. */
export function MfaEnrollPage() {
  const { t } = useTranslation('platform');
  const location = useLocation();
  const navigate = useNavigate();

  return (
    <>
      <AuthHeader title={t('mfa.required.title')} description={t('mfa.required.body')} />
      <MfaEnrollment
        onDone={() => void navigate(afterSignIn(readAuthState(location.state)), { replace: true })}
      />
      <AuthFooter>
        <LogoutButton className="-ml-3 text-muted-foreground" />
      </AuthFooter>
    </>
  );
}
