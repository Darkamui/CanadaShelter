import { useSelectPlatformSessionOrganization } from '@shelter/api-client/hooks/platform';
import type { SessionResponse } from '@shelter/api-client/model';
import { toast } from '@shelter/ui/components/toaster';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from '@shelter/ui/components/dropdown-menu';
import { useQueryClient } from '@tanstack/react-query';
import { Building2, ChevronsUpDown } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { activeOrganization, resetSession } from '../../../lib/auth/session';

const nameClass = 'truncate font-display text-[1.0625rem] font-semibold tracking-[-0.01em]';

/**
 * The open organization, in the header. With several memberships it is a menu that switches organization in place:
 * every cached response belonged to the previous one, so the session is reset and the user lands on home.
 */
export function OrganizationSwitcher({ session }: { session: SessionResponse }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const select = useSelectPlatformSessionOrganization();
  const current = activeOrganization(session);

  if (session.memberships.length < 2) {
    return (
      <span className="flex min-w-0 items-center gap-2.5">
        <Building2 aria-hidden="true" className="size-[1.125rem] shrink-0 text-muted-foreground" />
        <span className={nameClass}>{current?.organizationName}</span>
      </span>
    );
  }

  const choose = (organizationId: string) => {
    if (organizationId === session.activeOrganizationId) return;
    select.mutate(
      { organizationId },
      {
        onSuccess: async () => {
          await resetSession(queryClient);
          await navigate('/');
        },
        onError: () => toast.error(t('account.switchFailed')),
      },
    );
  };

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        disabled={select.isPending}
        aria-label={`${t('account.switchOrganization')} (${current?.organizationName ?? ''})`}
        className="-ml-2 flex h-10 min-w-0 items-center gap-2.5 rounded-md px-2 outline-none hover:bg-accent focus-visible:ring-[3px] focus-visible:ring-ring/45 data-[state=open]:bg-accent"
      >
        <Building2 aria-hidden="true" className="size-[1.125rem] shrink-0 text-muted-foreground" />
        <span className={nameClass}>{current?.organizationName}</span>
        <ChevronsUpDown aria-hidden="true" className="size-4 shrink-0 text-muted-foreground" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="min-w-64">
        <DropdownMenuLabel>{t('account.switchOrganization')}</DropdownMenuLabel>
        <DropdownMenuRadioGroup value={session.activeOrganizationId ?? ''} onValueChange={choose}>
          {session.memberships.map((membership) => (
            <DropdownMenuRadioItem
              key={membership.organizationId}
              value={membership.organizationId}
            >
              <Building2 aria-hidden="true" />
              {membership.organizationName}
            </DropdownMenuRadioItem>
          ))}
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
