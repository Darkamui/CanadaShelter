import { cn } from '@shelter/ui/lib/utils';
import { useTranslation } from 'react-i18next';
import { NavLink } from 'react-router';
import { visibleModules } from '../modules';

/** Module navigation, limited to what `permissions` allows (UX only: the server enforces). */
export function Sidebar({ permissions }: { permissions: readonly string[] }) {
  const { t } = useTranslation();

  return (
    <nav
      aria-label={t('nav.label')}
      className="w-60 shrink-0 border-r border-sidebar-border bg-sidebar p-3 text-sidebar-foreground"
    >
      <ul className="flex flex-col gap-1">
        {visibleModules(permissions).map(({ key, icon: Icon }) => (
          <li key={key}>
            <NavLink
              to={`/${key}`}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-2 rounded-md px-3 py-2 text-sm hover:bg-sidebar-accent hover:text-sidebar-accent-foreground',
                  isActive && 'bg-sidebar-accent font-medium text-sidebar-accent-foreground',
                )
              }
            >
              <Icon className="size-4" aria-hidden="true" />
              {t(`nav.${key}`)}
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}
