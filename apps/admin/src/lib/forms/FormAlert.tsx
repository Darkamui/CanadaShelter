import { cn } from '@shelter/ui/lib/utils';
import type { ReactNode } from 'react';

/** A form-level message: `error` is announced as an alert, `info` as a status. */
export function FormAlert({ tone, children }: { tone: 'error' | 'info'; children: ReactNode }) {
  return (
    <div
      role={tone === 'error' ? 'alert' : 'status'}
      className={cn(
        'rounded-md border px-3 py-2 text-sm',
        tone === 'error' ? 'border-destructive/50 text-destructive' : 'bg-muted',
      )}
    >
      {children}
    </div>
  );
}
