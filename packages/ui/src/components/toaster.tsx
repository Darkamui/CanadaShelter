import { CircleAlert, CircleCheck, Info, LoaderCircle, TriangleAlert } from 'lucide-react';
import { Toaster as Sonner, toast } from 'sonner';

/**
 * Transient confirmations ("Admission recorded"). Mount once inside the app providers; call `toast.success(text)`.
 * Errors that need the user to act stay on the page in an `Alert`.
 */
function Toaster({
  label,
}: {
  /** Accessible name of the notification region, translated. */ label: string;
}) {
  return (
    <Sonner
      position="bottom-right"
      containerAriaLabel={label}
      gap={8}
      icons={{
        success: <CircleCheck className="size-[1.125rem] text-success" />,
        error: <CircleAlert className="size-[1.125rem] text-danger" />,
        warning: <TriangleAlert className="size-[1.125rem] text-warning" />,
        info: <Info className="size-[1.125rem] text-info" />,
        loading: <LoaderCircle className="size-[1.125rem] animate-spin text-muted-foreground" />,
      }}
      toastOptions={{
        unstyled: true,
        classNames: {
          toast:
            'flex w-(--width) items-start gap-3 rounded-lg border bg-popover px-4 py-3 text-sm text-popover-foreground shadow-raised font-sans',
          icon: 'mt-px shrink-0',
          title: 'font-medium',
          description: 'text-muted-foreground',
          success: 'border-success-border',
          error: 'border-danger-border',
          warning: 'border-warning-border',
          info: 'border-info-border',
        },
      }}
    />
  );
}

export { toast, Toaster };
