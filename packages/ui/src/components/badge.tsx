import { cva, type VariantProps } from 'class-variance-authority';
import type { ComponentProps } from 'react';
import { cn } from '../lib/utils';

/** Status tones. Map domain states to a tone once (in the feature's model), never pick colors per screen. */
export type Tone = 'neutral' | 'primary' | 'success' | 'warning' | 'danger' | 'info';

const badgeVariants = cva(
  "inline-flex w-fit shrink-0 items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-medium whitespace-nowrap [&_svg]:pointer-events-none [&_svg:not([class*='size-'])]:size-3.5",
  {
    variants: {
      tone: {
        neutral: 'border-border-strong/70 bg-muted text-foreground/80',
        primary: 'border-primary/20 bg-secondary text-secondary-foreground',
        success: 'border-success-border bg-success-soft text-success',
        warning: 'border-warning-border bg-warning-soft text-warning',
        danger: 'border-danger-border bg-danger-soft text-danger',
        info: 'border-info-border bg-info-soft text-info',
      },
    },
    defaultVariants: { tone: 'neutral' },
  },
);

const dotClass: Record<Tone, string> = {
  neutral: 'bg-muted-foreground',
  primary: 'bg-primary',
  success: 'bg-success',
  warning: 'bg-warning',
  danger: 'bg-danger',
  info: 'bg-info',
};

/** A short status label. `dot` prefixes a colored dot (states); icons can be passed as children (alerts). */
function Badge({
  className,
  tone,
  dot = false,
  children,
  ...props
}: ComponentProps<'span'> & VariantProps<typeof badgeVariants> & { dot?: boolean }) {
  return (
    <span data-slot="badge" className={cn(badgeVariants({ tone }), className)} {...props}>
      {dot && (
        <span
          aria-hidden="true"
          className={cn('size-1.5 rounded-full', dotClass[tone ?? 'neutral'])}
        />
      )}
      {children}
    </span>
  );
}

export { Badge, badgeVariants };
