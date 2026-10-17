import { cn } from '@shelter/ui/lib/utils';
import { ChevronLeft } from 'lucide-react';
import type { ReactNode } from 'react';
import { Link, type LinkProps } from 'react-router';

/** The title block of a sign-in card (AuthLayout): the page's h1, and one line saying what to do here. */
export function AuthHeader({ title, description }: { title: ReactNode; description?: ReactNode }) {
  return (
    <div className="flex flex-col gap-2">
      <h1 className="font-display text-[1.75rem] leading-tight font-semibold tracking-[-0.015em]">
        {title}
      </h1>
      {description && (
        <p className="text-[0.9375rem] leading-relaxed text-muted-foreground">{description}</p>
      )}
    </div>
  );
}

/** The bottom of a sign-in card, under a rule: a way back, or out (sign out). */
export function AuthFooter({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <div
      className={cn(
        '-mx-6 -mb-6 flex items-center border-t px-6 py-4 sm:-mx-8 sm:-mb-8 sm:px-8',
        className,
      )}
    >
      {children}
    </div>
  );
}

/** A quiet link back to another sign-in step ("Back to sign-in"). */
export function AuthBackLink({ children, ...props }: LinkProps & { children: ReactNode }) {
  return (
    <Link
      {...props}
      className="-ml-1 inline-flex w-fit items-center gap-1 rounded-md px-1 py-0.5 text-sm font-medium text-muted-foreground transition-colors outline-none hover:text-foreground focus-visible:ring-[3px] focus-visible:ring-ring/45"
    >
      <ChevronLeft aria-hidden="true" className="size-4" />
      {children}
    </Link>
  );
}

/** An inline text link inside a sign-in card ("Forgot password?"). */
export function AuthTextLink({ className, ...props }: LinkProps) {
  return (
    <Link
      {...props}
      className={cn(
        'rounded-sm text-sm font-medium text-primary underline-offset-4 outline-none hover:underline focus-visible:ring-[3px] focus-visible:ring-ring/45',
        className,
      )}
    />
  );
}
