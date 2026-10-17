import { cva, type VariantProps } from 'class-variance-authority';
import { Slot } from 'radix-ui';
import type { ComponentProps } from 'react';
import { cn } from '../lib/utils';

const buttonVariants = cva(
  "inline-flex shrink-0 cursor-pointer items-center justify-center gap-2 rounded-md text-sm font-medium whitespace-nowrap transition-[color,background-color,border-color,box-shadow] outline-none select-none focus-visible:ring-[3px] focus-visible:ring-ring/45 disabled:pointer-events-none disabled:opacity-50 aria-disabled:pointer-events-none aria-disabled:opacity-50 [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4",
  {
    variants: {
      variant: {
        /** The one main action of a page, dialog or form. */
        default: 'bg-primary text-primary-foreground shadow-card hover:bg-primary-hover',
        /** Secondary actions next to a primary one. */
        outline:
          'border border-border-strong bg-card text-foreground shadow-card hover:border-ring/50 hover:bg-accent hover:text-accent-foreground',
        /** Low-emphasis, tinted: filters, "add" inside a section. */
        secondary: 'bg-secondary text-secondary-foreground hover:bg-secondary/70',
        /** Toolbars, row actions, menus. */
        ghost: 'text-foreground hover:bg-accent hover:text-accent-foreground',
        /** Irreversible or destructive actions. */
        destructive: 'bg-danger text-white shadow-card hover:bg-danger/90',
        link: 'h-auto px-0 text-primary underline-offset-4 hover:underline',
      },
      size: {
        default: 'h-10 px-4 pointer-coarse:h-11',
        sm: 'h-8 gap-1.5 px-3 pointer-coarse:h-10',
        lg: 'h-11 px-6 text-[0.9375rem]',
        icon: 'size-10 pointer-coarse:size-11',
        'icon-sm': 'size-8 pointer-coarse:size-10',
      },
    },
    defaultVariants: {
      variant: 'default',
      size: 'default',
    },
  },
);

function Button({
  className,
  variant,
  size,
  asChild = false,
  type,
  ...props
}: ComponentProps<'button'> & VariantProps<typeof buttonVariants> & { asChild?: boolean }) {
  const Comp = asChild ? Slot.Root : 'button';

  return (
    <Comp
      data-slot="button"
      type={asChild ? undefined : (type ?? 'button')}
      className={cn(buttonVariants({ variant, size, className }))}
      {...props}
    />
  );
}

export { Button, buttonVariants };
