import type { ComponentProps } from 'react';
import { cn } from '../lib/utils';

/**
 * A native checkbox in the brand colour. Native keeps the label click, the form value and the keyboard behaviour for
 * free; wrap it in a `<label>` with its text.
 */
function Checkbox({ className, ...props }: Omit<ComponentProps<'input'>, 'type'>) {
  return (
    <input
      type="checkbox"
      data-slot="checkbox"
      className={cn(
        'size-[1.125rem] shrink-0 cursor-pointer rounded-sm accent-primary outline-none focus-visible:ring-[3px] focus-visible:ring-ring/45 disabled:cursor-not-allowed disabled:opacity-50',
        className,
      )}
      {...props}
    />
  );
}

export { Checkbox };
