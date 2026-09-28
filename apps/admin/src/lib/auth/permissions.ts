/** Permission names the UI checks (the server's catalog is the authority: `PermissionDefinition`). */
export const Permissions = {
  animalRead: 'animal.read',
  movementRead: 'movement.read',
  staffRead: 'platform.staff.read',
  staffManage: 'platform.staff.manage',
} as const;

/** System roles (ADR 0018), in display order. Label key: `platform:roles.<key>`. */
export const ROLE_KEYS = ['administrator', 'staff', 'read_only'] as const;
export type RoleKey = (typeof ROLE_KEYS)[number];

export function isRoleKey(value: string): value is RoleKey {
  return (ROLE_KEYS as readonly string[]).includes(value);
}
