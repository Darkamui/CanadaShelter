import { ChevronDown } from 'lucide-react';
import type { ComponentProps } from 'react';
import { cn } from '../lib/utils';
import { controlClass } from './input';

/**
 * A styled native `<select>`. Use it for every form choice: the platform picker is the best one on a tablet, and
 * it stays operable by tests and assistive tech. Radix menus are for actions, not for values.
 */
function NativeSelect({ className, children, ...props }: ComponentProps<'select'>) {
  return (
    <div data-slot="native-select" className={cn('relative min-w-0', className)}>
      <select
        className={cn(
          controlClass,
          'h-10 cursor-pointer appearance-none py-1 pr-9 pl-3 pointer-coarse:h-11',
        )}
        {...props}
      >
        {children}
      </select>
      <ChevronDown
        aria-hidden="true"
        className="pointer-events-none absolute top-1/2 right-3 size-4 -translate-y-1/2 text-muted-foreground"
      />
    </div>
  );
}

export { NativeSelect };
