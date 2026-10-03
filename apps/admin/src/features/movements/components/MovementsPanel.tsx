import {
  getGetAnimalQueryKey,
  getGetAnimalTimelineQueryKey,
  getListAnimalsQueryKey,
} from '@shelter/api-client/hooks/animals';
import {
  getListMovementsQueryKey,
  useListMovements,
  useVoidMovement,
} from '@shelter/api-client/hooks/movements';
import type { AnimalResponse, MovementItem } from '@shelter/api-client/model';
import { Button } from '@shelter/ui/components/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@shelter/ui/components/dialog';
import { Label } from '@shelter/ui/components/label';
import { useQueryClient } from '@tanstack/react-query';
import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Permissions } from '../../../lib/auth/permissions';
import { hasPermission, useSession } from '../../../lib/auth/session';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { applyValidationErrors, problemCodeOf } from '../../../lib/forms/serverErrors';
import { useZodForm } from '../../../lib/forms/useZodForm';
import { formatDateTime } from '../../../lib/format';
import {
  CUSTODY_CONFLICT,
  latestInEffect,
  NOT_LATEST,
  voidSchema,
  type MovementKind,
} from '../model';
import { useIntakeReasons, useOutcomeTypes } from '../useMovementReference';
import { MovementDialog } from './MovementDialog';

/**
 * Custody actions and the movement history of one animal. Intake when not in care (or after an outcome), move and
 * outcome while in care, all with `movement.write`; with `movement.amend`, the latest movement in effect can be
 * voided.
 */
export function MovementsPanel({
  animal,
  startIntake = false,
}: {
  animal: AnimalResponse;
  /** Opens the intake dialog at once (from the create page). */
  startIntake?: boolean;
}) {
  const { t } = useTranslation('movements');
  const { data: session } = useSession();
  const canWrite = hasPermission(session, Permissions.movementWrite);
  const canAmend = hasPermission(session, Permissions.movementAmend);
  const queryClient = useQueryClient();
  const movements = useListMovements({ animalId: animal.id });
  const inCare = animal.custodyStatus === 'in_care';
  const [kind, setKind] = useState<MovementKind | undefined>(
    startIntake && canWrite && !inCare ? 'intake' : undefined,
  );
  const [voiding, setVoiding] = useState<MovementItem | undefined>();
  const [message, setMessage] = useState<{ tone: 'error' | 'info'; text: string } | undefined>();

  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: getGetAnimalQueryKey(animal.id) }),
      queryClient.invalidateQueries({ queryKey: getGetAnimalTimelineQueryKey(animal.id) }),
      queryClient.invalidateQueries({
        queryKey: getListMovementsQueryKey({ animalId: animal.id }),
      }),
      queryClient.invalidateQueries({ queryKey: getListAnimalsQueryKey() }),
    ]);

  const latest = movements.data ? latestInEffect(movements.data) : undefined;

  return (
    <section aria-labelledby="movements-title" className="flex max-w-2xl flex-col gap-3">
      <h2 id="movements-title" className="text-lg font-semibold">
        {t('title')}
      </h2>
      {message && <FormAlert tone={message.tone}>{message.text}</FormAlert>}
      {canWrite && (
        <div className="flex flex-wrap gap-2">
          {inCare ? (
            <>
              <Button variant="outline" onClick={() => setKind('relocation')}>
                {t('actions.relocation')}
              </Button>
              <Button variant="outline" onClick={() => setKind('outcome')}>
                {t('actions.outcome')}
              </Button>
            </>
          ) : (
            <Button onClick={() => setKind('intake')}>{t('actions.intake')}</Button>
          )}
        </div>
      )}
      {movements.isPending ? (
        <p role="status">{t('shell:list.loading')}</p>
      ) : movements.isError ? (
        <p role="alert" className="text-destructive">
          {t('loadFailed')}
        </p>
      ) : movements.data.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t('none')}</p>
      ) : (
        <ol className="flex flex-col gap-3">
          {movements.data.map((movement) => (
            <MovementEntry
              key={movement.id}
              movement={movement}
              canVoid={canAmend && movement.id === latest?.id}
              onVoid={() => setVoiding(movement)}
            />
          ))}
        </ol>
      )}
      <MovementDialog
        animal={animal}
        kind={kind}
        onClose={() => setKind(undefined)}
        onRecorded={async (recorded) => {
          setKind(undefined);
          setMessage({ tone: 'info', text: t(`recorded.${recorded}`) });
          await refresh();
        }}
        onConflict={refresh}
      />
      <VoidDialog
        movement={voiding}
        onClose={() => setVoiding(undefined)}
        onVoided={async () => {
          setVoiding(undefined);
          setMessage({ tone: 'info', text: t('voided') });
          await refresh();
        }}
        onConflict={async (text) => {
          setVoiding(undefined);
          setMessage({ tone: 'error', text });
          await refresh();
        }}
      />
    </section>
  );
}

