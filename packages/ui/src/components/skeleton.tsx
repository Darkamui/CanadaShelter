import type { ComponentProps } from 'react';
import { cn } from '../lib/utils';

/** Placeholder shape while content loads. Pair it with a visually hidden status text. */
function Skeleton({ className, ...props }: ComponentProps<'div'>) {
  return (
    <div
      data-slot="skeleton"
      aria-hidden="true"
      className={cn('animate-pulse rounded-md bg-muted', className)}
      {...props}
    />
  );
}

export { Skeleton };
