import { useListAnimals } from '@shelter/api-client/hooks/animals';
import { useListLocationKinds, useListLocations } from '@shelter/api-client/hooks/operations';
import type { AnimalListItem, LocationItem } from '@shelter/api-client/model';
import { Avatar } from '@shelter/ui/components/avatar';
import { Badge } from '@shelter/ui/components/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@shelter/ui/components/card';
import { DataTable, hideBelow, type DataTableFeatures } from '@shelter/ui/components/data-table';
import { Meter } from '@shelter/ui/components/meter';
import { RecordHeader, type RecordFact } from '@shelter/ui/components/record-header';
import { keepPreviousData } from '@tanstack/react-query';
import { createColumnHelper } from '@tanstack/react-table';
import { ChevronRight } from 'lucide-react';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';
import { useLocalize } from '../../../lib/i18n/localized';
import { useDataTableLabels } from '../../../lib/list/useDataTableLabels';
import { useListParams } from '../../../lib/list/useListParams';
import { PageError, RecordSkeleton } from '../../../lib/page/PageStates';
import { SpeciesIcon } from '../../animals/components/AnimalBadges';
import { useSpecies } from '../../animals/useSpecies';
import { LocationKindIcon } from '../components/LocationKindIcon';
import { locationPath } from '../paths';
import { usePopulation } from '../usePopulation';

const helper = createColumnHelper<DataTableFeatures, AnimalListItem>();

/** The location's ancestors, top first. Stops at a missing parent or a cycle. */
function ancestorsOf(locations: readonly LocationItem[], locationId: string): LocationItem[] {
  const byId = new Map(locations.map((l) => [l.id, l]));
  const chain: LocationItem[] = [];
  const seen = new Set<string>([locationId]);
  let parent = byId.get(byId.get(locationId)?.parentId ?? '');
  while (parent && !seen.has(parent.id)) {
    seen.add(parent.id);
    chain.unshift(parent);
    parent = byId.get(parent.parentId ?? '');
  }
  return chain;
}

/**
 * One location: where it sits in the tree, the active locations directly inside it and, with `animal.read`, how
 * many animals are in care there and which ones, the locations below included.
 */
export function LocationDetailPage() {
  const { t } = useTranslation('locations');
  const { locationId = '' } = useParams();
  const locations = useListLocations({ includeArchived: true });

  if (locations.isPending) return <RecordSkeleton />;
  if (locations.isError) return <PageError>{t('loadFailed')}</PageError>;
  const location = locations.data.find((l) => l.id === locationId);
  if (!location) return <PageError>{t('detail.notFound')}</PageError>;

  return <LocationRecord location={location} locations={locations.data} />;
}