function MovementEntry({
  movement,
  canVoid,
  onVoid,
}: {
  movement: MovementItem;
  canVoid: boolean;
  onVoid: () => void;
}) {
  const { t, i18n } = useTranslation('movements');
  const reasons = useIntakeReasons();
  const outcomes = useOutcomeTypes();
  const voided = movement.voidedByMovementId !== null;
  const code = movement.reasonCode
    ? movement.type === 'intake'
      ? reasons.labelOf(movement.reasonCode)
      : outcomes.labelOf(movement.reasonCode)
    : null;
  const rows: [string, string | null][] = [
    [t(movement.type === 'intake' ? 'fields.reason' : 'fields.outcome'), code],
    [t('fields.fromLocation'), movement.fromLocationName],
    [t('fields.toLocation'), movement.toLocationName],
    [t('fields.person'), movement.personName],
    [t(movement.type === 'void' ? 'fields.voidReason' : 'fields.notes'), movement.notes],
  ];

  return (
    <li className="flex flex-col gap-1 rounded-md border p-3">
      <div className="flex flex-wrap items-center gap-x-3">
        <p className={voided ? 'font-medium line-through' : 'font-medium'}>
          {t(`types.${movement.type}`, { defaultValue: movement.type })}
        </p>
        {voided && <span className="text-sm text-muted-foreground">{t('voidedTag')}</span>}
        {canVoid && (
          <Button variant="outline" size="sm" className="ml-auto" onClick={onVoid}>
            {t('actions.void')}
          </Button>
        )}
      </div>
      <p className="text-sm text-muted-foreground">
        <time dateTime={movement.occurredAt}>
          {formatDateTime(movement.occurredAt, i18n.language)}
        </time>
      </p>
      <dl className="grid gap-x-4 text-sm sm:grid-cols-[auto_1fr]">
        {rows
          .filter(([, value]) => value)
          .map(([label, value]) => (
            <div key={label} className="contents">
              <dt className="text-muted-foreground">{label}</dt>
              <dd className="whitespace-pre-line">{value}</dd>
            </div>
          ))}
      </dl>
    </li>
  );
}

const voidServerFields = { reason: ['reason', 'movements:form.tooLong'] } as const;

function VoidDialog({
  movement,
  onClose,
  onVoided,
  onConflict,
}: {
  movement: MovementItem | undefined;
  onClose: () => void;
  onVoided: () => Promise<unknown>;
  onConflict: (message: string) => Promise<unknown>;
}) {
  const { t } = useTranslation('movements');

  return (
    <Dialog open={movement !== undefined} onOpenChange={(open) => !open && onClose()}>
      <DialogContent closeLabel={t('close')}>
        <DialogHeader>
          <DialogTitle>{t('void.title')}</DialogTitle>
          <DialogDescription>
            {movement &&
              t('void.description', {
                type: t(`types.${movement.type}`, { defaultValue: movement.type }),
              })}
          </DialogDescription>
        </DialogHeader>
        {movement && (
          <VoidForm
            key={movement.id}
            movement={movement}
            onVoided={onVoided}
            onConflict={onConflict}
          />
        )}
      </DialogContent>
    </Dialog>
  );
}

function VoidForm({
  movement,
  onVoided,
  onConflict,
}: {
  movement: MovementItem;
  onVoided: () => Promise<unknown>;
  onConflict: (message: string) => Promise<unknown>;
}) {
  const { t } = useTranslation('movements');
  const voidMovement = useVoidMovement();
  const form = useZodForm(voidSchema, { defaultValues: { reason: '' } });
  const { errors, isSubmitting } = form.formState;
  const [failed, setFailed] = useState(false);
  const reasonId = useId();
  const message = errors.reason?.message ? t(errors.reason.message) : undefined;

  const submit = form.handleSubmit(async ({ reason }) => {
    setFailed(false);
    try {
      await voidMovement.mutateAsync({ movementId: movement.id, data: { reason } });
      await onVoided();
    } catch (error) {
      const code = problemCodeOf(error);
      if (code === NOT_LATEST) await onConflict(t('void.notLatest'));
      else if (code === CUSTODY_CONFLICT) await onConflict(t('void.custodyConflict'));
      else if (!applyValidationErrors(error, form.setError, voidServerFields)) setFailed(true);
    }
  });

  return (
    <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
      {failed && <FormAlert tone="error">{t('form.unexpectedError')}</FormAlert>}
      <div className="flex flex-col gap-2">
        <Label htmlFor={reasonId}>{t('void.reason')}</Label>
        <textarea
          id={reasonId}
          rows={3}
          className="rounded-md border bg-transparent px-3 py-2 text-sm"
          aria-describedby={`${reasonId}-hint`}
          aria-invalid={message ? true : undefined}
          {...form.register('reason')}
        />
        <p id={`${reasonId}-hint`} className="text-xs text-muted-foreground">
          {t('void.reasonHint')}
        </p>
        {message && <p className="text-sm text-destructive">{message}</p>}
      </div>
      <Button type="submit" className="self-start" disabled={isSubmitting}>
        {t('void.submit')}
      </Button>
    </form>
  );
}
