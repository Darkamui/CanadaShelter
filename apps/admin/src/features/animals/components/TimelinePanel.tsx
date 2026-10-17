import { useGetAnimalTimeline } from '@shelter/api-client/hooks/animals';
import type { TimelineItem } from '@shelter/api-client/model';
import { Button } from '@shelter/ui/components/button';
import { Card, CardAction, CardContent, CardHeader, CardTitle } from '@shelter/ui/components/card';
import { Skeleton } from '@shelter/ui/components/skeleton';
import { keepPreviousData } from '@tanstack/react-query';
import {
  ArrowRightLeft,
  ChevronLeft,
  ChevronRight,
  Circle,
  LogIn,
  LogOut,
  PawPrint,
  Undo2,
} from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { formatDateTime } from '../../../lib/format';

const PAGE_SIZE = 20;
const RECENT = 5;

/** Labels a code parameter (`reasonCode` → its reason's label); null hides it, undefined shows the value as sent. */
export type TimelineValueFormatter = (key: string, value: string) => string | null | undefined;

const typeIcons: Record<string, typeof Circle> = {
  animal_registered: PawPrint,
  intake_recorded: LogIn,
  relocation_recorded: ArrowRightLeft,
  outcome_recorded: LogOut,
  movement_voided: Undo2,
};

/**
 * The animal's whole timeline, newest first, a page at a time. Event types and parameter names are translated;
 * location and person IDs show the names the server resolved (person names only for staff who may read people);
 * `formatValue` labels codes.
 */
export function TimelinePanel({
  animalId,
  formatValue,
}: {
  animalId: string;
  formatValue?: TimelineValueFormatter;
}) {
  const { t, i18n } = useTranslation('animals');
  const [page, setPage] = useState(1);
  const timeline = useGetAnimalTimeline(
    animalId,
    { page, pageSize: PAGE_SIZE },
    { query: { placeholderData: keepPreviousData } },
  );
  const total = timeline.data?.totalCount ?? 0;
  const number = new Intl.NumberFormat(i18n.language);

  return (
    <Card aria-labelledby="timeline-title">
      <CardHeader>
        <CardTitle id="timeline-title">{t('timeline.title')}</CardTitle>
      </CardHeader>
      <CardContent>
        <TimelineBody query={timeline} formatValue={formatValue} />
      </CardContent>
      {total > PAGE_SIZE && (
        <div className="flex items-center justify-between gap-2 border-t px-5 py-3">
          <p className="tabular text-[0.8125rem] text-muted-foreground">
            {t('shell:list.range', {
              from: number.format((page - 1) * PAGE_SIZE + 1),
              to: number.format(Math.min(page * PAGE_SIZE, total)),
              total: number.format(total),
            })}
          </p>
          <div className="flex gap-1">
            <Button
              variant="outline"
              size="sm"
              disabled={page <= 1}
              onClick={() => setPage(page - 1)}
            >
              <ChevronLeft aria-hidden="true" />
              {t('shell:list.previousPage')}
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={page * PAGE_SIZE >= total}
              onClick={() => setPage(page + 1)}
            >
              {t('shell:list.nextPage')}
              <ChevronRight aria-hidden="true" />
            </Button>
          </div>
        </div>
      )}
    </Card>
  );
}

/** The last few events on the overview, with a link to the history tab. */
export function RecentActivity({
  animalId,
  formatValue,
}: {
  animalId: string;
  formatValue?: TimelineValueFormatter;
}) {
  const { t } = useTranslation('animals');
  const timeline = useGetAnimalTimeline(animalId, { page: 1, pageSize: RECENT });

  return (
    <Card aria-labelledby="recent-title">
      <CardHeader>
        <CardTitle id="recent-title">{t('overview.recent')}</CardTitle>
        {(timeline.data?.totalCount ?? 0) > RECENT && (
          <CardAction>
            <Button asChild variant="link" size="sm" className="h-auto px-0">
              <Link to="history">{t('overview.allHistory')}</Link>
            </Button>
          </CardAction>
        )}
      </CardHeader>
      <CardContent>
        <TimelineBody query={timeline} formatValue={formatValue} />
      </CardContent>
    </Card>
  );
}

function TimelineBody({
  query,
  formatValue,
}: {
  query: { isPending: boolean; isError: boolean; data?: { items: TimelineItem[] } };
  formatValue?: TimelineValueFormatter;
}) {
  const { t } = useTranslation('animals');

  if (query.isPending) {
    return (
      <div role="status" className="flex flex-col gap-4">
        <span className="sr-only">{t('shell:list.loading')}</span>
        {[0, 1, 2].map((i) => (
          <div key={i} className="flex gap-3">
            <Skeleton className="size-8 rounded-full" />
            <div className="flex flex-1 flex-col gap-2">
              <Skeleton className="h-4 w-40" />
              <Skeleton className="h-3 w-24" />
            </div>
          </div>
        ))}
      </div>
    );
  }
  if (query.isError || !query.data) {
    return <FormAlert tone="error">{t('timeline.loadFailed')}</FormAlert>;
  }

  return (
    <ol className="flex flex-col">
      {query.data.items.map((item) => (
        <TimelineEntry key={item.id} item={item} formatValue={formatValue} />
      ))}
    </ol>
  );
}

function TimelineEntry({
  item,
  formatValue,
}: {
  item: TimelineItem;
  formatValue?: TimelineValueFormatter;
}) {
  const { t, i18n } = useTranslation('animals');
  const Icon = typeIcons[item.type] ?? Circle;
  // Resolved names first, then labelled codes; other parameters as sent.
  const details = Object.entries(item.parameters).flatMap(([key, value]) => {
    const shown = item.names[key] ?? formatValue?.(key, value);
    return shown === null
      ? []
      : [
          {
            key,
            label: t(`timeline.parameters.${key}`, { defaultValue: key }),
            value: shown ?? value,
          },
        ];
  });

  return (
    <li className="group relative flex gap-3 pb-5 last:pb-0">
      {/* The rail between this event's icon and the next one. */}
      <span
        aria-hidden="true"
        className="absolute top-9 bottom-1 left-4 w-px -translate-x-1/2 bg-border group-last:hidden"
      />
      <span
        aria-hidden="true"
        className="relative flex size-8 shrink-0 items-center justify-center rounded-full border bg-card text-muted-foreground [&_svg]:size-4"
      >
        <Icon />
      </span>
      <div className="flex min-w-0 flex-1 flex-col gap-1 pt-1">
        <div className="flex flex-wrap items-baseline gap-x-3 gap-y-0.5">
          <p className="text-sm font-medium">
            {t(`timeline.types.${item.type}`, { defaultValue: item.type })}
          </p>
          <time
            dateTime={item.occurredAt}
            className="tabular text-[0.8125rem] text-muted-foreground"
          >
            {formatDateTime(item.occurredAt, i18n.language)}
          </time>
        </div>
        {details.length > 0 && (
          <dl className="grid gap-x-4 gap-y-0.5 text-sm sm:grid-cols-[max-content_1fr]">
            {details.map((d) => (
              <div key={d.key} className="contents">
                <dt className="text-muted-foreground">{d.label}</dt>
                <dd>{d.value}</dd>
              </div>
            ))}
          </dl>
        )}
      </div>
    </li>
  );
}