function LocationRecord({
  location,
  locations,
}: {
  location: LocationItem;
  locations: readonly LocationItem[];
}) {
  const { t } = useTranslation('locations');
  const localize = useLocalize();
  const kinds = useListLocationKinds();
  const population = usePopulation();
  const ancestors = ancestorsOf(locations, location.id);
  const children = locations.filter((l) => l.parentId === location.id && !l.isArchived);
  const kind = kinds.data?.find((k) => k.code === location.kindCode);
  const counts = population.of(location.id);
  const none = <span className="font-normal text-muted-foreground">—</span>;

  const facts: RecordFact[] = [
    { label: t('fields.kind'), value: kind ? localize(kind.label) : location.kindCode },
  ];
  if (population.ready) {
    facts.push(
      { label: t('detail.here'), value: <span className="tabular">{counts.count}</span> },
      { label: t('detail.subtree'), value: <span className="tabular">{counts.subtreeCount}</span> },
    );
  }
  facts.push({
    label: t('detail.capacity'),
    value:
      location.capacity === null ? (
        none
      ) : population.ready ? (
        <span className="flex flex-col gap-1.5 pt-0.5">
          <span className="tabular">
            {t('detail.occupancyValue', {
              count: counts.subtreeCount,
              capacity: location.capacity,
            })}
          </span>
          <Meter
            value={counts.subtreeCount}
            max={location.capacity}
            aria-label={t('detail.occupancy', { name: location.name })}
            className="max-w-40"
          />
        </span>
      ) : (
        <span className="tabular">{location.capacity}</span>
      ),
  });

  return (
    <div className="flex flex-col gap-6">
      <nav aria-label={t('detail.breadcrumb')} className="text-sm">
        <ol className="flex flex-wrap items-center gap-1.5 text-muted-foreground">
          {[{ id: '', name: t('title') }, ...ancestors].map((a) => (
            <li key={a.id} className="flex items-center gap-1.5">
              <Link
                to={a.id ? locationPath(a.id) : '/operations'}
                className="rounded-sm underline-offset-4 hover:text-foreground hover:underline"
              >
                {a.name}
              </Link>
              <ChevronRight aria-hidden="true" className="size-3.5" />
            </li>
          ))}
          <li aria-current="page" className="font-medium text-foreground">
            {location.name}
          </li>
        </ol>
      </nav>
      <RecordHeader
        tone={location.isArchived ? 'neutral' : 'primary'}
        avatar={
          <Avatar size="lg">
            <LocationKindIcon code={location.kindCode} />
          </Avatar>
        }
        title={location.name}
        status={location.isArchived && <Badge>{t('archivedBadge')}</Badge>}
        facts={facts}
      />
      {children.length > 0 && (
        <Card aria-labelledby="children-title">
          <CardHeader>
            <CardTitle id="children-title">{t('detail.children')}</CardTitle>
          </CardHeader>
          <CardContent>
            <ul className="-my-2 divide-y">
              {children.map((child) => (
                <li key={child.id} className="flex items-center gap-3 py-2.5 text-sm">
                  <span
                    aria-hidden="true"
                    className="flex size-7 shrink-0 items-center justify-center rounded-md bg-secondary text-secondary-foreground [&_svg]:size-3.5"
                  >
                    <LocationKindIcon code={child.kindCode} />
                  </span>
                  <Link
                    to={locationPath(child.id)}
                    className="min-w-0 flex-1 font-medium underline-offset-4 hover:underline"
                  >
                    {child.name}
                  </Link>
                  {population.ready && (
                    <span className="tabular text-muted-foreground">
                      {t('animalCount', { count: population.of(child.id).subtreeCount })}
                    </span>
                  )}
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}
      {population.enabled && <AnimalsHere locationId={location.id} />}
    </div>
  );
}

/** Animals in care at the location or below it, paged in the URL. */
function AnimalsHere({ locationId }: { locationId: string }) {
  const { t } = useTranslation('locations');
  const { t: ta } = useTranslation('animals');
  const list = useListParams();
  const { labelOf } = useSpecies();
  const animals = useListAnimals(
    {
      locationId,
      status: 'in_care',
      page: list.page,
      pageSize: list.pageSize,
      sort: list.sort || undefined,
    },
    { query: { placeholderData: keepPreviousData } },
  );
  const labels = useDataTableLabels(t('detail.animalsCaption'));

  const columns = useMemo(
    () =>
      helper.columns([
        helper.accessor('number', {
          header: ta('fields.number'),
          cell: (info) => <span className="text-muted-foreground">#{info.getValue()}</span>,
        }),
        helper.accessor('name', {
          header: ta('fields.name'),
          cell: (info) => (
            <span className="flex items-center gap-3">
              <Avatar size="sm">
                <SpeciesIcon code={info.row.original.speciesCode} />
              </Avatar>
              <Link
                to={`/animals/${info.row.original.id}`}
                className="font-medium underline-offset-4 hover:underline"
              >
                {info.getValue() ?? t('animals:list.unnamed')}
              </Link>
            </span>
          ),
        }),
        helper.accessor('speciesCode', {
          header: ta('fields.species'),
          enableSorting: false,
          cell: (info) => labelOf(info.getValue()),
        }),
        helper.accessor('currentLocationName', {
          header: ta('fields.location'),
          enableSorting: false,
        }),
      ]),
    [t, ta, labelOf],
  );

  return (
    <section aria-labelledby="animals-here-title" className="flex flex-col gap-3">
      <h2 id="animals-here-title" className="font-display text-lg font-semibold">
        {t('detail.animals')}
      </h2>
      <DataTable
        columns={columns}
        data={animals.data?.items ?? []}
        getRowId={(row) => row.id}
        status={animals.status}
        page={list.page}
        pageSize={list.pageSize}
        totalCount={animals.data?.totalCount ?? 0}
        onPageChange={list.setPage}
        sorting={list.sorting}
        onSortingChange={list.setSorting}
        labels={labels}
        columnClassNames={{ number: 'w-20 tabular', currentLocationName: hideBelow.md }}
      />
    </section>
  );
}
