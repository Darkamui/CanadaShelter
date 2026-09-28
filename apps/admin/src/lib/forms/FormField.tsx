import { Input } from '@shelter/ui/components/input';
import { Label } from '@shelter/ui/components/label';
import { useId, type ComponentProps } from 'react';
import type { FieldError } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

/**
 * Labelled input with an optional hint and an error announced to screen readers. Spread `register('name')`
 * into it; `error` is the field's error, whose message is an i18n key with its namespace.
 */
export function FormField({
  label,
  hint,
  error,
  ...input
}: ComponentProps<'input'> & {
  label: string;
  hint?: string;
  error?: Pick<FieldError, 'message'>;
}) {
  const { t } = useTranslation();
  const id = useId();
  const message = error?.message ? t(error.message) : undefined;
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = message ? `${id}-error` : undefined;

  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        aria-invalid={message ? true : undefined}
        aria-describedby={[hintId, errorId].filter(Boolean).join(' ') || undefined}
        {...input}
      />
      {hint && (
        <p id={hintId} className="text-xs text-muted-foreground">
          {hint}
        </p>
      )}
      {message && (
        <p id={errorId} className="text-sm text-destructive">
          {message}
        </p>
      )}
    </div>
  );
}
