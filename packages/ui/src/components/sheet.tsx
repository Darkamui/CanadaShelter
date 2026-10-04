import { X } from 'lucide-react';
import { Dialog as SheetPrimitive } from 'radix-ui';
import type { ComponentProps } from 'react';
import { cn } from '../lib/utils';
import { DialogOverlay } from './dialog';

const Sheet = SheetPrimitive.Root;
const SheetTrigger = SheetPrimitive.Trigger;
const SheetClose = SheetPrimitive.Close;
const SheetTitle = SheetPrimitive.Title;
const SheetDescription = SheetPrimitive.Description;

/** A panel that slides in from the left edge (the navigation on narrow screens). */
function SheetContent({
  className,
  children,
  closeLabel,
  ...props
}: ComponentProps<typeof SheetPrimitive.Content> & { closeLabel: string }) {
  return (
    <SheetPrimitive.Portal>
      <DialogOverlay />
      <SheetPrimitive.Content
        data-slot="sheet-content"
        className={cn(
          'fixed inset-y-0 left-0 z-50 flex w-72 max-w-[85vw] flex-col shadow-raised outline-none data-[state=open]:animate-sheet',
          className,
        )}
        {...props}
      >
        {children}
        <SheetPrimitive.Close
          className="absolute top-3 right-3 inline-flex size-9 items-center justify-center rounded-md opacity-80 transition-opacity hover:opacity-100 focus-visible:ring-[3px] focus-visible:ring-ring/45 focus-visible:outline-none"
          aria-label={closeLabel}
        >
          <X aria-hidden="true" className="size-4" />
        </SheetPrimitive.Close>
      </SheetPrimitive.Content>
    </SheetPrimitive.Portal>
  );
}

export { Sheet, SheetClose, SheetContent, SheetDescription, SheetTitle, SheetTrigger };
