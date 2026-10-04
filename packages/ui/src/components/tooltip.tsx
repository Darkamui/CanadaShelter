import { Tooltip as TooltipPrimitive } from 'radix-ui';
import type { ComponentProps } from 'react';
import { cn } from '../lib/utils';

/** Mount once near the root. */
function TooltipProvider({
  delayDuration = 300,
  ...props
}: ComponentProps<typeof TooltipPrimitive.Provider>) {
  return <TooltipPrimitive.Provider delayDuration={delayDuration} {...props} />;
}

const Tooltip = TooltipPrimitive.Root;
const TooltipTrigger = TooltipPrimitive.Trigger;

/**
 * A hint for a control that already has an accessible name (an icon button's `aria-label`). Never put information
 * only in a tooltip: touch screens cannot hover.
 */
function TooltipContent({
  className,
  sideOffset = 6,
  ...props
}: ComponentProps<typeof TooltipPrimitive.Content>) {
  return (
    <TooltipPrimitive.Portal>
      <TooltipPrimitive.Content
        data-slot="tooltip-content"
        sideOffset={sideOffset}
        className={cn(
          'z-50 rounded-md bg-foreground px-2.5 py-1.5 text-xs font-medium text-background shadow-raised data-[state=delayed-open]:animate-in',
          className,
        )}
        {...props}
      />
    </TooltipPrimitive.Portal>
  );
}

export { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger };
