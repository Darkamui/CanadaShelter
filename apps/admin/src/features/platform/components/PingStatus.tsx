import { useGetPlatformPing } from '@shelter/api-client/hooks/platform';
import { cn } from '@shelter/ui/lib/utils';
import { useTranslation } from 'react-i18next';

/** Backend reachability indicator (GET /api/platform/ping). */
export function PingStatus() {
  const { t } = useTranslation('platform');
  const { data, isPending, isError } = useGetPlatformPing();

  const state = isPending ? 'checking' : isError || data.status !== 'ok' ? 'down' : 'up';

  return (
    <span role="status" className="flex items-center gap-2 text-sm text-muted-foreground">
      <span
        aria-hidden="true"
        className={cn(
          'size-2 rounded-full',
          state === 'up' && 'bg-emerald-500',
          state === 'down' && 'bg-destructive',
          state === 'checking' && 'bg-muted-foreground',
        )}
      />
      {t(`ping.${state}`)}
    </span>
  );
}
