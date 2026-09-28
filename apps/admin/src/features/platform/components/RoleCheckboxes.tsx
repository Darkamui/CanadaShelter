import { useTranslation } from 'react-i18next';
import { ROLE_KEYS, type RoleKey } from '../../../lib/auth/permissions';

/** One checkbox per system role. */
export function RoleCheckboxes({
  value,
  onChange,
  legend,
  disabled,
}: {
  value: readonly RoleKey[];
  onChange: (roles: RoleKey[]) => void;
  legend: string;
  disabled?: boolean;
}) {
  const { t } = useTranslation('platform');

  return (
    <fieldset className="flex flex-col gap-2" disabled={disabled}>
      <legend className="mb-1 text-sm font-medium">{legend}</legend>
      {ROLE_KEYS.map((role) => (
        <label key={role} className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            className="size-4"
            checked={value.includes(role)}
            onChange={(event) =>
              onChange(
                event.target.checked
                  ? ROLE_KEYS.filter((r) => r === role || value.includes(r))
                  : value.filter((r) => r !== role),
              )
            }
          />
          {t(`roles.${role}`)}
        </label>
      ))}
    </fieldset>
  );
}
