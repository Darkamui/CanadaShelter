import { Button } from '@shelter/ui/components/button';
import { FormActions, FormSection } from '@shelter/ui/components/form-layout';
import { useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { CheckboxField, FormField, SelectField, TextareaField } from '../../../lib/forms/FormField';
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

/**
 * Create and edit form for an animal. `withMicrochip` shows the microchip field (create only). `onSubmit` rejects with
 * the API error to show it on the fields. `alert` is a page-level message above the fields (a version conflict);
 * `extraActions` sits at the start of the action bar.
 */
export function AnimalForm({
  defaultValues,
  submitLabel,
  cancelTo,
  withMicrochip = false,
  alert,
  extraActions,
  onSubmit,
}: {
  defaultValues: AnimalFormValues;
  submitLabel: string;
  cancelTo: string;
  withMicrochip?: boolean;
  alert?: ReactNode;
  extraActions?: ReactNode;
  onSubmit: (values: AnimalFormOutput) => Promise<void>;
}) {
  const { t } = useTranslation('animals');
  const form = useZodForm(animalSchema, { defaultValues });
  const { errors, isSubmitting } = form.formState;
  const [failed, setFailed] = useState(false);
  const species = useSpecies();
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
    <form
      className="rounded-xl border bg-card px-5 pt-7 shadow-card sm:px-6"
      onSubmit={submit}
      noValidate
    >
      {(alert || failed) && (
        <div className="mb-7 flex flex-col gap-3">
          {alert}
          {failed && <FormAlert tone="error">{t('form.unexpectedError')}</FormAlert>}
        </div>
      )}
      <FormSection title={t('sections.identity')} description={t('sections.identityHint')}>
        <FormField
          label={t('fields.name')}
          autoComplete="off"
          error={errors.name}
          {...form.register('name')}
        />
        <SelectField
          label={t('fields.species')}
          error={errors.speciesCode}
          {...form.register('speciesCode')}
        >
          <option value="">{t('form.chooseSpecies')}</option>
          {speciesOptions.map((s) => (
            <option key={s.code} value={s.code}>
              {s.label}
            </option>
          ))}
        </SelectField>
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
        {withMicrochip && (
          <FormField
            label={t('fields.microchip')}
            hint={t('fields.microchipHint')}
            autoComplete="off"
            inputMode="numeric"
            error={errors.microchip}
            {...form.register('microchip')}
          />
        )}
      </FormSection>
      <FormSection title={t('sections.biology')}>
        <SelectField label={t('fields.sex')} {...form.register('sex')}>
          {SEXES.map((code) => (
            <option key={code} value={code}>
              {t(`sexes.${code}`)}
            </option>
          ))}
        </SelectField>
        <SelectField
          label={t('fields.reproductiveStatus')}
          {...form.register('reproductiveStatus')}
        >
          {REPRODUCTIVE_STATUSES.map((code) => (
            <option key={code} value={code}>
              {t(`reproductiveStatuses.${code}`)}
            </option>
          ))}
        </SelectField>
        <FormField
          label={t('fields.birthDate')}
          type="date"
          min="1950-01-01"
          max={today()}
          error={errors.birthDate}
          {...form.register('birthDate')}
        />
        <CheckboxField
          label={t('fields.birthDateEstimated')}
          disabled={birthDate === ''}
          {...form.register('birthDateEstimated')}
        />
      </FormSection>
      <FormSection title={t('fields.notesLegend')} description={t('fields.noPersonalInformation')}>
        <TextareaField label={t('fields.marks')} error={errors.marks} {...form.register('marks')} />
        <TextareaField
          label={t('fields.medicalAlert')}
          error={errors.medicalAlert}
          {...form.register('medicalAlert')}
        />
        <TextareaField
          label={t('fields.behaviourAlert')}
          error={errors.behaviourAlert}
          {...form.register('behaviourAlert')}
        />
        <TextareaField
          label={t('fields.legalAlert')}
          error={errors.legalAlert}
          {...form.register('legalAlert')}
        />
      </FormSection>
      <FormActions sticky className="rounded-b-xl">
        {extraActions && <div className="mr-auto">{extraActions}</div>}
        <Button asChild variant="ghost">
          <Link to={cancelTo} relative="path">
            {t('shell:forms.cancel')}
          </Link>
        </Button>
        <Button type="submit" disabled={isSubmitting}>
          {submitLabel}
        </Button>
      </FormActions>
    </form>
  );
}
