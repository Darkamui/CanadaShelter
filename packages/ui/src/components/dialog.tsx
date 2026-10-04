import { X } from 'lucide-react';
import { Dialog as DialogPrimitive } from 'radix-ui';
import type { ComponentProps } from 'react';
import { cn } from '../lib/utils';

const Dialog = DialogPrimitive.Root;
const DialogTrigger = DialogPrimitive.Trigger;
const DialogClose = DialogPrimitive.Close;

function DialogOverlay({ className, ...props }: ComponentProps<typeof DialogPrimitive.Overlay>) {
  return (
    <DialogPrimitive.Overlay
      data-slot="dialog-overlay"
      className={cn(
        'fixed inset-0 z-50 bg-[oklch(0.22_0.03_182/0.42)] backdrop-blur-[2px] data-[state=open]:animate-overlay',
        className,
      )}
      {...props}
    />
  );
}

/** A focused task over the page: one action, its fields, Cancel and the primary button in the footer. */
function DialogContent({
  className,
  children,
  closeLabel,
  ...props
}: ComponentProps<typeof DialogPrimitive.Content> & {
  /** Accessible name of the close button, translated by the caller. */
  closeLabel: string;
}) {
  return (
    <DialogPrimitive.Portal>
      <DialogOverlay />
      <DialogPrimitive.Content
        data-slot="dialog-content"
        className={cn(
          'fixed top-1/2 left-1/2 z-50 grid max-h-[90vh] w-full max-w-[calc(100%-2rem)] -translate-x-1/2 -translate-y-1/2 gap-5 overflow-y-auto rounded-xl border bg-popover p-6 text-popover-foreground shadow-raised outline-none data-[state=open]:animate-in sm:max-w-lg',
          className,
        )}
        {...props}
      >
        {children}
        <DialogPrimitive.Close
          className="absolute top-4 right-4 inline-flex size-8 items-center justify-center rounded-md text-muted-foreground transition-colors hover:bg-accent hover:text-foreground focus-visible:ring-[3px] focus-visible:ring-ring/45 focus-visible:outline-none"
          aria-label={closeLabel}
        >
          <X aria-hidden="true" className="size-4" />
        </DialogPrimitive.Close>
      </DialogPrimitive.Content>
    </DialogPrimitive.Portal>
  );
}

function DialogHeader({ className, ...props }: ComponentProps<'div'>) {
  return (
    <div
      data-slot="dialog-header"
      className={cn('flex flex-col gap-1.5 pr-8', className)}
      {...props}
    />
  );
}

function DialogFooter({ className, ...props }: ComponentProps<'div'>) {
  return (
    <div
      data-slot="dialog-footer"
      className={cn(
        '-mx-6 -mb-6 flex flex-col-reverse gap-2 rounded-b-xl border-t bg-subtle px-6 py-4 sm:flex-row sm:justify-end',
        className,
      )}
      {...props}
    />
  );
}

function DialogTitle({ className, ...props }: ComponentProps<typeof DialogPrimitive.Title>) {
  return (
    <DialogPrimitive.Title
      data-slot="dialog-title"
      className={cn('font-display text-xl leading-tight font-semibold', className)}
      {...props}
    />
  );
}

function DialogDescription({
  className,
  ...props
}: ComponentProps<typeof DialogPrimitive.Description>) {
  return (
    <DialogPrimitive.Description
      data-slot="dialog-description"
      className={cn('text-sm text-muted-foreground', className)}
      {...props}
    />
  );
}

export {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogOverlay,
  DialogTitle,
  DialogTrigger,
};
