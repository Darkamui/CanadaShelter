import type { ComponentProps, ReactNode } from 'react';
import { cn } from '../lib/utils';
import { Button } from './button';
import { Tooltip, TooltipContent, TooltipTrigger } from './tooltip';

/**
 * A row action as an icon button: `label` is its full accessible name ("Edit Kennel 2"), `hint` the short verb shown
 * as a tooltip. Use for repeated per-row actions, where text buttons would crowd the row.
 */
function IconAction({
  label,
  hint,
  className,
  children,
  ...props
}: Omit<ComponentProps<typeof Button>, 'size' | 'variant' | 'aria-label'> & {
  label: string;
  hint: string;
  children: ReactNode;
}) {
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <Button
          size="icon-sm"
          variant="ghost"
          aria-label={label}
          className={cn('text-muted-foreground hover:text-foreground', className)}
          {...props}
        >
          {children}
        </Button>
      </TooltipTrigger>
      <TooltipContent>{hint}</TooltipContent>
    </Tooltip>
  );
}

export { IconAction };
