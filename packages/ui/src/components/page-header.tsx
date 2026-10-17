import type { ComponentProps, ReactNode } from 'react';
import { cn } from '../lib/utils';

/**
 * The top of every page: an optional back link, the one `h1`, a short description and the page's actions (primary
 * last). Records use `RecordHeader` instead.
 */
function PageHeader({
  title,
  description,
  actions,
  above,
  meta,
  className,
  ...props
}: Omit<ComponentProps<'header'>, 'title'> & {
  title: ReactNode;
  description?: ReactNode;
  actions?: ReactNode;
  /** Back link or breadcrumb. */
  above?: ReactNode;
  /** Next to the title: a count, a status badge. */
  meta?: ReactNode;
}) {
  return (
    <header data-slot="page-header" className={cn('flex flex-col gap-3', className)} {...props}>
      {above}
      <div className="flex flex-wrap items-end justify-between gap-x-6 gap-y-3">
        <div className="flex min-w-0 flex-col gap-1">
          <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
            <h1 className="font-display text-[1.75rem] leading-tight font-semibold tracking-[-0.015em]">
              {title}
            </h1>
            {meta}
          </div>
          {description && (
            <p className="max-w-prose text-sm text-muted-foreground">{description}</p>
          )}
        </div>
        {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
      </div>
    </header>
  );
}

export { PageHeader };
