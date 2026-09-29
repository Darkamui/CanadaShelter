import { useListPeople } from '@shelter/api-client/hooks/people';
import type { PersonListItem } from '@shelter/api-client/model';
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
import { RoleTags } from '../components/RoleTags';
import { PERSON_ROLES } from '../model';

const helper = createColumnHelper<DataTableFeatures, PersonListItem>();

/** The organization's people: search by name, email or phone; filter by role; archived people on request. */
export function PeopleListPage() {
  const { t } = useTranslation('people');
  const list = useListParams();
  const { data: session } = useSession();
  const canWrite = hasPermission(session, Permissions.personWrite);
  const role = list.filter('role');
  const archived = list.filter('archived') === 'true';

  const people = useListPeople(
    {
      page: list.page,
      pageSize: list.pageSize,
      q: list.q || undefined,
      role: role || undefined,
      archived: archived || undefined,
      sort: list.sort || undefined,
    },
    { query: { placeholderData: keepPreviousData } },
  );
  const labels = useDataTableLabels(t('list.caption'));

  const columns = useMemo(
    () =>
      helper.columns([
        helper.accessor('displayName', {
          id: 'name',
          header: t('fields.name'),
          cell: (info) => (
            <Link
              to={info.row.original.id}
              className="font-medium underline-offset-4 hover:underline"
            >
              {info.getValue()}
            </Link>
          ),
        }),
        helper.accessor('email', { header: t('fields.email'), enableSorting: false }),
        helper.accessor('phone', { header: t('fields.phone'), enableSorting: false }),
        helper.accessor('city', { header: t('fields.city'), enableSorting: false }),
        helper.accessor('roleTags', {
          header: t('fields.roleTags'),
          enableSorting: false,
          cell: (info) => <RoleTags roles={info.getValue()} />,
        }),
      ]),
    [t],
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
        data={people.data?.items ?? []}
        getRowId={(row) => row.id}
        status={people.status}
        page={list.page}
        pageSize={list.pageSize}
        totalCount={people.data?.totalCount ?? 0}
        onPageChange={list.setPage}
        sorting={list.sorting}
        onSortingChange={list.setSorting}
        labels={labels}
        toolbar={
          <Filters
            q={list.q}
            role={role}
            archived={archived}
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
  role,
  archived,
  onSearch,
  onFilter,
}: {
  q: string;
  role: string;
  archived: boolean;
  onSearch: (q: string) => void;
  onFilter: (key: string, value: string) => void;
}) {
  const { t } = useTranslation('people');
  const searchId = useId();
  const roleId = useId();

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
        <Label htmlFor={roleId}>{t('list.roleFilter')}</Label>
        <select
          id={roleId}
          className="h-9 rounded-md border bg-transparent px-3 text-sm"
          value={role}
          onChange={(event) => onFilter('role', event.target.value)}
        >
          <option value="">{t('list.allRoles')}</option>
          {PERSON_ROLES.map((code) => (
            <option key={code} value={code}>
              {t(`roles.${code}`)}
            </option>
          ))}
        </select>
      </div>
      <label className="flex h-9 items-center gap-2 text-sm">
        <input
          type="checkbox"
          className="size-4"
          checked={archived}
          onChange={(event) => onFilter('archived', event.target.checked ? 'true' : '')}
        />
        {t('list.showArchived')}
      </label>
    </div>
  );
}
