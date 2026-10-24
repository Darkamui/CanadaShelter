import type { ComponentProps } from 'react';
import { cn } from '../lib/utils';

/**
 * How full something is (an enclosure against its capacity). Turns amber from 85 % and brick at capacity. The caller
 * gives the accessible name (`aria-label`) and shows the numbers as text next to it.
 */
function Meter({
  value,
  max,
  className,
  ...props
}: ComponentProps<'div'> & { value: number; max: number }) {
  const ratio = max > 0 ? value / max : 0;
  const tone = ratio >= 1 ? 'bg-danger' : ratio >= 0.85 ? 'bg-warning' : 'bg-success';

  return (
    <div
      role="meter"
      aria-valuemin={0}
      aria-valuemax={max}
      aria-valuenow={value}
      data-slot="meter"
      className={cn('h-1.5 w-full overflow-hidden rounded-full bg-muted', className)}
      {...props}
    >
      <div
        className={cn('h-full rounded-full transition-[width]', tone)}
        style={{ width: `${Math.min(100, Math.round(ratio * 100))}%` }}
      />
    </div>
  );
}

export { Meter };
