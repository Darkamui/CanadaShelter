import { useListPeople } from '@shelter/api-client/hooks/people';
import type { PersonListItem } from '@shelter/api-client/model';
import { Avatar, initialsOf } from '@shelter/ui/components/avatar';
import { Badge } from '@shelter/ui/components/badge';
import { Button } from '@shelter/ui/components/button';
import { DataTable, hideBelow, type DataTableFeatures } from '@shelter/ui/components/data-table';
import { EmptyState } from '@shelter/ui/components/empty-state';
import { FilterBar } from '@shelter/ui/components/filter-bar';
import { PageHeader } from '@shelter/ui/components/page-header';
import { keepPreviousData } from '@tanstack/react-query';
import { createColumnHelper } from '@tanstack/react-table';
import { Plus, Search, Users } from 'lucide-react';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { Permissions } from '../../../lib/auth/permissions';
import { hasPermission, useSession } from '../../../lib/auth/session';
import { CheckboxField, FormField, SelectField } from '../../../lib/forms/FormField';
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
            <div className="flex items-center gap-3">
              <Avatar size="sm">{initialsOf(info.getValue())}</Avatar>
              <Link
                to={info.row.original.id}
                className="font-medium underline-offset-4 hover:underline"
              >
                {info.getValue()}
              </Link>
              {info.row.original.isArchived && <Badge>{t('detail.archivedBadge')}</Badge>}
            </div>
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
  const filtered = Boolean(list.q || role || archived);
  const newPerson = canWrite && (
    <Button asChild>
      <Link to="new">
        <Plus aria-hidden="true" />
        {t('list.new')}
      </Link>
    </Button>
  );

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title={t('title')} description={t('list.description')} actions={newPerson} />
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
        columnClassNames={{
          email: hideBelow.md,
          phone: hideBelow.lg,
          city: hideBelow.xl,
        }}
        emptyState={
          !filtered && (
            <EmptyState
              icon={<Users />}
              title={t('list.empty')}
              description={t('list.emptyBody')}
              action={newPerson}
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
              label={t('list.roleFilter')}
              value={role}
              onChange={(event) => list.setFilter('role', event.target.value)}
            >
              <option value="">{t('list.allRoles')}</option>
              {PERSON_ROLES.map((code) => (
                <option key={code} value={code}>
                  {t(`roles.${code}`)}
                </option>
              ))}
            </SelectField>
            <CheckboxField
              label={t('list.showArchived')}
              checked={archived}
              onChange={(event) => list.setFilter('archived', event.target.checked ? 'true' : '')}
            />
          </FilterBar>
        }
      />
    </div>
  );
}
