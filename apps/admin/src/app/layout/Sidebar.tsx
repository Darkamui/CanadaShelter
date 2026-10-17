import { Tooltip, TooltipContent, TooltipTrigger } from '@shelter/ui/components/tooltip';
import { cn } from '@shelter/ui/lib/utils';
import { PanelLeftClose, PanelLeftOpen } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link, NavLink } from 'react-router';
import { MODULE_GROUPS, visibleModules } from '../modules';
import { BrandMark } from './BrandMark';

type NavProps = {
  permissions: readonly string[];
  /** Icons only, names in tooltips (desktop rail). */
  collapsed?: boolean;
  /** Called after a link is followed (closes the mobile sheet). */
  onNavigate?: () => void;
};

/** The brand and app name, a link home. Outside the navigation landmark, which lists modules only. */
export function SidebarBrand({ collapsed = false }: { collapsed?: boolean }) {
  const { t } = useTranslation();

  return (
    <Link
      to="/"
      className={cn(
        'flex h-16 shrink-0 items-center gap-2.5 rounded-md px-4 text-sidebar-foreground outline-none focus-visible:ring-[3px] focus-visible:ring-sidebar-indicator/50',
        collapsed && 'justify-center px-0',
      )}
    >
      <BrandMark className="size-8 text-sidebar-indicator" />
      <span
        className={cn(
          'font-display text-[1.0625rem] leading-tight font-semibold tracking-[-0.01em]',
          collapsed && 'sr-only',
        )}
      >
        {t('appName')}
      </span>
    </Link>
  );
}

/** Module navigation in groups, limited to what `permissions` allows (UX only: the server enforces). */
export function SidebarNav({ permissions, collapsed = false, onNavigate }: NavProps) {
  const { t } = useTranslation();
  const modules = visibleModules(permissions);

  return (
    <nav aria-label={t('nav.label')} className="flex flex-col gap-5 px-3 py-2">
      {MODULE_GROUPS.map((group) => {
        const items = modules.filter((module) => module.group === group);
        if (items.length === 0) return null;
        const headingId = `nav-group-${group}`;
        return (
          <div key={group} className="flex flex-col gap-1">
            <p
              id={headingId}
              className={cn(
                'px-3 pb-1 text-xs font-medium text-sidebar-muted',
                collapsed && 'sr-only',
              )}
            >
              {t(`nav.groups.${group}`)}
            </p>
            {collapsed && (
              <span aria-hidden="true" className="mx-3 mb-1 h-px bg-sidebar-border first:hidden" />
            )}
            <ul aria-labelledby={headingId} className="flex flex-col gap-0.5">
              {items.map(({ key, icon: Icon }) => {
                const label = t(`nav.${key}`);
                const link = (
                  <NavLink
                    to={`/${key}`}
                    onClick={onNavigate}
                    className={({ isActive }) =>
                      cn(
                        'relative flex h-10 items-center gap-3 rounded-md px-3 text-sm text-sidebar-foreground/85 transition-colors outline-none hover:bg-sidebar-accent hover:text-sidebar-foreground focus-visible:ring-[3px] focus-visible:ring-sidebar-indicator/50 pointer-coarse:h-11',
                        isActive &&
                          'bg-sidebar-active font-medium text-sidebar-foreground before:absolute before:inset-y-2 before:-left-3 before:w-1 before:rounded-r-full before:bg-sidebar-indicator',
                        collapsed && 'justify-center px-0',
                      )
                    }
                  >
                    <Icon className="size-[1.125rem] shrink-0" aria-hidden="true" />
                    <span className={cn(collapsed && 'sr-only')}>{label}</span>
                  </NavLink>
                );
                return (
                  <li key={key}>
                    {collapsed ? (
                      <Tooltip>
                        <TooltipTrigger asChild>{link}</TooltipTrigger>
                        <TooltipContent side="right">{label}</TooltipContent>
                      </Tooltip>
                    ) : (
                      link
                    )}
                  </li>
                );
              })}
            </ul>
          </div>
        );
      })}
    </nav>
  );
}

/** The desktop rail (from `lg`): brand, grouped modules and the collapse toggle. */
export function Sidebar({
  permissions,
  collapsed,
  onToggle,
}: {
  permissions: readonly string[];
  collapsed: boolean;
  onToggle: () => void;
}) {
  const { t } = useTranslation();
  const ToggleIcon = collapsed ? PanelLeftOpen : PanelLeftClose;

  return (
    <aside
      className={cn(
        'sticky top-0 hidden h-svh shrink-0 flex-col bg-sidebar text-sidebar-foreground transition-[width] duration-200 lg:flex',
        collapsed ? 'w-[4.5rem]' : 'w-64',
      )}
    >
      <div className={cn('px-2', collapsed && 'px-3')}>
        <SidebarBrand collapsed={collapsed} />
      </div>
      <div className="flex-1 overflow-y-auto">
        <SidebarNav permissions={permissions} collapsed={collapsed} />
      </div>
      <div className="border-t border-sidebar-border p-3">
        <button
          type="button"
          onClick={onToggle}
          aria-label={collapsed ? t('nav.expand') : t('nav.collapse')}
          aria-expanded={!collapsed}
          className={cn(
            'flex h-9 w-full items-center gap-3 rounded-md px-3 text-sm text-sidebar-muted transition-colors outline-none hover:bg-sidebar-accent hover:text-sidebar-foreground focus-visible:ring-[3px] focus-visible:ring-sidebar-indicator/50',
            collapsed && 'justify-center px-0',
          )}
        >
          <ToggleIcon aria-hidden="true" className="size-[1.125rem]" />
          {!collapsed && <span aria-hidden="true">{t('nav.collapse')}</span>}
        </button>
      </div>
    </aside>
  );
}
