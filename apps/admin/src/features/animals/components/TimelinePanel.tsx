import { useGetAnimalTimeline } from '@shelter/api-client/hooks/animals';
import type { TimelineItem } from '@shelter/api-client/model';
import { Button } from '@shelter/ui/components/button';
import { keepPreviousData } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatDateTime } from '../../../lib/format';

const PAGE_SIZE = 20;

/**
 * The animal's timeline, newest first. Event types and parameter names are translated; location and person IDs show
 * the names the server resolved (person names only for staff who may read people).
 */
export function TimelinePanel({ animalId }: { animalId: string }) {
  const { t } = useTranslation('animals');
  const [page, setPage] = useState(1);
  const timeline = useGetAnimalTimeline(
    animalId,
    { page, pageSize: PAGE_SIZE },
    { query: { placeholderData: keepPreviousData } },
  );
  const total = timeline.data?.totalCount ?? 0;

  return (
    <section aria-labelledby="timeline-title" className="flex max-w-2xl flex-col gap-3">
      <h2 id="timeline-title" className="text-lg font-semibold">
        {t('timeline.title')}
      </h2>
      {timeline.isPending ? (
        <p role="status">{t('shell:list.loading')}</p>
      ) : timeline.isError ? (
        <p role="alert" className="text-destructive">
          {t('timeline.loadFailed')}
        </p>
      ) : (
        <>
          <ol className="flex flex-col gap-3 border-l pl-4">
            {timeline.data.items.map((item) => (
              <TimelineEntry key={item.id} item={item} />
            ))}
          </ol>
          {total > PAGE_SIZE && (
            <div className="flex gap-2">
              <Button
                variant="outline"
                size="sm"
                disabled={page <= 1}
                onClick={() => setPage(page - 1)}
              >
                {t('shell:list.previousPage')}
              </Button>
              <Button
                variant="outline"
                size="sm"
                disabled={page * PAGE_SIZE >= total}
                onClick={() => setPage(page + 1)}
              >
                {t('shell:list.nextPage')}
              </Button>
            </div>
          )}
        </>
      )}
    </section>
  );
}

function TimelineEntry({ item }: { item: TimelineItem }) {
  const { t, i18n } = useTranslation('animals');
  // Resolved names first; other parameters (codes) as sent.
  const details = Object.entries(item.parameters).map(([key, value]) => ({
    key,
    label: t(`timeline.parameters.${key}`, { defaultValue: key }),
    value: item.names[key] ?? value,
  }));

  return (
    <li className="flex flex-col gap-1">
      <p className="font-medium">{t(`timeline.types.${item.type}`, { defaultValue: item.type })}</p>
      <p className="text-sm text-muted-foreground">
        <time dateTime={item.occurredAt}>{formatDateTime(item.occurredAt, i18n.language)}</time>
      </p>
      {details.length > 0 && (
        <dl className="grid gap-x-4 text-sm sm:grid-cols-[auto_1fr]">
          {details.map((d) => (
            <div key={d.key} className="contents">
              <dt className="text-muted-foreground">{d.label}</dt>
              <dd>{d.value}</dd>
            </div>
          ))}
        </dl>
      )}
    </li>
  );
}
