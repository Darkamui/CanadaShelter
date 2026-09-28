import { ApiError } from '@shelter/api-client/http';
import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';

/** The `errors` of a 400 ValidationProblem, keyed by request field; undefined for any other failure. */
export function validationErrors(error: unknown): Record<string, string[]> | undefined {
  if (!(error instanceof ApiError) || error.status !== 400) return undefined;
  const errors = error.problem?.errors;
  return typeof errors === 'object' && errors !== null
    ? (errors as Record<string, string[]>)
    : undefined;
}

/**
 * Puts a ValidationProblem's errors on the form's fields. Server messages are English only, so each request
 * field maps to a form field and a catalog key. Returns false when no field matched: show a form-level alert.
 */
export function applyValidationErrors<T extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<T>,
  fields: Partial<Record<string, readonly [field: Path<T>, message: string]>>,
): boolean {
  let applied = false;
  for (const key of Object.keys(validationErrors(error) ?? {})) {
    const target = fields[key] ?? fields[key.charAt(0).toLowerCase() + key.slice(1)];
    if (!target) continue;
    setError(target[0], { type: 'server', message: target[1] }, { shouldFocus: !applied });
    applied = true;
  }
  return applied;
}
