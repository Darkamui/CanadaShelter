import { Button } from '@shelter/ui/components/button';
import { Label } from '@shelter/ui/components/label';
import { useId, useState } from 'react';
import type { FieldError } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { FormField } from '../../../lib/forms/FormField';
import { applyValidationErrors } from '../../../lib/forms/serverErrors';
import { useZodForm } from '../../../lib/forms/useZodForm';
import {
  animalSchema,
  REPRODUCTIVE_STATUSES,
  SEXES,
  today,
  type AnimalFormOutput,
  type AnimalFormValues,
} from '../model';
import { useSpecies } from '../useSpecies';

const invalid = 'animals:form.invalid';
const tooLong = 'animals:form.tooLong';

// Server field → form field and message (server messages are English only).
const serverFields = {
  name: ['name', tooLong],
  speciesCode: ['speciesCode', 'animals:form.speciesUnavailable'],
  breed: ['breed', tooLong],
  secondaryBreed: ['secondaryBreed', tooLong],
  colour: ['colour', tooLong],
  sex: ['sex', invalid],
  reproductiveStatus: ['reproductiveStatus', invalid],
  birthDate: ['birthDate', 'animals:form.birthDateRange'],
  marks: ['marks', tooLong],
  behaviourAlert: ['behaviourAlert', tooLong],
  medicalAlert: ['medicalAlert', tooLong],
  legalAlert: ['legalAlert', tooLong],
  microchip: ['microchip', 'animals:form.microchipInvalid'],
} as const;

const selectClass = 'h-9 rounded-md border bg-transparent px-3 text-sm';

/**
 * Create and edit form for an animal. `withMicrochip` shows the microchip field (create only). `onSubmit` rejects with
 * the API error to show it on the fields.
 */
export function AnimalForm({
  defaultValues,
  submitLabel,
  withMicrochip = false,
  onSubmit,
}: {
  defaultValues: AnimalFormValues;
  submitLabel: string;
  withMicrochip?: boolean;
  onSubmit: (values: AnimalFormOutput) => Promise<void>;
}) {
  const { t } = useTranslation('animals');
  const form = useZodForm(animalSchema, { defaultValues });
  const { errors, isSubmitting } = form.formState;
  const [failed, setFailed] = useState(false);
  const species = useSpecies();
  const speciesId = useId();
  const sexId = useId();
  const reproductiveId = useId();
  const birthDate = form.watch('birthDate');
  // A species the organization has since hidden stays selectable on the animal that already has it.
  const current = defaultValues.speciesCode;
  const speciesOptions =
    current && !species.options.some((s) => s.code === current)
      ? [...species.options, { code: current, label: species.labelOf(current) }]
      : species.options;

  const submit = form.handleSubmit(async (values) => {
    setFailed(false);
    try {
      await onSubmit(values);
    } catch (error) {
      if (!applyValidationErrors(error, form.setError, serverFields)) setFailed(true);
    }
  });

  return (
    <form className="flex max-w-2xl flex-col gap-4" onSubmit={submit} noValidate>
      {failed && <FormAlert tone="error">{t('form.unexpectedError')}</FormAlert>}
      <div className="grid gap-4 sm:grid-cols-2">
        <FormField
          label={t('fields.name')}
          autoComplete="off"
          error={errors.name}
          {...form.register('name')}
        />
        <div className="flex flex-col gap-2">
          <Label htmlFor={speciesId}>{t('fields.species')}</Label>
          <select
            id={speciesId}
            className={selectClass}
            aria-invalid={errors.speciesCode ? true : undefined}
            {...form.register('speciesCode')}
          >
            <option value="">{t('form.chooseSpecies')}</option>
            {speciesOptions.map((s) => (
              <option key={s.code} value={s.code}>
                {s.label}
              </option>
            ))}
          </select>
          <FieldMessage error={errors.speciesCode} />
        </div>
      </div>
      <div className="grid gap-4 sm:grid-cols-3">
        <FormField
          label={t('fields.breed')}
          autoComplete="off"
          error={errors.breed}
          {...form.register('breed')}
        />
        <FormField
          label={t('fields.secondaryBreed')}
          autoComplete="off"
          error={errors.secondaryBreed}
          {...form.register('secondaryBreed')}
        />
        <FormField
          label={t('fields.colour')}
          autoComplete="off"
          error={errors.colour}
          {...form.register('colour')}
        />
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="flex flex-col gap-2">
          <Label htmlFor={sexId}>{t('fields.sex')}</Label>
          <select id={sexId} className={selectClass} {...form.register('sex')}>
            {SEXES.map((code) => (
              <option key={code} value={code}>
                {t(`sexes.${code}`)}
              </option>
            ))}
          </select>
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor={reproductiveId}>{t('fields.reproductiveStatus')}</Label>
          <select
            id={reproductiveId}
            className={selectClass}
            {...form.register('reproductiveStatus')}
          >
            {REPRODUCTIVE_STATUSES.map((code) => (
              <option key={code} value={code}>
                {t(`reproductiveStatuses.${code}`)}
              </option>
            ))}
          </select>
        </div>
      </div>
      <div className="flex flex-wrap items-end gap-4">
        <FormField
          label={t('fields.birthDate')}
          type="date"
          min="1950-01-01"
          max={today()}
          error={errors.birthDate}
          {...form.register('birthDate')}
        />
        <label className="flex h-9 items-center gap-2 text-sm">
          <input
            type="checkbox"
            className="size-4"
            disabled={birthDate === ''}
            {...form.register('birthDateEstimated')}
          />
          {t('fields.birthDateEstimated')}
        </label>
      </div>
      {withMicrochip && (
        <FormField
          label={t('fields.microchip')}
          hint={t('fields.microchipHint')}
          autoComplete="off"
          error={errors.microchip}
          {...form.register('microchip')}
        />
      )}
      <fieldset className="flex flex-col gap-4">
        <legend className="mb-1 text-sm font-medium">{t('fields.notesLegend')}</legend>
        <p className="text-xs text-muted-foreground">{t('fields.noPersonalInformation')}</p>
        <NoteField label={t('fields.marks')} error={errors.marks} {...form.register('marks')} />
        <NoteField
          label={t('fields.behaviourAlert')}
          error={errors.behaviourAlert}
          {...form.register('behaviourAlert')}
        />
        <NoteField
          label={t('fields.medicalAlert')}
          error={errors.medicalAlert}
          {...form.register('medicalAlert')}
        />
        <NoteField
          label={t('fields.legalAlert')}
          error={errors.legalAlert}
          {...form.register('legalAlert')}
        />
      </fieldset>
      <Button type="submit" className="self-start" disabled={isSubmitting}>
        {submitLabel}
      </Button>
    </form>
  );
}

function NoteField({
  label,
  error,
  ...textarea
}: React.ComponentProps<'textarea'> & { label: string; error?: Pick<FieldError, 'message'> }) {
  const id = useId();
  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor={id}>{label}</Label>
      <textarea
        id={id}
        rows={2}
        className="rounded-md border bg-transparent px-3 py-2 text-sm"
        aria-invalid={error ? true : undefined}
        {...textarea}
      />
      <FieldMessage error={error} />
    </div>
  );
}

function FieldMessage({ error }: { error?: Pick<FieldError, 'message'> }) {
  const { t } = useTranslation();
  return error?.message ? <p className="text-sm text-destructive">{t(error.message)}</p> : null;
}
