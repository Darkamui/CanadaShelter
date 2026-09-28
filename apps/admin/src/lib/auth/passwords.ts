import { z } from 'zod';

/** The server's policy (ADR 0018): 12 characters or more, no composition rules. The server decides. */
export const MIN_PASSWORD_LENGTH = 12;

/** The new-password and confirmation fields of a form. */
export const newPasswordFields = {
  password: z.string().min(MIN_PASSWORD_LENGTH, { error: 'platform:password.tooShort' }),
  confirmation: z.string(),
};

/** The confirmation must repeat the password: `.refine(passwordsMatch.check, passwordsMatch.params)`. */
export const passwordsMatch = {
  check: (value: { password: string; confirmation: string }) =>
    value.password === value.confirmation,
  params: { error: 'platform:password.mismatch', path: ['confirmation'] },
};
