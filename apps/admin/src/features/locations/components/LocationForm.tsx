import type { LocationKindItem, LocationRequest } from '@shelter/api-client/model';
import { Button } from '@shelter/ui/components/button';
import { Label } from '@shelter/ui/components/label';
import { useId, useState } from 'react';
import type { FieldError } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { FormField } from '../../../lib/forms/FormField';
import { applyValidationErrors } from '../../../lib/forms/serverErrors';
import { useZodForm } from '../../../lib/forms/useZodForm';
import { useLocalize } from '../../../lib/i18n/localized';
import { locationSchema, toRequest, type LocationFormValues, type LocationNode } from '../model';

// Server field → form field and message (server messages are English only). A name error from the server is the
// sibling uniqueness check: the length is checked here first.
const serverFields = {
  name: ['name', 'locations:form.nameTaken'],
  kindCode: ['kindCode', 'locations:form.kindUnavailable'],
  parentId: ['parentId', 'locations:form.parentInvalid'],
  capacity: ['capacity', 'locations:form.capacityRange'],
} as const;

const selectClass = 'h-9 rounded-md border bg-transparent px-3 text-sm';

/**
 * Create and edit form for a location. `parents` are the locations it may be placed in (depth-first, itself and its
 * descendants already removed). `onSubmit` rejects with the API error to show it on the fields.
 */
export function LocationForm({
  defaultValues,
  kinds,
  parents,
  submitLabel,
  onSubmit,
}: {
  defaultValues: LocationFormValues;
  kinds: readonly LocationKindItem[];
  parents: readonly LocationNode[];
  submitLabel: string;
  onSubmit: (request: LocationRequest) => Promise<void>;
}) {
  const { t } = useTranslation('locations');
  const localize = useLocalize();
  const form = useZodForm(locationSchema, { defaultValues });
  const { errors, isSubmitting } = form.formState;
  const [failed, setFailed] = useState(false);
  const kindId = useId();
  const parentId = useId();
  // A kind the organization has since hidden stays selectable on the location that already has it.
  const currentKindHidden =
    defaultValues.kindCode !== '' && !kinds.some((k) => k.code === defaultValues.kindCode);

  const submit = form.handleSubmit(async (values) => {
    setFailed(false);
    try {
      await onSubmit(toRequest(values));
    } catch (error) {
      if (!applyValidationErrors(error, form.setError, serverFields)) setFailed(true);
    }
  });

  return (
    <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
      {failed && <FormAlert tone="error">{t('form.unexpectedError')}</FormAlert>}
      <FormField
        label={t('fields.name')}
        autoComplete="off"
        error={errors.name}
        {...form.register('name')}
      />
      <div className="flex flex-col gap-2">
        <Label htmlFor={kindId}>{t('fields.kind')}</Label>
        <select
          id={kindId}
          className={selectClass}
          aria-invalid={errors.kindCode ? true : undefined}
          {...form.register('kindCode')}
        >
          <option value="">{t('form.chooseKind')}</option>
          {currentKindHidden && (
            <option value={defaultValues.kindCode}>{defaultValues.kindCode}</option>
          )}
          {kinds.map((kind) => (
            <option key={kind.code} value={kind.code}>
              {localize(kind.label)}
            </option>
          ))}
        </select>
        <FieldMessage error={errors.kindCode} />
      </div>
      <div className="flex flex-col gap-2">
        <Label htmlFor={parentId}>{t('fields.parent')}</Label>
        <select
          id={parentId}
          className={selectClass}
          aria-invalid={errors.parentId ? true : undefined}
          {...form.register('parentId')}
        >
          <option value="">{t('form.topLevel')}</option>
          {parents.map(({ location, depth }) => (
            <option key={location.id} value={location.id}>
              {`${'  '.repeat(depth)}${location.name}`}
            </option>
          ))}
        </select>
        <FieldMessage error={errors.parentId} />
      </div>
      <FormField
        label={t('fields.capacity')}
        hint={t('fields.capacityHint')}
        inputMode="numeric"
        autoComplete="off"
        error={errors.capacity}
        {...form.register('capacity')}
      />
      <Button type="submit" className="self-start" disabled={isSubmitting}>
        {submitLabel}
      </Button>
    </form>
  );
}

function FieldMessage({ error }: { error?: Pick<FieldError, 'message'> }) {
  const { t } = useTranslation();
  return error?.message ? <p className="text-sm text-destructive">{t(error.message)}</p> : null;
}
