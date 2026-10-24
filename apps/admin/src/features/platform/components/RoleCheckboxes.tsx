import { Checkbox } from '@shelter/ui/components/checkbox';
import { useId } from 'react';
import type { FieldError } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { ROLE_KEYS, type RoleKey } from '../../../lib/auth/permissions';
import { FieldMessage } from '../../../lib/forms/FormField';

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
      className="flex min-w-0 flex-col gap-1"
      disabled={disabled}
      aria-invalid={message ? true : undefined}
      aria-describedby={message ? errorId : undefined}
    >
      <legend className="mb-2 text-sm leading-none font-medium">{legend}</legend>
      {ROLE_KEYS.map((role) => (
        <label key={role} className="flex min-h-9 cursor-pointer items-center gap-2.5 text-sm">
          <Checkbox
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
      {message && <FieldMessage id={errorId}>{message}</FieldMessage>}
    </fieldset>
  );
}
