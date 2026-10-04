import { useSelectPlatformSessionOrganization } from '@shelter/api-client/hooks/platform';
import { Avatar, initialsOf } from '@shelter/ui/components/avatar';
import { Badge } from '@shelter/ui/components/badge';
import { useQueryClient } from '@tanstack/react-query';
import { ChevronRight } from 'lucide-react';
import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation, useNavigate } from 'react-router';
import { afterSignIn, readAuthState } from '../../../lib/auth/navigation';
import { resetSession, useSession } from '../../../lib/auth/session';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { AuthFooter, AuthHeader } from '../../../lib/page/AuthParts';
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
      <AuthHeader
        title={t('organizations.title')}
        description={memberships.length === 0 ? undefined : t('organizations.intro')}
      />
      {select.isError && <FormAlert tone="error">{t('organizations.selectFailed')}</FormAlert>}
      {memberships.length === 0 ? (
        <FormAlert tone="info">{t('organizations.none')}</FormAlert>
      ) : (
        <ul className="flex flex-col overflow-hidden rounded-xl border">
          {memberships.map((membership) => {
            const active = membership.organizationId === session?.activeOrganizationId;
            return (
              <li key={membership.organizationId} className="border-b last:border-b-0">
                <button
                  type="button"
                  className="group flex w-full items-center gap-3 px-4 py-3 text-left transition-colors outline-none hover:bg-subtle focus-visible:bg-subtle focus-visible:ring-[3px] focus-visible:ring-ring/45 focus-visible:ring-inset disabled:pointer-events-none disabled:opacity-60"
                  aria-current={active ? 'true' : undefined}
                  disabled={select.isPending}
                  onClick={() => choose(membership.organizationId)}
                >
                  <Avatar>{initialsOf(membership.organizationName)}</Avatar>
                  <span className="min-w-0 flex-1 truncate font-medium">
                    {membership.organizationName}
                  </span>
                  {active && <Badge tone="primary">{t('organizations.current')}</Badge>}
                  <ChevronRight
                    aria-hidden="true"
                    className="size-4 shrink-0 text-muted-foreground transition-transform group-hover:translate-x-0.5"
                  />
                </button>
              </li>
            );
          })}
        </ul>
      )}
      <AuthFooter>
        <LogoutButton className="-ml-3 text-muted-foreground" />
      </AuthFooter>
    </>
  );
}
