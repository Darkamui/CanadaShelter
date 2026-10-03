import { useListAnimals } from '@shelter/api-client/hooks/animals';
import { useListLocationKinds, useListLocations } from '@shelter/api-client/hooks/operations';
import type { AnimalListItem, LocationItem } from '@shelter/api-client/model';
import { DataTable, type DataTableFeatures } from '@shelter/ui/components/data-table';
import { keepPreviousData } from '@tanstack/react-query';
import { createColumnHelper } from '@tanstack/react-table';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { useLocalize } from '../../../lib/i18n/localized';
import { useDataTableLabels } from '../../../lib/list/useDataTableLabels';
import { useListParams } from '../../../lib/list/useListParams';
import { useSpecies } from '../../animals/useSpecies';
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
  const localize = useLocalize();
  const { locationId = '' } = useParams();
  const locations = useListLocations({ includeArchived: true });
  const kinds = useListLocationKinds();
  const population = usePopulation();

  if (locations.isPending) return <p role="status">{t('shell:list.loading')}</p>;
  if (locations.isError) return <FormAlert tone="error">{t('loadFailed')}</FormAlert>;
  const location = locations.data.find((l) => l.id === locationId);
  if (!location) return <FormAlert tone="error">{t('detail.notFound')}</FormAlert>;

  const ancestors = ancestorsOf(locations.data, location.id);
  const children = locations.data.filter((l) => l.parentId === location.id && !l.isArchived);
  const kind = kinds.data?.find((k) => k.code === location.kindCode);
  const counts = population.of(location.id);

  return (
    <section className="flex flex-col gap-6">
      <nav aria-label={t('detail.breadcrumb')} className="text-sm">
        <ol className="flex flex-wrap gap-1 text-muted-foreground">
          <li>
            <Link to="/operations" className="underline-offset-4 hover:underline">
              {t('title')}
            </Link>
          </li>
          {ancestors.map((a) => (
            <li key={a.id}>
              <span aria-hidden="true">› </span>
              <Link to={locationPath(a.id)} className="underline-offset-4 hover:underline">
                {a.name}
              </Link>
            </li>
          ))}
        </ol>
      </nav>
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold">{location.name}</h1>
        <p className="text-sm text-muted-foreground">
          {[
            kind ? localize(kind.label) : location.kindCode,
            location.capacity !== null && t('capacity', { count: location.capacity }),
            location.isArchived && t('archivedTag'),
          ]
            .filter(Boolean)
            .join(' · ')}
        </p>
      </div>
      {population.ready && (
        <dl className="grid max-w-md grid-cols-[auto_1fr] gap-x-4 text-sm">
          <dt className="text-muted-foreground">{t('detail.here')}</dt>
          <dd>{counts.count}</dd>
          <dt className="text-muted-foreground">{t('detail.subtree')}</dt>
          <dd>{counts.subtreeCount}</dd>
        </dl>
      )}
      {children.length > 0 && (
        <section aria-labelledby="children-title" className="flex flex-col gap-2">
          <h2 id="children-title" className="text-lg font-semibold">
            {t('detail.children')}
          </h2>
          <ul className="flex flex-col gap-1">
            {children.map((child) => (
              <li key={child.id} className="flex gap-2 text-sm">
                <Link
                  to={locationPath(child.id)}
                  className="font-medium underline-offset-4 hover:underline"
                >
                  {child.name}
                </Link>
                {population.ready && (
                  <span className="text-muted-foreground">
                    {t('animalCount', { count: population.of(child.id).subtreeCount })}
                  </span>
                )}
              </li>
            ))}
          </ul>
        </section>
      )}
      {population.enabled && <AnimalsHere locationId={location.id} />}
    </section>
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
          cell: (info) => `#${info.getValue()}`,
        }),
        helper.accessor('name', {
          header: ta('fields.name'),
          cell: (info) => (
            <Link
              to={`/animals/${info.row.original.id}`}
              className="font-medium underline-offset-4 hover:underline"
            >
              {info.getValue() ?? t('animals:list.unnamed')}
            </Link>
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
    <section aria-labelledby="animals-here-title" className="flex flex-col gap-2">
      <h2 id="animals-here-title" className="text-lg font-semibold">
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
      />
    </section>
  );
}
