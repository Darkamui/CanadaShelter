import type { ComponentProps } from 'react';
import { cn } from '../lib/utils';

/** Shared look of every text-like control (input, select, textarea). */
export const controlClass = cn(
  'w-full min-w-0 rounded-md border border-input bg-card text-base text-foreground shadow-card transition-[color,border-color,box-shadow] outline-none md:text-sm',
  'placeholder:text-muted-foreground/80',
  'hover:border-border-strong focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/25',
  'disabled:cursor-not-allowed disabled:bg-muted disabled:opacity-70',
  'aria-invalid:border-danger aria-invalid:ring-danger/15 aria-invalid:focus-visible:ring-danger/25',
);

function Input({ className, type, ...props }: ComponentProps<'input'>) {
  return (
    <input
      type={type}
      data-slot="input"
      className={cn(
        controlClass,
        'flex h-10 px-3 py-1 pointer-coarse:h-11',
        'file:border-0 file:bg-transparent file:text-sm file:font-medium',
        '[&::-webkit-calendar-picker-indicator]:opacity-60',
        className,
      )}
      {...props}
    />
  );
}

export { Input };
