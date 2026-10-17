import type { ComponentProps, ReactNode } from 'react';
import { cn } from '../lib/utils';

/**
 * Nothing to show yet: say what would be here and offer the action that fills it. `inCard` drops the dashed frame
 * when the card around it already draws one.
 */
function EmptyState({
  inCard = false,
  icon,
  title,
  description,
  action,
  className,
  ...props
}: Omit<ComponentProps<'div'>, 'title'> & {
  icon?: ReactNode;
  title: ReactNode;
  description?: ReactNode;
  action?: ReactNode;
  inCard?: boolean;
}) {
  return (
    <div
      data-slot="empty-state"
      className={cn(
        'flex flex-col items-center gap-3 px-6 text-center',
        inCard ? 'py-6' : 'rounded-xl border border-dashed border-border-strong bg-subtle py-10',
        className,
      )}
      {...props}
    >
      {icon && (
        <div className="flex size-11 items-center justify-center rounded-full bg-secondary text-secondary-foreground [&_svg]:size-5">
          {icon}
        </div>
      )}
      <div className="flex max-w-sm flex-col gap-1">
        <p className="font-medium">{title}</p>
        {description && <p className="text-sm text-muted-foreground">{description}</p>}
      </div>
      {action}
    </div>
  );
}

export { EmptyState };
