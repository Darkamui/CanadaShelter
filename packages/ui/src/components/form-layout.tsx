import type { ComponentProps, ReactNode } from 'react';
import { cn } from '../lib/utils';

/**
 * One group of a long form ("Identification", "Alerts"). On wide screens the title and its explanation sit in a
 * column beside the fields, so a form reads as a short list of topics.
 */
function FormSection({
  title,
  description,
  className,
  children,
  ...props
}: Omit<ComponentProps<'section'>, 'title'> & { title: ReactNode; description?: ReactNode }) {
  return (
    <section
      data-slot="form-section"
      className={cn(
        'grid gap-x-10 gap-y-4 border-b py-7 first:pt-0 last:border-0 lg:grid-cols-[minmax(0,16rem)_minmax(0,1fr)]',
        className,
      )}
      {...props}
    >
      <div className="flex flex-col gap-1">
        <h2 className="text-[0.9375rem] font-semibold">{title}</h2>
        {description && <p className="text-sm text-muted-foreground">{description}</p>}
      </div>
      <div className="grid max-w-2xl gap-4 sm:grid-cols-2 [&>[data-wide]]:sm:col-span-2">
        {children}
      </div>
    </section>
  );
}

/** The buttons of a page form. `sticky` keeps them in reach at the bottom of a long form. */
function FormActions({
  sticky = false,
  className,
  ...props
}: ComponentProps<'div'> & { sticky?: boolean }) {
  return (
    <div
      data-slot="form-actions"
      className={cn(
        'flex flex-wrap items-center justify-end gap-2',
        sticky &&
          'sticky bottom-0 z-10 -mx-5 border-t bg-card/95 px-5 py-3 backdrop-blur supports-[backdrop-filter]:bg-card/80 sm:-mx-6 sm:px-6',
        className,
      )}
      {...props}
    />
  );
}

export { FormActions, FormSection };
