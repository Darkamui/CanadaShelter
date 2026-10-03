import {
  useRecordIntake,
  useRecordOutcome,
  useRecordRelocation,
} from '@shelter/api-client/hooks/movements';
import type { AnimalResponse } from '@shelter/api-client/model';
import { Button } from '@shelter/ui/components/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@shelter/ui/components/dialog';
import { Label } from '@shelter/ui/components/label';
import { useId, useMemo, useState } from 'react';
import { Controller, type FieldError } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Permissions } from '../../../lib/auth/permissions';
import { hasPermission, useSession } from '../../../lib/auth/session';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { FormField } from '../../../lib/forms/FormField';
import { applyValidationErrors, problemCodeOf } from '../../../lib/forms/serverErrors';
import { useZodForm } from '../../../lib/forms/useZodForm';
import {
  CUSTODY_CONFLICT,
  emptyMovement,
  movementSchema,
  orNull,
  toInstant,
  type MovementFormValues,
  type MovementKind,
} from '../model';
import { useHoldingLocations, useIntakeReasons, useOutcomeTypes } from '../useMovementReference';
import { PersonPicker } from './PersonPicker';

// Server field → form field and message (server messages are English only).
const serverFields = {
  reasonCode: ['code', 'movements:form.reasonUnavailable'],
  outcomeCode: ['code', 'movements:form.outcomeUnavailable'],
  toLocationId: ['toLocationId', 'movements:form.locationUnavailable'],
  personId: ['personId', 'movements:form.personInvalid'],
  occurredAt: ['occurredAt', 'movements:form.occurredAtInvalid'],
  notes: ['notes', 'movements:form.tooLong'],
} as const;

const selectClass = 'h-9 rounded-md border bg-transparent px-3 text-sm';

/** Records an intake, a relocation or an outcome for the animal. Closed when `kind` is undefined. */
export function MovementDialog({
  animal,
  kind,
  onClose,
  onRecorded,
  onConflict,
}: {
  animal: AnimalResponse;
  kind: MovementKind | undefined;
  onClose: () => void;
  onRecorded: (kind: MovementKind) => Promise<unknown>;
  /** Someone else changed the animal's custody first: reload it. */
  onConflict: () => Promise<unknown>;
}) {
  const { t } = useTranslation('movements');

  return (
    <Dialog open={kind !== undefined} onOpenChange={(open) => !open && onClose()}>
      <DialogContent closeLabel={t('close')}>
        <DialogHeader>
          <DialogTitle>{kind && t(`dialog.${kind}.title`)}</DialogTitle>
          <DialogDescription>{kind && t(`dialog.${kind}.description`)}</DialogDescription>
        </DialogHeader>
        {kind && (
          <MovementForm
            key={kind}
            animal={animal}
            kind={kind}
            onRecorded={onRecorded}
            onConflict={onConflict}
          />
        )}
      </DialogContent>
    </Dialog>
  );
}

