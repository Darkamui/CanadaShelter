import { Badge } from '@shelter/ui/components/badge';
import { useTranslation } from 'react-i18next';
import { isPersonRole } from '../model';

function useRoleLabel() {
  const { t } = useTranslation('people');
  return (role: string) => (isPersonRole(role) ? t(`roles.${role}`) : role);
}

/** A person's role tags as translated labels, comma-separated. Unknown codes show as is. */
export function RoleTags({ roles }: { roles: readonly string[] }) {
  const label = useRoleLabel();
  return <>{roles.map(label).join(', ')}</>;
}

/** A person's role tags as badges, for the record header. */
export function RoleBadges({ roles }: { roles: readonly string[] }) {
  const label = useRoleLabel();
  return (
    <>
      {roles.map((role) => (
        <Badge key={role} tone="primary">
          {label(role)}
        </Badge>
      ))}
    </>
  );
}
