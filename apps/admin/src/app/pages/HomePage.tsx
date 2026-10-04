import { Badge } from '@shelter/ui/components/badge';
import { Button } from '@shelter/ui/components/button';
import { PageHeader } from '@shelter/ui/components/page-header';
import { ChevronRight, MapPin, Plus, Search, UserPlus } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { Permissions } from '../../lib/auth/permissions';
import { hasPermission, useSession } from '../../lib/auth/session';
import { visibleModules } from '../modules';

const SHORTCUTS = [
  { key: 'newAnimal', to: '/animals/new', icon: Plus, permission: Permissions.animalWrite },
  { key: 'findAnimal', to: '/animals', icon: Search, permission: Permissions.animalRead },
  { key: 'newPerson', to: '/people/new', icon: UserPlus, permission: Permissions.personWrite },
  { key: 'locations', to: '/operations', icon: MapPin, permission: Permissions.locationRead },
] as const;

/** Where staff land: the day's shortcuts, then the modules they can open and the ones still to come. */
export function HomePage() {
  const { t } = useTranslation();
  const { data: session } = useSession();
  const firstName = session?.user.displayName.split(/\s+/)[0] ?? '';
  const shortcuts = SHORTCUTS.filter(({ permission }) => hasPermission(session, permission));
  const modules = visibleModules(session?.permissions ?? []);
  const ready = modules.filter((module) => module.ready);
  const upcoming = modules.filter((module) => !module.ready);

  return (
    <>
      <PageHeader title={t('home.title')} description={t('home.greeting', { name: firstName })} />

      {shortcuts.length > 0 && (
        <section aria-labelledby="home-shortcuts" className="flex flex-col gap-3">
          <h2 id="home-shortcuts" className="text-[0.9375rem] font-semibold">
            {t('home.shortcuts')}
          </h2>
          <div className="flex flex-wrap gap-2">
            {shortcuts.map(({ key, to, icon: Icon }, index) => (
              <Button key={key} asChild variant={index === 0 ? 'default' : 'outline'}>
                <Link to={to}>
                  <Icon aria-hidden="true" />
                  {t(`home.shortcut.${key}`)}
                </Link>
              </Button>
            ))}
          </div>
        </section>
      )}

      <section aria-labelledby="home-modules" className="flex flex-col gap-3">
        <h2 id="home-modules" className="text-[0.9375rem] font-semibold">
          {t('home.modules')}
        </h2>
        <ul className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {ready.map(({ key, icon: Icon }) => (
            <li key={key}>
              <Link
                to={`/${key}`}
                className="group flex h-full items-start gap-4 rounded-xl border bg-card p-4 shadow-card transition-colors outline-none hover:border-ring/40 focus-visible:ring-[3px] focus-visible:ring-ring/45"
              >
                <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-secondary text-secondary-foreground">
                  <Icon aria-hidden="true" className="size-5" />
                </span>
                <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                  <span className="font-medium">{t(`nav.${key}`)}</span>
                  <span className="text-sm text-muted-foreground">{t(`modules.${key}`)}</span>
                </span>
                <ChevronRight
                  aria-hidden="true"
                  className="mt-0.5 size-4 shrink-0 text-muted-foreground transition-transform group-hover:translate-x-0.5"
                />
              </Link>
            </li>
          ))}
        </ul>
        {upcoming.length > 0 && (
          <ul className="grid gap-x-6 gap-y-1 rounded-xl border border-dashed border-border-strong bg-subtle p-3 sm:grid-cols-2 xl:grid-cols-3">
            {upcoming.map(({ key, icon: Icon }) => (
              <li
                key={key}
                className="flex items-center gap-3 px-1 py-1.5 text-sm text-muted-foreground"
              >
                <Icon aria-hidden="true" className="size-4 shrink-0" />
                <span className="flex-1">{t(`nav.${key}`)}</span>
                <Badge>{t('module.soon')}</Badge>
              </li>
            ))}
          </ul>
        )}
      </section>
    </>
  );
}
