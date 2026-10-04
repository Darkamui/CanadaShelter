import type { SessionResponse } from '@shelter/api-client/model';
import { Button } from '@shelter/ui/components/button';
import { Sheet, SheetContent, SheetTitle, SheetTrigger } from '@shelter/ui/components/sheet';
import { Menu } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { OrganizationSwitcher } from '../../features/platform/components/OrganizationSwitcher';
import { PingStatus } from '../../features/platform/components/PingStatus';
import { UserMenu } from '../../features/platform/components/UserMenu';
import { LanguageSwitcher } from './LanguageSwitcher';
import { SidebarBrand, SidebarNav } from './Sidebar';

export function Header({ session }: { session: SessionResponse }) {
  const { t } = useTranslation();
  const [navOpen, setNavOpen] = useState(false);

  return (
    <header className="sticky top-0 z-30 flex h-16 shrink-0 items-center gap-3 border-b bg-background/90 px-4 backdrop-blur supports-[backdrop-filter]:bg-background/75 sm:px-6 lg:px-8">
      <Sheet open={navOpen} onOpenChange={setNavOpen}>
        <SheetTrigger asChild>
          <Button
            variant="ghost"
            size="icon"
            className="-ml-2 lg:hidden"
            aria-label={t('nav.open')}
          >
            <Menu aria-hidden="true" className="size-5" />
          </Button>
        </SheetTrigger>
        <SheetContent
          closeLabel={t('close')}
          aria-describedby={undefined}
          className="bg-sidebar text-sidebar-foreground"
        >
          <SheetTitle className="sr-only">{t('nav.label')}</SheetTitle>
          <div className="px-2">
            <SidebarBrand />
          </div>
          <div className="flex-1 overflow-y-auto pb-4">
            <SidebarNav permissions={session.permissions} onNavigate={() => setNavOpen(false)} />
          </div>
        </SheetContent>
      </Sheet>
      <OrganizationSwitcher session={session} />
      <div className="ml-auto flex items-center gap-1 sm:gap-2">
        <PingStatus />
        <LanguageSwitcher />
        <span aria-hidden="true" className="mx-1 h-6 w-px bg-border" />
        <UserMenu session={session} />
      </div>
    </header>
  );
}
