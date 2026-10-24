import { Checkbox } from '@shelter/ui/components/checkbox';
import { Input } from '@shelter/ui/components/input';
import { Label } from '@shelter/ui/components/label';
import { NativeSelect } from '@shelter/ui/components/native-select';
import { Textarea } from '@shelter/ui/components/textarea';
import { cn } from '@shelter/ui/lib/utils';
import { CircleAlert } from 'lucide-react';
import { useId, type ComponentProps, type ReactNode } from 'react';
import type { FieldError } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

type FieldProps = {
  label: string;
  hint?: string;
  /** The field's error; its message is an i18n key with its namespace. */
  error?: Pick<FieldError, 'message'>;
  /** Spans both columns of a {@link FormSection}. */
  wide?: boolean;
  className?: string;
};

type ControlProps = {
  id: string;
  'aria-invalid'?: true;
  'aria-describedby'?: string;
};

/** Label, control, hint and error, wired together. The `render` callback gets the control's ARIA props. */
function Field({
  label,
  hint,
  error,
  wide,
  className,
  render,
}: FieldProps & { render: (control: ControlProps) => ReactNode }) {
  const { t } = useTranslation();
  const id = useId();
  const message = error?.message ? t(error.message) : undefined;
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = message ? `${id}-error` : undefined;

  return (
    <div
      data-slot="field"
      data-wide={wide || undefined}
      className={cn('flex min-w-0 flex-col gap-2', className)}
    >
      <Label htmlFor={id}>{label}</Label>
      {render({
        id,
        'aria-invalid': message ? true : undefined,
        'aria-describedby': [hintId, errorId].filter(Boolean).join(' ') || undefined,
      })}
      {hint && (
        <p id={hintId} className="text-[0.8125rem] text-muted-foreground">
          {hint}
        </p>
      )}
      {message && <FieldMessage id={errorId}>{message}</FieldMessage>}
    </div>
  );
}

/** A field's error line. */
export function FieldMessage({ id, children }: { id?: string; children: ReactNode }) {
  return (
    <p id={id} className="flex items-start gap-1.5 text-[0.8125rem] font-medium text-danger">
      <CircleAlert aria-hidden="true" className="mt-px size-3.5 shrink-0" />
      {children}
    </p>
  );
}

/**
 * Labelled input with an optional hint and an error announced to screen readers. Spread `register('name')` into it.
 */
export function FormField({
  label,
  hint,
  error,
  wide,
  className,
  ...input
}: ComponentProps<'input'> & FieldProps) {
  return (
    <Field
      {...{ label, hint, error, wide, className }}
      render={(control) => <Input {...control} {...input} />}
    />
  );
}

/** Labelled native select; pass the `<option>`s as children. */
export function SelectField({
  label,
  hint,
  error,
  wide,
  className,
  children,
  ...select
}: ComponentProps<'select'> & FieldProps) {
  return (
    <Field
      {...{ label, hint, error, wide, className }}
      render={(control) => (
        <NativeSelect {...control} {...select}>
          {children}
        </NativeSelect>
      )}
    />
  );
}

/** Labelled textarea that grows with its content. */
export function TextareaField({
  label,
  hint,
  error,
  wide = true,
  className,
  rows = 2,
  ...textarea
}: ComponentProps<'textarea'> & FieldProps) {
  return (
    <Field
      {...{ label, hint, error, wide, className }}
      render={(control) => <Textarea rows={rows} {...control} {...textarea} />}
    />
  );
}

/** A checkbox with its label on the right, aligned with the inputs of its row. */
export function CheckboxField({
  label,
  hint,
  wide,
  className,
  ...input
}: Omit<ComponentProps<'input'>, 'type'> & Omit<FieldProps, 'error'>) {
  const id = useId();
  const hintId = hint ? `${id}-hint` : undefined;
  return (
    <div
      data-slot="field"
      data-wide={wide || undefined}
      className={cn('flex min-w-0 flex-col justify-end gap-1', className)}
    >
      <label
        htmlFor={id}
        className="flex min-h-10 cursor-pointer items-center gap-2.5 text-sm has-disabled:cursor-not-allowed has-disabled:text-muted-foreground"
      >
        <Checkbox id={id} aria-describedby={hintId} {...input} />
        {label}
      </label>
      {hint && (
        <p id={hintId} className="text-[0.8125rem] text-muted-foreground">
          {hint}
        </p>
      )}
    </div>
  );
}
