import type { ComponentProps, ReactNode } from 'react';
import { cn } from '../lib/utils';

export type DescriptionItem = {
  label: string;
  value: ReactNode;
  /** Spans the full width (long text, notes). */
  wide?: boolean;
};

/**
 * Read-only facts about a record, label above value. Empty values show `emptyText` (an em dash by default) so the
 * reader knows the field exists but is not filled.
 */
function DescriptionList({
  items,
  emptyText = '—',
  columns = 2,
  className,
  ...props
}: ComponentProps<'dl'> & { items: DescriptionItem[]; emptyText?: string; columns?: 1 | 2 | 3 }) {
  return (
    <dl
      data-slot="description-list"
      className={cn(
        'grid gap-x-8 gap-y-4',
        columns === 2 && 'sm:grid-cols-2',
        columns === 3 && 'sm:grid-cols-2 xl:grid-cols-3',
        className,
      )}
      {...props}
    >
      {items.map(({ label, value, wide }) => {
        const empty = value === null || value === undefined || value === '' || value === false;
        return (
          <div
            key={label}
            className={cn('flex min-w-0 flex-col gap-1', wide && 'sm:col-span-full')}
          >
            <dt className="text-[0.8125rem] text-muted-foreground">{label}</dt>
            <dd
              className={cn(
                'text-sm break-words whitespace-pre-line',
                empty && 'text-muted-foreground/70',
              )}
            >
              {empty ? emptyText : value}
            </dd>
          </div>
        );
      })}
    </dl>
  );
}

export { DescriptionList };
