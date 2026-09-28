/** The server's policy (ADR 0018): 12 characters or more, no composition rules. The server decides. */
export const MIN_PASSWORD_LENGTH = 12;

export type PasswordProblem = 'tooShort' | 'mismatch' | undefined;

/** Client-side check before sending a new password. */
export function passwordProblem(password: string, confirmation: string): PasswordProblem {
  if (password.length < MIN_PASSWORD_LENGTH) return 'tooShort';
  if (password !== confirmation) return 'mismatch';
  return undefined;
}
