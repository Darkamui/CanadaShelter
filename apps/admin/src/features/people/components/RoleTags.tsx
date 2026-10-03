import { useTranslation } from 'react-i18next';
import { isPersonRole } from '../model';

/** A person's role tags as translated labels, comma-separated. Unknown codes show as is. */
export function RoleTags({ roles }: { roles: readonly string[] }) {
  const { t } = useTranslation('people');
  return <>{roles.map((role) => (isPersonRole(role) ? t(`roles.${role}`) : role)).join(', ')}</>;
}
