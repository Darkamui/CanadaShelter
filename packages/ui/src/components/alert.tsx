import { CircleAlert, CircleCheck, Info, TriangleAlert } from 'lucide-react';
import type { ComponentProps, ReactNode } from 'react';
import { cn } from '../lib/utils';

export type AlertTone = 'info' | 'success' | 'warning' | 'danger';

const toneClass: Record<AlertTone, string> = {
  info: 'border-info-border bg-info-soft [&>svg]:text-info',
  success: 'border-success-border bg-success-soft [&>svg]:text-success',
  warning: 'border-warning-border bg-warning-soft [&>svg]:text-warning',
  danger: 'border-danger-border bg-danger-soft [&>svg]:text-danger',
};

const toneIcon = { info: Info, success: CircleCheck, warning: TriangleAlert, danger: CircleAlert };

/**
 * A message that stays on the page until its cause goes away: a failed save, a notice about the record. Transient
 * confirmations ("Saved") are toasts instead. `danger` is announced as an alert, the others as a status.
 */
function Alert({
  tone = 'info',
  title,
  icon,
  className,
  children,
  ...props
}: Omit<ComponentProps<'div'>, 'title'> & {
  tone?: AlertTone;
  title?: ReactNode;
  icon?: ReactNode;
}) {
  const Icon = toneIcon[tone];

  return (
    <div
      data-slot="alert"
      role={tone === 'danger' ? 'alert' : 'status'}
      className={cn(
        'grid grid-cols-[1.125rem_1fr] items-start gap-x-3 rounded-lg border px-4 py-3 text-sm [&>svg]:mt-0.5 [&>svg]:size-[1.125rem]',
        toneClass[tone],
        className,
      )}
      {...props}
    >
      {icon ?? <Icon aria-hidden="true" />}
      <div className="col-start-2 flex min-w-0 flex-col gap-1">
        {title && <p className="font-semibold">{title}</p>}
        {children && <div className={cn(title && 'text-foreground/80')}>{children}</div>}
      </div>
    </div>
  );
}

export { Alert };
