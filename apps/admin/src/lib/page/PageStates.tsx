import { Alert } from '@shelter/ui/components/alert';
import { Skeleton } from '@shelter/ui/components/skeleton';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

/** A record page while its record loads: the header's shape, so the page does not jump when it arrives. */
export function RecordSkeleton() {
  const { t } = useTranslation('shell');
  return (
    <div role="status" className="flex flex-col gap-6">
      <span className="sr-only">{t('list.loading')}</span>
      <div
        aria-hidden="true"
        className="flex flex-col gap-4 rounded-xl border bg-card p-6 shadow-card"
      >
        <div className="flex items-center gap-5">
          <Skeleton className="size-16 rounded-2xl" />
          <div className="flex flex-1 flex-col gap-3">
            <Skeleton className="h-8 w-56" />
            <Skeleton className="h-5 w-32" />
          </div>
        </div>
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
          {[0, 1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-10" />
          ))}
        </div>
      </div>
      <Skeleton aria-hidden="true" className="h-64 rounded-xl" />
    </div>
  );
}

/** A page whose record could not be loaded (not found, no access, server error). */
export function PageError({ above, children }: { above?: ReactNode; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-4">
      {above}
      <Alert tone="danger">{children}</Alert>
    </div>
  );
}
