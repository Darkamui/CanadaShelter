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

/**
 * The nine backend modules (architecture §6), in navigation order. Label key: `shell:nav.<key>`.
 * `permission` hides the entry without it (UX only: the server enforces); `null` = placeholder module
 * with no read permission yet, always listed.
 */
export const MODULES = [
  { key: 'animals', icon: PawPrint, permission: Permissions.animalRead },
  { key: 'people', icon: Users, permission: Permissions.personRead },
  { key: 'movements', icon: ArrowLeftRight, permission: Permissions.movementRead },
  { key: 'medical', icon: Stethoscope, permission: null },
  { key: 'operations', icon: ClipboardList, permission: null },
  { key: 'engagement', icon: HeartHandshake, permission: null },
  { key: 'municipal', icon: Landmark, permission: null },
  { key: 'reporting', icon: ChartColumn, permission: null },
  { key: 'platform', icon: Settings, permission: Permissions.staffRead },
] as const satisfies readonly { key: string; icon: LucideIcon; permission: string | null }[];

/** The modules a user with `permissions` may open. */
export function visibleModules(permissions: readonly string[]) {
  return MODULES.filter(
    ({ permission }) => permission === null || permissions.includes(permission),
  );
}
