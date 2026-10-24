import type { MovementItem } from '@shelter/api-client/model';
import { z } from 'zod';

/** Server limit (`Movement.NotesMaxLength`). */
export const NOTES_MAX_LENGTH = 2000;

/** Problem codes of the movement 409s (`MovementRecorder` on the server). */
export const CUSTODY_CONFLICT = 'movement.custodyConflict';
export const NOT_LATEST = 'movement.notLatest';

const notes = z.string().max(NOTES_MAX_LENGTH, { error: 'movements:form.tooLong' });
// `datetime-local` value; '' means now.
const occurredAt = z.string();

/**
 * One form for the three movement kinds; the dialog shows only the fields of its kind. `code` is the intake reason
 * or the outcome type; `personId` and `toLocationId` are '' for none. `personRequired` are the outcome codes that
 * need a person.
 */
export function movementSchema(
  kind: MovementKind,
  personRequired: ReadonlySet<string> = new Set(),
) {
  return z
    .object({ code: z.string(), toLocationId: z.string(), personId: z.string(), occurredAt, notes })
    .superRefine((values, ctx) => {
      const required = (path: keyof typeof values) =>
        ctx.addIssue({ code: 'custom', path: [path], message: 'shell:forms.required' });
      if (kind !== 'relocation' && values.code === '') required('code');
      if (kind !== 'outcome' && values.toLocationId === '') required('toLocationId');
      if (kind === 'outcome' && personRequired.has(values.code) && values.personId === '')
        required('personId');
    });
}

export type MovementKind = 'intake' | 'relocation' | 'outcome';
export type MovementFormValues = z.infer<ReturnType<typeof movementSchema>>;

export const emptyMovement: MovementFormValues = {
  code: '',
  toLocationId: '',
  personId: '',
  occurredAt: '',
  notes: '',
};

export const voidSchema = z.object({
  reason: z
    .string()
    .trim()
    .min(1, { error: 'shell:forms.required' })
    .max(NOTES_MAX_LENGTH, { error: 'movements:form.tooLong' }),
});

/** `datetime-local` (local time) to an ISO instant; '' to null, which the server reads as now. */
export function toInstant(value: string): string | null {
  return value === '' ? null : new Date(value).toISOString();
}

export function orNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
}

/**
 * The movement a void may cancel: the newest one neither a void nor voided. The list comes newest first, as the
 * server sends it.
 */
export function latestInEffect(movements: readonly MovementItem[]): MovementItem | undefined {
  return movements.find((m) => m.type !== 'void' && m.voidedByMovementId === null);
}
