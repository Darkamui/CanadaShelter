import {
  ArrowLeftRight,
  ChartColumn,
  ClipboardList,
  HeartHandshake,
  Landmark,
  type LucideIcon,
  PawPrint,
  Settings,
  Stethoscope,
  Users,
} from 'lucide-react';
import { Permissions } from '../lib/auth/permissions';

/** Navigation groups, in order. Label key: `shell:nav.groups.<group>`. */
export const MODULE_GROUPS = ['shelter', 'community', 'management'] as const;
export type ModuleGroup = (typeof MODULE_GROUPS)[number];

/**
 * The nine backend modules (architecture §6), in navigation order. Label key: `shell:nav.<key>`, description
 * `shell:modules.<key>`. `permission` hides the entry without it (UX only: the server enforces); `null` =
 * placeholder module with no read permission yet, always listed. `ready` = the module has its own routes.
 */
export const MODULES = [
  { key: 'animals', group: 'shelter', icon: PawPrint, permission: Permissions.animalRead, ready: true },
  { key: 'people', group: 'community', icon: Users, permission: Permissions.personRead, ready: true },
  { key: 'movements', group: 'shelter', icon: ArrowLeftRight, permission: Permissions.movementRead, ready: false },
  { key: 'medical', group: 'shelter', icon: Stethoscope, permission: null, ready: false },
  { key: 'operations', group: 'shelter', icon: ClipboardList, permission: Permissions.locationRead, ready: true },
  { key: 'engagement', group: 'community', icon: HeartHandshake, permission: null, ready: false },
  { key: 'municipal', group: 'community', icon: Landmark, permission: null, ready: false },
  { key: 'reporting', group: 'management', icon: ChartColumn, permission: null, ready: false },
  { key: 'platform', group: 'management', icon: Settings, permission: Permissions.staffRead, ready: true },
] as const satisfies readonly {
  key: string;
  group: ModuleGroup;
  icon: LucideIcon;
  permission: string | null;
  ready: boolean;
}[];

/** The modules a user with `permissions` may open. */
export function visibleModules(permissions: readonly string[]) {
  return MODULES.filter(
    ({ permission }) => permission === null || permissions.includes(permission),
  );
}
