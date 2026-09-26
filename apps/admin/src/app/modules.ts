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

/** The nine backend modules (architecture §6), in navigation order. Label key: `shell:nav.<key>`. */
export const MODULES = [
  { key: 'animals', icon: PawPrint },
  { key: 'people', icon: Users },
  { key: 'movements', icon: ArrowLeftRight },
  { key: 'medical', icon: Stethoscope },
  { key: 'operations', icon: ClipboardList },
  { key: 'engagement', icon: HeartHandshake },
  { key: 'municipal', icon: Landmark },
  { key: 'reporting', icon: ChartColumn },
  { key: 'platform', icon: Settings },
] as const satisfies readonly { key: string; icon: LucideIcon }[];

export type ModuleKey = (typeof MODULES)[number]['key'];

export function isModuleKey(value: string | undefined): value is ModuleKey {
  return MODULES.some((module) => module.key === value);
}