function MovementForm({
  animal,
  kind,
  onRecorded,
  onConflict,
}: {
  animal: AnimalResponse;
  kind: MovementKind;
  onRecorded: (kind: MovementKind) => Promise<unknown>;
  onConflict: () => Promise<unknown>;
}) {
  const { t } = useTranslation('movements');
  const { data: session } = useSession();
  const canPickPerson = hasPermission(session, Permissions.personRead);
  const reasons = useIntakeReasons();
  const outcomes = useOutcomeTypes();
  const locations = useHoldingLocations();
  const personRequired = useMemo(
    () => new Set(outcomes.options.filter((o) => o.requiresPerson).map((o) => o.code)),
    [outcomes.options],
  );
  const schema = useMemo(() => movementSchema(kind, personRequired), [kind, personRequired]);
  const form = useZodForm(schema, { defaultValues: emptyMovement });
  const { errors, isSubmitting } = form.formState;
  const [alert, setAlert] = useState<string | undefined>();
  const codeId = useId();
  const locationId = useId();
  const notesId = useId();

  const intake = useRecordIntake();
  const relocation = useRecordRelocation();
  const outcome = useRecordOutcome();

  const codes = kind === 'intake' ? reasons.options : outcomes.options;
  const targets = locations.options.filter((l) => l.id !== animal.currentLocationId);
  const code = form.watch('code');
  const needsPerson = kind === 'outcome' && personRequired.has(code);

  const record = (values: MovementFormValues) => {
    const common = {
      animalId: animal.id,
      notes: orNull(values.notes),
      occurredAt: toInstant(values.occurredAt),
    };
    const personId = canPickPerson ? orNull(values.personId) : null;
    switch (kind) {
      case 'intake':
        return intake.mutateAsync({
          data: { ...common, reasonCode: values.code, toLocationId: values.toLocationId, personId },
        });
      case 'relocation':
        return relocation.mutateAsync({ data: { ...common, toLocationId: values.toLocationId } });
      case 'outcome':
        return outcome.mutateAsync({ data: { ...common, outcomeCode: values.code, personId } });
    }
  };

  const submit = form.handleSubmit(async (values) => {
    setAlert(undefined);
    try {
      await record(values);
      await onRecorded(kind);
    } catch (error) {
      if (problemCodeOf(error) === CUSTODY_CONFLICT) {
        setAlert(t('form.custodyConflict'));
        await onConflict();
      } else if (!applyValidationErrors(error, form.setError, serverFields)) {
        setAlert(t('form.unexpectedError'));
      }
    }
  });

  return (
    <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
      {alert && <FormAlert tone="error">{alert}</FormAlert>}
      {kind !== 'relocation' && (
        <div className="flex flex-col gap-2">
          <Label htmlFor={codeId}>
            {t(kind === 'intake' ? 'fields.reason' : 'fields.outcome')}
          </Label>
          <select
            id={codeId}
            className={selectClass}
            aria-invalid={errors.code ? true : undefined}
            {...form.register('code')}
          >
            <option value="">{t('form.choose')}</option>
            {codes.map((o) => (
              <option key={o.code} value={o.code}>
                {o.label}
              </option>
            ))}
          </select>
          <FieldMessage error={errors.code} />
        </div>
      )}
      {kind === 'outcome' ? (
        animal.currentLocationName && (
          <p className="text-sm text-muted-foreground">
            {t('fields.leavesFrom', { location: animal.currentLocationName })}
          </p>
        )
      ) : (
        <div className="flex flex-col gap-2">
          <Label htmlFor={locationId}>{t('fields.toLocation')}</Label>
          <select
            id={locationId}
            className={selectClass}
            aria-invalid={errors.toLocationId ? true : undefined}
            {...form.register('toLocationId')}
          >
            <option value="">{t('form.choose')}</option>
            {targets.map((l) => (
              <option key={l.id} value={l.id}>
                {l.label}
              </option>
            ))}
          </select>
          {locations.status === 'success' && targets.length === 0 && (
            <p className="text-sm text-muted-foreground">{t('form.noLocations')}</p>
          )}
          <FieldMessage error={errors.toLocationId} />
        </div>
      )}
      {kind !== 'relocation' && canPickPerson && (
        <Controller
          control={form.control}
          name="personId"
          render={({ field }) => (
            <PersonPicker
              label={t(needsPerson ? 'fields.personRequired' : 'fields.person')}
              value={field.value}
              onChange={field.onChange}
              error={errors.personId}
            />
          )}
        />
      )}
      <FormField
        type="datetime-local"
        label={t('fields.occurredAt')}
        hint={t('fields.occurredAtHint')}
        error={errors.occurredAt}
        {...form.register('occurredAt')}
      />
      <div className="flex flex-col gap-2">
        <Label htmlFor={notesId}>{t('fields.notes')}</Label>
        <textarea
          id={notesId}
          rows={3}
          className="rounded-md border bg-transparent px-3 py-2 text-sm"
          aria-describedby={`${notesId}-hint`}
          aria-invalid={errors.notes ? true : undefined}
          {...form.register('notes')}
        />
        <p id={`${notesId}-hint`} className="text-xs text-muted-foreground">
          {t(kind === 'relocation' ? 'fields.notesHintNoPerson' : 'fields.notesHint')}
        </p>
        <FieldMessage error={errors.notes} />
      </div>
      <Button type="submit" className="self-start" disabled={isSubmitting}>
        {t(`dialog.${kind}.submit`)}
      </Button>
    </form>
  );
}

function FieldMessage({ error }: { error?: Pick<FieldError, 'message'> }) {
  const { t } = useTranslation();
  return error?.message ? <p className="text-sm text-destructive">{t(error.message)}</p> : null;
}
