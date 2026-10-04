import type { ComponentProps } from 'react';
import { cn } from '../lib/utils';

/**
 * Route tabs: each tab is a link to a nested route, so a tab can be bookmarked and the back button works. The app
 * renders its router's links with {@link tabClass}; the active one carries `aria-current="page"`.
 */
function TabNav({ className, ...props }: ComponentProps<'nav'>) {
  return (
    <nav
      data-slot="tab-nav"
      className={cn('-mb-px flex gap-1 overflow-x-auto', className)}
      {...props}
    />
  );
}

const tabClass =
  "relative inline-flex h-11 shrink-0 items-center gap-2 rounded-t-md px-3 text-sm font-medium whitespace-nowrap text-muted-foreground transition-colors outline-none after:absolute after:inset-x-2 after:bottom-0 after:h-0.5 after:rounded-full after:content-[''] hover:text-foreground focus-visible:ring-[3px] focus-visible:ring-ring/45 aria-[current=page]:text-foreground aria-[current=page]:after:bg-primary [&_svg]:size-4";

/** A count after a tab label. */
function TabCount({ className, ...props }: ComponentProps<'span'>) {
  return (
    <span
      className={cn(
        'tabular rounded-full bg-muted px-1.5 text-xs leading-5 text-muted-foreground',
        className,
      )}
      {...props}
    />
  );
}

export { TabCount, tabClass, TabNav };
