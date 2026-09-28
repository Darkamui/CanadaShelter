import { zodResolver } from '@hookform/resolvers/zod';
import { useForm, type FieldValues, type UseFormProps } from 'react-hook-form';
import type { z } from 'zod';

/**
 * React Hook Form validated by a Zod schema. Errors appear on submit, then update as the user types.
 * Schema messages are i18n keys with their namespace (`shell:forms.required`); `FormField` translates them.
 */
export function useZodForm<TInput extends FieldValues, TOutput extends FieldValues>(
  schema: z.ZodType<TOutput, TInput>,
  // NoInfer: the schema alone decides the field types, not the default values.
  props?: Omit<UseFormProps<NoInfer<TInput>, unknown, NoInfer<TOutput>>, 'resolver'>,
) {
  return useForm<TInput, unknown, TOutput>({
    resolver: zodResolver(schema),
    mode: 'onSubmit',
    reValidateMode: 'onChange',
    ...props,
  });
}
