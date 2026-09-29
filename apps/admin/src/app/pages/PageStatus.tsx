import type { ReactNode } from 'react';

/** Full-page centered message (loading, error). */
export function PageStatus({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-svh flex-col items-center justify-center gap-3 p-6 text-muted-foreground">
      {children}
    </div>
  );
}
