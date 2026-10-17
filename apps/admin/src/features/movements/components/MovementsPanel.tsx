import { useListMovements, useVoidMovement } from '@shelter/api-client/hooks/movements';
import type { AnimalResponse, MovementItem } from '@shelter/api-client/model';
import { Badge } from '@shelter/ui/components/badge';
import { Button } from '@shelter/ui/components/button';
import { Card, CardContent, CardHeader, CardTitle } from '@shelter/ui/components/card';
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@shelter/ui/components/dialog';
import { EmptyState } from '@shelter/ui/components/empty-state';
import { Skeleton } from '@shelter/ui/components/skeleton';
import { toast } from '@shelter/ui/components/toaster';
import { cn } from '@shelter/ui/lib/utils';
import { ArrowRightLeft, LogIn, LogOut, Undo2 } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Permissions } from '../../../lib/auth/permissions';
import { hasPermission, useSession } from '../../../lib/auth/session';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { TextareaField } from '../../../lib/forms/FormField';
import { applyValidationErrors, problemCodeOf } from '../../../lib/forms/serverErrors';
import { useZodForm } from '../../../lib/forms/useZodForm';
import { formatDateTime } from '../../../lib/format';
import { CUSTODY_CONFLICT, latestInEffect, NOT_LATEST, voidSchema } from '../model';
import { useIntakeReasons, useOutcomeTypes } from '../useMovementReference';
import { useRefreshCustody } from '../useRefreshCustody';

const typeIcons: Record<string, typeof LogIn> = {
  intake: LogIn,
  relocation: ArrowRightLeft,
  outcome: LogOut,
  void: Undo2,
};

/**
 * The movement history of one animal, newest first. With `movement.amend`, the latest movement in effect can be
 * voided. The custody buttons are on the record's header (`CustodyActions`).
 */
export function MovementsPanel({ animal }: { animal: AnimalResponse }) {
  const { t } = useTranslation('movements');
  const { data: session } = useSession();
  const canAmend = hasPermission(session, Permissions.movementAmend);
  const movements = useListMovements({ animalId: animal.id });
  const refresh = useRefreshCustody(animal.id);
  const [voiding, setVoiding] = useState<MovementItem | undefined>();
  const [conflict, setConflict] = useState<string | undefined>();

  const latest = movements.data ? latestInEffect(movements.data) : undefined;

  return (
    <Card aria-labelledby="movements-title">
      <CardHeader>
        <CardTitle id="movements-title">{t('title')}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {conflict && <FormAlert tone="error">{conflict}</FormAlert>}
        {movements.isPending ? (
          <div role="status" className="flex flex-col gap-3">
            <span className="sr-only">{t('shell:list.loading')}</span>
            <Skeleton className="h-16" />
            <Skeleton className="h-16" />
          </div>
        ) : movements.isError ? (
          <FormAlert tone="error">{t('loadFailed')}</FormAlert>
        ) : movements.data.length === 0 ? (
          <EmptyState inCard icon={<ArrowRightLeft />} title={t('none')} />
        ) : (
          <ol className="flex flex-col divide-y">
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
      </CardContent>
      <VoidDialog
        movement={voiding}
        onClose={() => setVoiding(undefined)}
        onVoided={async () => {
          setVoiding(undefined);
          setConflict(undefined);
          toast.success(t('voided'));
          await refresh();
        }}
        onConflict={async (text) => {
          setVoiding(undefined);
          setConflict(text);
          await refresh();
        }}
      />
    </Card>
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
  const Icon = typeIcons[movement.type] ?? ArrowRightLeft;
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
    <li className="flex gap-4 py-4 first:pt-0 last:pb-0">
      <span
        aria-hidden="true"
        className={cn(
          'flex size-9 shrink-0 items-center justify-center rounded-full bg-secondary text-secondary-foreground [&_svg]:size-4',
          voided && 'bg-muted text-muted-foreground',
        )}
      >
        <Icon />
      </span>
      <div className="flex min-w-0 flex-1 flex-col gap-1.5">
        <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
          <p className={cn('font-medium', voided && 'text-muted-foreground line-through')}>
            {t(`types.${movement.type}`, { defaultValue: movement.type })}
          </p>
          {voided && <Badge>{t('voidedTag')}</Badge>}
          <time
            dateTime={movement.occurredAt}
            className="tabular ml-auto text-[0.8125rem] text-muted-foreground"
          >
            {formatDateTime(movement.occurredAt, i18n.language)}
          </time>
        </div>
        <dl className="grid gap-x-4 gap-y-0.5 text-sm sm:grid-cols-[max-content_1fr]">
          {rows
            .filter(([, value]) => value)
            .map(([label, value]) => (
              <div key={label} className="contents">
                <dt className="text-muted-foreground">{label}</dt>
                <dd className="whitespace-pre-line">{value}</dd>
              </div>
            ))}
        </dl>
        {canVoid && (
          <Button variant="ghost" size="sm" className="-ml-2.5 self-start" onClick={onVoid}>
            <Undo2 aria-hidden="true" />
            {t('actions.void')}
          </Button>
        )}
      </div>
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
      <TextareaField
        label={t('void.reason')}
        hint={t('void.reasonHint')}
        rows={3}
        error={errors.reason}
        {...form.register('reason')}
      />
      <DialogFooter className="mt-1">
        <DialogClose asChild>
          <Button type="button" variant="ghost">
            {t('shell:forms.cancel')}
          </Button>
        </DialogClose>
        <Button type="submit" variant="destructive" disabled={isSubmitting}>
          {t('void.submit')}
        </Button>
      </DialogFooter>
    </form>
  );
}
