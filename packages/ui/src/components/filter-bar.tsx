import type { ComponentProps } from 'react';
import { cn } from '../lib/utils';

/** The search and filters above a list. Fields sit side by side and wrap; the submit button aligns with them. */
function FilterBar({ className, ...props }: ComponentProps<'form'>) {
  return (
    <form
      role="search"
      data-slot="filter-bar"
      className={cn(
        'flex flex-wrap items-end gap-3 [&_[data-slot=field]]:min-w-48 [&_[data-slot=field]]:flex-1 sm:[&_[data-slot=field]]:max-w-xs',
        className,
      )}
      {...props}
    />
  );
}

export { FilterBar };
