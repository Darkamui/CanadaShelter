import type { PersonRequest } from '@shelter/api-client/model';
import { Button } from '@shelter/ui/components/button';
import { Checkbox } from '@shelter/ui/components/checkbox';
import { FormActions, FormSection } from '@shelter/ui/components/form-layout';
import { useState } from 'react';
import { Controller, type FieldError } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { FieldMessage, FormField, SelectField, TextareaField } from '../../../lib/forms/FormField';
import { formMessages } from '../../../lib/forms/schemas';
import { applyValidationErrors } from '../../../lib/forms/serverErrors';
import { useZodForm } from '../../../lib/forms/useZodForm';
import {
  PERSON_ROLES,
  PROVINCES,
  personSchema,
  toRequest,
  type PersonFormValues,
  type PersonRole,
} from '../model';

const invalid = 'people:form.invalid';

// Server field → form field and message (server messages are English only).
const serverFields = {
  firstName: ['firstName', 'people:form.tooLong'],
  lastName: ['lastName', 'people:form.tooLong'],
  displayName: ['displayName', 'people:form.nameRequired'],
  email: ['email', formMessages.email],
  phone: ['phone', invalid],
  secondaryPhone: ['secondaryPhone', invalid],
  addressLine: ['addressLine', 'people:form.tooLong'],
  city: ['city', 'people:form.tooLong'],
  province: ['province', invalid],
  postalCode: ['postalCode', 'people:form.tooLong'],
  preferredLanguage: ['preferredLanguage', invalid],
  roleTags: ['roleTags', invalid],
  notes: ['notes', 'people:form.tooLong'],
} as const;

/** Create and edit form for a person. `onSubmit` rejects with the API error to show it on the fields. */
export function PersonForm({
  defaultValues,
  submitLabel,
  cancelTo,
  onSubmit,
}: {
  defaultValues: PersonFormValues;
  submitLabel: string;
  cancelTo: string;
  onSubmit: (request: PersonRequest) => Promise<void>;
}) {
  const { t } = useTranslation('people');
  const form = useZodForm(personSchema, { defaultValues });
  const { errors, isSubmitting } = form.formState;
  const [failed, setFailed] = useState(false);

  const submit = form.handleSubmit(async (values) => {
    setFailed(false);
    try {
      await onSubmit(toRequest(values));
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
      {failed && (
        <div className="mb-7">
          <FormAlert tone="error">{t('form.unexpectedError')}</FormAlert>
        </div>
      )}
      <FormSection title={t('sections.identity')} description={t('sections.identityHint')}>
        <FormField
          label={t('fields.firstName')}
          autoComplete="off"
          error={errors.firstName}
          {...form.register('firstName')}
        />
        <FormField
          label={t('fields.lastName')}
          autoComplete="off"
          error={errors.lastName}
          {...form.register('lastName')}
        />
        <FormField
          label={t('fields.displayName')}
          hint={t('fields.displayNameHint')}
          autoComplete="off"
          wide
          error={errors.displayName}
          {...form.register('displayName')}
        />
      </FormSection>
      <FormSection title={t('sections.contact')} description={t('sections.contactHint')}>
        <FormField
          label={t('fields.email')}
          type="email"
          autoComplete="off"
          wide
          error={errors.email}
          {...form.register('email')}
        />
        <FormField
          label={t('fields.phone')}
          type="tel"
          autoComplete="off"
          error={errors.phone}
          {...form.register('phone')}
        />
        <FormField
          label={t('fields.secondaryPhone')}
          type="tel"
          autoComplete="off"
          error={errors.secondaryPhone}
          {...form.register('secondaryPhone')}
        />
        <FormField
          label={t('fields.addressLine')}
          autoComplete="off"
          wide
          error={errors.addressLine}
          {...form.register('addressLine')}
        />
        <FormField
          label={t('fields.city')}
          autoComplete="off"
          error={errors.city}
          {...form.register('city')}
        />
        <SelectField
          label={t('fields.province')}
          error={errors.province}
          {...form.register('province')}
        >
          <option value="">{t('form.noProvince')}</option>
          {PROVINCES.map((code) => (
            <option key={code} value={code}>
              {t(`provinces.${code}`)}
            </option>
          ))}
        </SelectField>
        <FormField
          label={t('fields.postalCode')}
          autoComplete="off"
          error={errors.postalCode}
          {...form.register('postalCode')}
        />
        <SelectField label={t('fields.preferredLanguage')} {...form.register('preferredLanguage')}>
          <option value="fr">{t('languages.fr')}</option>
          <option value="en">{t('languages.en')}</option>
        </SelectField>
      </FormSection>
      <FormSection title={t('sections.rolesNotes')}>
        <Controller
          control={form.control}
          name="roleTags"
          render={({ field, fieldState }) => (
            <RoleTagCheckboxes
              legend={t('fields.roleTags')}
              value={field.value}
              onChange={field.onChange}
              error={fieldState.error}
            />
          )}
        />
        <TextareaField
          label={t('fields.notes')}
          hint={t('fields.notesHint')}
          rows={4}
          error={errors.notes}
          {...form.register('notes')}
        />
      </FormSection>
      <FormActions sticky className="rounded-b-xl">
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

function RoleTagCheckboxes({
  legend,
  value,
  onChange,
  error,
}: {
  legend: string;
  value: readonly PersonRole[];
  onChange: (roles: PersonRole[]) => void;
  error?: Pick<FieldError, 'message'>;
}) {
  const { t } = useTranslation('people');

  return (
    <fieldset data-wide className="flex min-w-0 flex-col gap-2">
      <legend className="mb-2 text-sm leading-none font-medium">{legend}</legend>
      <div className="grid gap-x-6 gap-y-1 sm:grid-cols-2">
        {PERSON_ROLES.map((role) => (
          <label key={role} className="flex min-h-9 cursor-pointer items-center gap-2.5 text-sm">
            <Checkbox
              checked={value.includes(role)}
              onChange={(event) =>
                onChange(
                  event.target.checked
                    ? PERSON_ROLES.filter((r) => r === role || value.includes(r))
                    : value.filter((r) => r !== role),
                )
              }
            />
            {t(`roles.${role}`)}
          </label>
        ))}
      </div>
      {error?.message && <FieldMessage>{t(error.message)}</FieldMessage>}
    </fieldset>
  );
}
