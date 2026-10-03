import { useListAnimals } from '@shelter/api-client/hooks/animals';
import type { AnimalListItem } from '@shelter/api-client/model';
import { Button } from '@shelter/ui/components/button';
import { DataTable, type DataTableFeatures } from '@shelter/ui/components/data-table';
import { Input } from '@shelter/ui/components/input';
import { Label } from '@shelter/ui/components/label';
import { keepPreviousData } from '@tanstack/react-query';
import { createColumnHelper } from '@tanstack/react-table';
import { useId, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { Permissions } from '../../../lib/auth/permissions';
import { hasPermission, useSession } from '../../../lib/auth/session';
import { useDataTableLabels } from '../../../lib/list/useDataTableLabels';
import { useListParams } from '../../../lib/list/useListParams';
import { CUSTODY_STATUSES } from '../model';
import { useSpecies } from '../useSpecies';

const helper = createColumnHelper<DataTableFeatures, AnimalListItem>();
const selectClass = 'h-9 rounded-md border bg-transparent px-3 text-sm';

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
          cell: (info) => `#${info.getValue()}`,
        }),
        helper.accessor('name', {
          header: t('fields.name'),
          cell: (info) => (
            <Link
              to={info.row.original.id}
              className="font-medium underline-offset-4 hover:underline"
            >
              {info.getValue() ?? t('list.unnamed')}
            </Link>
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
          cell: (info) =>
            t(`custodyStatuses.${info.getValue()}`, { defaultValue: info.getValue() }),
        }),
        helper.accessor('currentLocationName', {
          header: t('fields.location'),
          enableSorting: false,
        }),
        helper.accessor('hasAlerts', {
          header: t('fields.alerts'),
          enableSorting: false,
          cell: (info) => (info.getValue() ? t('list.hasAlerts') : ''),
        }),
      ]),
    [t, labelOf],
  );

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-2xl font-semibold">{t('title')}</h1>
        {canWrite && (
          <Button asChild>
            <Link to="new">{t('list.new')}</Link>
          </Button>
        )}
      </div>
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
        toolbar={
          <Filters
            q={list.q}
            status={status}
            speciesCode={speciesCode}
            speciesOptions={species.options}
            onSearch={list.setQuery}
            onFilter={list.setFilter}
          />
        }
      />
    </section>
  );
}

function Filters({
  q,
  status,
  speciesCode,
  speciesOptions,
  onSearch,
  onFilter,
}: {
  q: string;
  status: string;
  speciesCode: string;
  speciesOptions: { code: string; label: string }[];
  onSearch: (q: string) => void;
  onFilter: (key: string, value: string) => void;
}) {
  const { t } = useTranslation('animals');
  const searchId = useId();
  const statusId = useId();
  const speciesId = useId();

  return (
    <div className="flex flex-wrap items-end gap-4">
      <form
        role="search"
        className="flex items-end gap-2"
        onSubmit={(event) => {
          event.preventDefault();
          onSearch(String(new FormData(event.currentTarget).get('q') ?? '').trim());
        }}
      >
        <div className="flex flex-col gap-2">
          <Label htmlFor={searchId}>{t('list.searchLabel')}</Label>
          <Input id={searchId} name="q" type="search" defaultValue={q} key={q} className="w-72" />
        </div>
        <Button type="submit" variant="outline">
          {t('shell:list.search')}
        </Button>
      </form>
      <div className="flex flex-col gap-2">
        <Label htmlFor={statusId}>{t('fields.custodyStatus')}</Label>
        <select
          id={statusId}
          className={selectClass}
          value={status}
          onChange={(event) => onFilter('status', event.target.value)}
        >
          <option value="">{t('list.allStatuses')}</option>
          {CUSTODY_STATUSES.map((code) => (
            <option key={code} value={code}>
              {t(`custodyStatuses.${code}`)}
            </option>
          ))}
        </select>
      </div>
      <div className="flex flex-col gap-2">
        <Label htmlFor={speciesId}>{t('fields.species')}</Label>
        <select
          id={speciesId}
          className={selectClass}
          value={speciesCode}
          onChange={(event) => onFilter('species', event.target.value)}
        >
          <option value="">{t('list.allSpecies')}</option>
          {speciesOptions.map((s) => (
            <option key={s.code} value={s.code}>
              {s.label}
            </option>
          ))}
        </select>
      </div>
    </div>
  );
}
