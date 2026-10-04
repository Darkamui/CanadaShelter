import { useGetPlatformPing } from '@shelter/api-client/hooks/platform';
import { cn } from '@shelter/ui/lib/utils';
import { useTranslation } from 'react-i18next';

/** Backend reachability (GET /api/platform/ping): a dot, with its text shown on wide screens. */
export function PingStatus() {
  const { t } = useTranslation('platform');
  const { data, isPending, isError } = useGetPlatformPing();

  const state = isPending ? 'checking' : isError || data.status !== 'ok' ? 'down' : 'up';

  return (
    <span
      role="status"
      className={cn(
        'flex items-center gap-2 px-2 text-xs text-muted-foreground',
        state === 'down' && 'text-danger',
      )}
    >
      <span
        aria-hidden="true"
        className={cn(
          'size-2 rounded-full ring-4',
          state === 'up' && 'bg-success ring-success/15',
          state === 'down' && 'bg-danger ring-danger/15',
          state === 'checking' && 'animate-pulse bg-muted-foreground ring-transparent',
        )}
      />
      <span className={cn(state !== 'down' && 'sr-only xl:not-sr-only')}>{t(`ping.${state}`)}</span>
    </span>
  );
}
