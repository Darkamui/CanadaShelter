import { z } from 'zod';

/** Message keys shared by every form (namespace `shell`). */
export const formMessages = {
  required: 'shell:forms.required',
  email: 'shell:forms.email',
} as const;

/** A non-blank string, trimmed. Not for passwords, whose spaces count. */
export function requiredText(error: string = formMessages.required) {
  return z.string().trim().min(1, { error });
}

/** A trimmed, required email address. The server stays the authority on what it accepts. */
export function emailAddress() {
  return requiredText().pipe(z.email({ error: formMessages.email }));
}
