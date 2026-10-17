import type { ComponentProps, ReactNode } from 'react';
import { cn } from '../lib/utils';
import type { Tone } from './badge';

const edgeClass: Record<Tone, string> = {
  neutral: 'bg-border-strong',
  primary: 'bg-primary',
  success: 'bg-success',
  warning: 'bg-warning',
  danger: 'bg-danger',
  info: 'bg-info',
};

export type RecordFact = { label: string; value: ReactNode };

/**
 * The top of a record page, drawn like the card clipped to a kennel door: an edge in the record's status tone, its
 * picture or initials, the name as the page's `h1`, status and alerts as badges, the record's actions, the facts
 * staff check first, and the record's tabs along the bottom edge.
 */
function RecordHeader({
  tone = 'neutral',
  avatar,
  title,
  status,
  chips,
  actions,
  facts,
  tabs,
  className,
  ...props
}: Omit<ComponentProps<'header'>, 'title'> & {
  /** Colors the left edge: the record's status at a glance. */
  tone?: Tone;
  avatar?: ReactNode;
  /** The `h1` content, e.g. the number and the name. */
  title: ReactNode;
  /** Badges next to the name (custody status, archived). */
  status?: ReactNode;
  /** A row of badges under the name (alerts, roles). */
  chips?: ReactNode;
  actions?: ReactNode;
  facts?: RecordFact[];
  tabs?: ReactNode;
}) {
  return (
    <header
      data-slot="record-header"
      className={cn(
        'relative overflow-hidden rounded-xl border bg-card text-card-foreground shadow-card',
        className,
      )}
      {...props}
    >
      <span aria-hidden="true" className={cn('absolute inset-y-0 left-0 w-1.5', edgeClass[tone])} />
      <div className="flex flex-wrap items-start gap-x-5 gap-y-4 py-5 pr-5 pl-7">
        {avatar}
        <div className="flex min-w-0 flex-1 basis-64 flex-col gap-2.5">
          <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
            <h1 className="font-display text-[2rem] leading-[1.1] font-semibold tracking-[-0.02em] break-words">
              {title}
            </h1>
            {status}
          </div>
          {chips && <div className="flex flex-wrap items-center gap-2">{chips}</div>}
        </div>
        {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
      </div>
      {facts && facts.length > 0 && (
        <dl className="grid grid-cols-2 gap-x-6 gap-y-3 border-t bg-subtle py-3.5 pr-5 pl-7 sm:grid-cols-[repeat(auto-fit,minmax(10rem,1fr))]">
          {facts.map(({ label, value }) => (
            <div key={label} className="flex min-w-0 flex-col gap-0.5">
              <dt className="text-[0.8125rem] text-muted-foreground">{label}</dt>
              <dd className="truncate text-sm font-medium">{value}</dd>
            </div>
          ))}
        </dl>
      )}
      {tabs && <div className="border-t pl-5">{tabs}</div>}
    </header>
  );
}

export { RecordHeader };
