import type { ReactNode } from 'react';
import { hasPermission, useSession } from '../../lib/auth/session';
import { ForbiddenPage } from '../pages/ForbiddenPage';

/** Hides a page the user cannot use. UX only: the server enforces the permission. */
export function RequirePermission({
  permission,
  children,
}: {
  permission: string;
  children: ReactNode;
}) {
  const { data } = useSession();
  return hasPermission(data, permission) ? children : <ForbiddenPage />;
}
