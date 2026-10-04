import type { SessionResponse } from '@shelter/api-client/model';
import { Avatar, initialsOf } from '@shelter/ui/components/avatar';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@shelter/ui/components/dropdown-menu';
import { ChevronDown, LogOut, ShieldCheck } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { paths } from '../../../lib/auth/paths';
import { useLogout } from './useLogout';

/** The signed-in user: account security and sign-out. */
export function UserMenu({ session }: { session: SessionResponse }) {
  const { t } = useTranslation('platform');
  const { logout, isPending } = useLogout();
  const name = session.user.displayName;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        aria-label={`${t('shell:account.label')} (${name})`}
        className="flex h-10 items-center gap-2 rounded-full py-1 pr-2 pl-1 text-sm outline-none hover:bg-accent focus-visible:ring-[3px] focus-visible:ring-ring/45 data-[state=open]:bg-accent"
      >
        <Avatar size="sm">{initialsOf(name)}</Avatar>
        <span className="hidden max-w-40 truncate font-medium md:inline">{name}</span>
        <ChevronDown aria-hidden="true" className="size-4 text-muted-foreground" />
      </DropdownMenuTrigger>
      <DropdownMenuContent>
        <DropdownMenuLabel className="flex flex-col gap-0.5 pb-2">
          <span className="text-sm font-medium text-foreground">{name}</span>
          {t('shell:account.label')}
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild>
          <Link to={paths.security}>
            <ShieldCheck aria-hidden="true" />
            {t('shell:account.security')}
          </Link>
        </DropdownMenuItem>
        <DropdownMenuItem tone="danger" disabled={isPending} onSelect={logout}>
          <LogOut aria-hidden="true" />
          {t('logout')}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
