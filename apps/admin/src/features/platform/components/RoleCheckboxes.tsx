import { useId } from 'react';
import type { FieldError } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { ROLE_KEYS, type RoleKey } from '../../../lib/auth/permissions';

/** One checkbox per system role. `error.message` is an i18n key with its namespace. */
export function RoleCheckboxes({
  value,
  onChange,
  legend,
  disabled,
  error,
}: {
  value: readonly RoleKey[];
  onChange: (roles: RoleKey[]) => void;
  legend: string;
  disabled?: boolean;
  error?: Pick<FieldError, 'message'>;
}) {
  const { t } = useTranslation('platform');
  const errorId = useId();
  const message = error?.message ? t(error.message) : undefined;

  return (
    <fieldset
      className="flex flex-col gap-2"
      disabled={disabled}
      aria-invalid={message ? true : undefined}
      aria-describedby={message ? errorId : undefined}
    >
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
      {message && (
        <p id={errorId} className="text-sm text-destructive">
          {message}
        </p>
      )}
    </fieldset>
  );
}
