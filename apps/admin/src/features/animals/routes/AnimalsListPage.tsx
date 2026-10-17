import { useListAnimals } from '@shelter/api-client/hooks/animals';
import type { AnimalListItem } from '@shelter/api-client/model';
import { Button } from '@shelter/ui/components/button';
import { Avatar } from '@shelter/ui/components/avatar';
import { DataTable, hideBelow, type DataTableFeatures } from '@shelter/ui/components/data-table';
import { EmptyState } from '@shelter/ui/components/empty-state';
import { FilterBar } from '@shelter/ui/components/filter-bar';
import { PageHeader } from '@shelter/ui/components/page-header';
import { keepPreviousData } from '@tanstack/react-query';
import { createColumnHelper } from '@tanstack/react-table';
import { PawPrint, Plus, Search, TriangleAlert } from 'lucide-react';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { Permissions } from '../../../lib/auth/permissions';
import { hasPermission, useSession } from '../../../lib/auth/session';
import { FormField, SelectField } from '../../../lib/forms/FormField';
import { useDataTableLabels } from '../../../lib/list/useDataTableLabels';
import { useListParams } from '../../../lib/list/useListParams';
import { CustodyBadge, SpeciesIcon } from '../components/AnimalBadges';
import { CUSTODY_STATUSES } from '../model';
import { useSpecies } from '../useSpecies';

const helper = createColumnHelper<DataTableFeatures, AnimalListItem>();

/** The organization's animals: search by name, number or identifier; filter by custody status and species. */
export function AnimalsListPage() {
  const { t } = useTranslation('animals');
  const list = useListParams();
  const { data: session } = useSession();
  const canWrite = hasPermission(session, Permissions.animalWrite);
  const species = useSpecies();
  const status = list.filter('status');
  const speciesCode = list.filter('species');

  const animals = useListAnimals(
    {
      page: list.page,
      pageSize: list.pageSize,
      q: list.q || undefined,
      status: status || undefined,
      species: speciesCode || undefined,
      sort: list.sort || undefined,
    },
    { query: { placeholderData: keepPreviousData } },
  );
  const labels = useDataTableLabels(t('list.caption'));
  const { labelOf } = species;

  const columns = useMemo(
    () =>
      helper.columns([
        helper.accessor('number', {
          header: t('fields.number'),
          cell: (info) => <span className="text-muted-foreground">#{info.getValue()}</span>,
        }),
        helper.accessor('name', {
          header: t('fields.name'),
          cell: (info) => (
            <div className="flex items-center gap-3">
              <Avatar size="sm">
                <SpeciesIcon code={info.row.original.speciesCode} />
              </Avatar>
              <Link
                to={info.row.original.id}
                className="font-medium underline-offset-4 hover:underline"
              >
                {info.getValue() ?? t('list.unnamed')}
              </Link>
            </div>
          ),
        }),
        helper.accessor('speciesCode', {
          header: t('fields.species'),
          enableSorting: false,
          cell: (info) => labelOf(info.getValue()),
        }),
        helper.accessor('breed', { header: t('fields.breed'), enableSorting: false }),
        helper.accessor('custodyStatus', {
          header: t('fields.custodyStatus'),
          enableSorting: false,
          cell: (info) => <CustodyBadge status={info.getValue()} />,
        }),
        helper.accessor('currentLocationName', {
          header: t('fields.location'),
          enableSorting: false,
        }),
        helper.accessor('hasAlerts', {
          header: t('fields.alerts'),
          enableSorting: false,
          cell: (info) =>
            info.getValue() && (
              <span className="inline-flex items-center gap-1.5 font-medium text-danger">
                <TriangleAlert aria-hidden="true" className="size-4" />
                {t('list.hasAlerts')}
              </span>
            ),
        }),
      ]),
    [t, labelOf],
  );
  const filtered = Boolean(list.q || status || speciesCode);

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t('title')}
        description={t('list.description')}
        actions={
          canWrite && (
            <Button asChild>
              <Link to="new">
                <Plus aria-hidden="true" />
                {t('list.new')}
              </Link>
            </Button>
          )
        }
      />
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
        columnClassNames={{
          number: 'w-20 tabular',
          speciesCode: hideBelow.lg,
          breed: hideBelow.xl,
          currentLocationName: hideBelow.md,
        }}
        emptyState={
          !filtered && (
            <EmptyState
              icon={<PawPrint />}
              title={t('list.empty')}
              description={t('list.emptyBody')}
              action={
                canWrite && (
                  <Button asChild>
                    <Link to="new">
                      <Plus aria-hidden="true" />
                      {t('list.new')}
                    </Link>
                  </Button>
                )
              }
            />
          )
        }
        toolbar={
          <FilterBar
            onSubmit={(event) => {
              event.preventDefault();
              list.setQuery(String(new FormData(event.currentTarget).get('q') ?? '').trim());
            }}
          >
            <FormField
              label={t('list.searchLabel')}
              name="q"
              type="search"
              defaultValue={list.q}
              key={list.q}
            />
            <Button type="submit" variant="outline">
              <Search aria-hidden="true" />
              {t('shell:list.search')}
            </Button>
            <SelectField
              label={t('fields.custodyStatus')}
              value={status}
              onChange={(event) => list.setFilter('status', event.target.value)}
            >
              <option value="">{t('list.allStatuses')}</option>
              {CUSTODY_STATUSES.map((code) => (
                <option key={code} value={code}>
                  {t(`custodyStatuses.${code}`)}
                </option>
              ))}
            </SelectField>
            <SelectField
              label={t('fields.species')}
              value={speciesCode}
              onChange={(event) => list.setFilter('species', event.target.value)}
            >
              <option value="">{t('list.allSpecies')}</option>
              {species.options.map((s) => (
                <option key={s.code} value={s.code}>
                  {s.label}
                </option>
              ))}
            </SelectField>
          </FilterBar>
        }
      />
    </div>
  );
}
