import type { ComponentProps } from 'react';
import { cn } from '../lib/utils';
import { controlClass } from './input';

function Textarea({ className, rows = 3, ...props }: ComponentProps<'textarea'>) {
  return (
    <textarea
      data-slot="textarea"
      rows={rows}
      className={cn(
        controlClass,
        'field-sizing-content min-h-16 px-3 py-2 leading-relaxed',
        className,
      )}
      {...props}
    />
  );
}

export { Textarea };
