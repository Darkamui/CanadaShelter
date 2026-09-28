import { useSelectPlatformSessionOrganization } from '@shelter/api-client/hooks/platform';
import { Button } from '@shelter/ui/components/button';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation, useNavigate } from 'react-router';
import { afterSignIn, readAuthState } from '../../../lib/auth/navigation';
import { resetSession, useSession } from '../../../lib/auth/session';
import { FormAlert } from '../components/FormAlert';
import { LogoutButton } from '../components/LogoutButton';

/** Chooses the session's organization. With a single membership and none active, it is chosen for the user. */
export function OrganizationPickerPage() {
  const { t } = useTranslation('platform');
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { data: session } = useSession();
  const select = useSelectPlatformSessionOrganization();
  const autoSelected = useRef(false);

  const choose = (organizationId: string) =>
    select.mutate(
      { organizationId },
      {
        onSuccess: async () => {
          // Every cached response belonged to the previous organization.
          await resetSession(queryClient);
          await navigate(afterSignIn(readAuthState(location.state)), { replace: true });
        },
      },
    );

  const memberships = session?.memberships ?? [];
  const only = memberships.length === 1 && !session?.activeOrganizationId ? memberships[0] : null;

  useEffect(() => {
    if (only && !autoSelected.current) {
      autoSelected.current = true;
      choose(only.organizationId);
    }
  });

  return (
    <>
      <h1 className="text-2xl font-semibold">{t('organizations.title')}</h1>
      {select.isError && <FormAlert tone="error">{t('organizations.selectFailed')}</FormAlert>}
      {memberships.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t('organizations.none')}</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {memberships.map((membership) => {
            const active = membership.organizationId === session?.activeOrganizationId;
            return (
              <li key={membership.organizationId}>
                <Button
                  variant={active ? 'default' : 'outline'}
                  className="w-full justify-between"
                  aria-current={active ? 'true' : undefined}
                  disabled={select.isPending}
                  onClick={() => choose(membership.organizationId)}
                >
                  {membership.organizationName}
                  {active && <span className="text-xs">{t('organizations.current')}</span>}
                </Button>
              </li>
            );
          })}
        </ul>
      )}
      <div>
        <LogoutButton />
      </div>
    </>
  );
}
