import type { ReactNode } from 'react';
import { BrandMark } from '../layout/BrandMark';

/** Full-page centered message before the shell exists (session loading, server unreachable). */
export function PageStatus({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-svh flex-col items-center justify-center gap-4 p-6 text-sm text-muted-foreground">
      <BrandMark className="size-10 animate-pulse text-primary" pawClassName="fill-background" />
      {children}
    </div>
  );
}
