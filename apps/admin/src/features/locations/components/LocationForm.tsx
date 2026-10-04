import type { LocationKindItem, LocationRequest } from '@shelter/api-client/model';
import { Button } from '@shelter/ui/components/button';
import { DialogClose, DialogFooter } from '@shelter/ui/components/dialog';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { FormField, SelectField } from '../../../lib/forms/FormField';
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

/**
 * Create and edit form for a location. `parents` are the locations it may be placed in (depth-first, itself and its
 * descendants already removed). Rendered inside a dialog, with its footer. `onSubmit` rejects with the API error to show it on the fields.
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
      <SelectField label={t('fields.kind')} error={errors.kindCode} {...form.register('kindCode')}>
        <option value="">{t('form.chooseKind')}</option>
        {currentKindHidden && (
          <option value={defaultValues.kindCode}>{defaultValues.kindCode}</option>
        )}
        {kinds.map((kind) => (
          <option key={kind.code} value={kind.code}>
            {localize(kind.label)}
          </option>
        ))}
      </SelectField>
      <SelectField
        label={t('fields.parent')}
        error={errors.parentId}
        {...form.register('parentId')}
      >
        <option value="">{t('form.topLevel')}</option>
        {parents.map(({ location, depth }) => (
          <option key={location.id} value={location.id}>
            {`${'   '.repeat(depth)}${location.name}`}
          </option>
        ))}
      </SelectField>
      <FormField
        label={t('fields.capacity')}
        hint={t('fields.capacityHint')}
        inputMode="numeric"
        autoComplete="off"
        error={errors.capacity}
        {...form.register('capacity')}
      />
      <DialogFooter className="mt-1">
        <DialogClose asChild>
          <Button type="button" variant="ghost">
            {t('shell:forms.cancel')}
          </Button>
        </DialogClose>
        <Button type="submit" disabled={isSubmitting}>
          {submitLabel}
        </Button>
      </DialogFooter>
    </form>
  );
}
