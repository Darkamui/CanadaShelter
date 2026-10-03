import type { PersonRequest } from '@shelter/api-client/model';
import { Button } from '@shelter/ui/components/button';
import { Label } from '@shelter/ui/components/label';
import { useId, useState } from 'react';
import { Controller, type FieldError } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { FormField } from '../../../lib/forms/FormField';
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

const selectClass = 'h-9 rounded-md border bg-transparent px-3 text-sm';

/** Create and edit form for a person. `onSubmit` rejects with the API error to show it on the fields. */
export function PersonForm({
  defaultValues,
  submitLabel,
  onSubmit,
}: {
  defaultValues: PersonFormValues;
  submitLabel: string;
  onSubmit: (request: PersonRequest) => Promise<void>;
}) {
  const { t } = useTranslation('people');
  const form = useZodForm(personSchema, { defaultValues });
  const { errors, isSubmitting } = form.formState;
  const [failed, setFailed] = useState(false);
  const provinceId = useId();
  const languageId = useId();
  const notesId = useId();

  const submit = form.handleSubmit(async (values) => {
    setFailed(false);
    try {
      await onSubmit(toRequest(values));
    } catch (error) {
      if (!applyValidationErrors(error, form.setError, serverFields)) setFailed(true);
    }
  });

  return (
    <form className="flex max-w-2xl flex-col gap-4" onSubmit={submit} noValidate>
      {failed && <FormAlert tone="error">{t('form.unexpectedError')}</FormAlert>}
      <div className="grid gap-4 sm:grid-cols-2">
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
      </div>
      <FormField
        label={t('fields.displayName')}
        hint={t('fields.displayNameHint')}
        autoComplete="off"
        error={errors.displayName}
        {...form.register('displayName')}
      />
      <div className="grid gap-4 sm:grid-cols-2">
        <FormField
          label={t('fields.email')}
          type="email"
          autoComplete="off"
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
      </div>
      <FormField
        label={t('fields.addressLine')}
        autoComplete="off"
        error={errors.addressLine}
        {...form.register('addressLine')}
      />
      <div className="grid gap-4 sm:grid-cols-3">
        <FormField
          label={t('fields.city')}
          autoComplete="off"
          error={errors.city}
          {...form.register('city')}
        />
        <div className="flex flex-col gap-2">
          <Label htmlFor={provinceId}>{t('fields.province')}</Label>
          <select id={provinceId} className={selectClass} {...form.register('province')}>
            <option value="">{t('form.noProvince')}</option>
            {PROVINCES.map((code) => (
              <option key={code} value={code}>
                {t(`provinces.${code}`)}
              </option>
            ))}
          </select>
          <FieldMessage error={errors.province} />
        </div>
        <FormField
          label={t('fields.postalCode')}
          autoComplete="off"
          error={errors.postalCode}
          {...form.register('postalCode')}
        />
      </div>
      <div className="flex flex-col gap-2">
        <Label htmlFor={languageId}>{t('fields.preferredLanguage')}</Label>
        <select id={languageId} className={selectClass} {...form.register('preferredLanguage')}>
          <option value="fr">{t('languages.fr')}</option>
          <option value="en">{t('languages.en')}</option>
        </select>
      </div>
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
      <div className="flex flex-col gap-2">
        <Label htmlFor={notesId}>{t('fields.notes')}</Label>
        <textarea
          id={notesId}
          rows={4}
          className="rounded-md border bg-transparent px-3 py-2 text-sm"
          aria-describedby={`${notesId}-hint`}
          aria-invalid={errors.notes ? true : undefined}
          {...form.register('notes')}
        />
        <p id={`${notesId}-hint`} className="text-xs text-muted-foreground">
          {t('fields.notesHint')}
        </p>
        <FieldMessage error={errors.notes} />
      </div>
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
    <fieldset className="flex flex-col gap-2">
      <legend className="mb-1 text-sm font-medium">{legend}</legend>
      <div className="flex flex-wrap gap-x-6 gap-y-2">
        {PERSON_ROLES.map((role) => (
          <label key={role} className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              className="size-4"
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
      <FieldMessage error={error} />
    </fieldset>
  );
}
