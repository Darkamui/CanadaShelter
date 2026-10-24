import { DataTable, type DataTableFeatures } from '@shelter/ui/components/data-table';
import { createColumnHelper } from '@tanstack/react-table';
import { act, fireEvent, render, renderHook, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { I18nextProvider } from 'react-i18next';
import { MemoryRouter, useLocation } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import i18n from '../i18n';
import { useDataTableLabels } from './useDataTableLabels';
import { sortingToSort, sortToSorting, useListParams } from './useListParams';

type Row = { id: string; name: string };
const helper = createColumnHelper<DataTableFeatures, Row>();
const columns = helper.columns([helper.accessor('name', { header: 'Nom' })]);

type TableProps = {
  rows: Row[];
  page?: number;
  total?: number;
  status?: 'pending' | 'error' | 'success';
  onPageChange?: (page: number) => void;
  onSortingChange?: () => void;
};

function Table({
  rows,
  page = 1,
  total,
  status = 'success',
  onPageChange,
  onSortingChange,
}: TableProps) {
  const labels = useDataTableLabels('Animaux');
  return (
    <DataTable
      columns={columns}
      data={rows}
      getRowId={(row) => row.id}
      status={status}
      page={page}
      pageSize={2}
      totalCount={total ?? rows.length}
      onPageChange={onPageChange ?? (() => {})}
      onSortingChange={onSortingChange}
      labels={labels}
    />
  );
}

const inI18n = (ui: ReactNode) => <I18nextProvider i18n={i18n}>{ui}</I18nextProvider>;

describe('DataTable', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('fr-CA');
  });

  it('is named by its caption and reports page changes', () => {
    const onPageChange = vi.fn();
    const rows = [
      { id: '1', name: 'Rex' },
      { id: '2', name: 'Mia' },
    ];
    render(inI18n(<Table rows={rows} total={5} onPageChange={onPageChange} />));

    expect(screen.getByRole('table', { name: 'Animaux' })).toBeInTheDocument();
    expect(screen.getByText('1–2 sur 5')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Précédent' })).toBeDisabled();

    fireEvent.click(screen.getByRole('button', { name: 'Suivant' }));
    expect(onPageChange).toHaveBeenCalledWith(2);
  });

  it('disables next on the last page', () => {
    render(inI18n(<Table rows={[{ id: '5', name: 'Zoé' }]} page={3} total={5} />));
    expect(screen.getByRole('button', { name: 'Suivant' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Précédent' })).toBeEnabled();
  });

  it('shows empty, loading and error states', () => {
    const { rerender } = render(inI18n(<Table rows={[]} />));
    expect(screen.getByText('Aucun résultat.')).toBeInTheDocument();

    rerender(inI18n(<Table rows={[]} status="pending" />));
    expect(screen.getByText('Chargement…')).toBeInTheDocument();

    rerender(inI18n(<Table rows={[]} status="error" />));
    expect(screen.getByRole('alert')).toHaveTextContent('Impossible de charger la liste.');
  });

  it('offers a named sort toggle only when sorting is handled', () => {
    const rows = [{ id: '1', name: 'Rex' }];
    const onSortingChange = vi.fn();
    const { rerender } = render(inI18n(<Table rows={rows} />));
    expect(screen.queryByRole('button', { name: 'Trier par Nom' })).not.toBeInTheDocument();

    rerender(inI18n(<Table rows={rows} onSortingChange={onSortingChange} />));
    fireEvent.click(screen.getByRole('button', { name: 'Trier par Nom' }));
    expect(onSortingChange).toHaveBeenCalledWith([{ id: 'name', desc: false }]);
  });

  it('switches strings with the UI locale', async () => {
    render(inI18n(<Table rows={[]} />));
    await act(() => i18n.changeLanguage('en-CA'));
    expect(screen.getByText('No results.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Next' })).toBeInTheDocument();
  });
});

describe('useListParams', () => {
  function setup(initial: string) {
    function Search() {
      return <output data-testid="search">{useLocation().search}</output>;
    }
    const wrapper = ({ children }: { children: ReactNode }) => (
      <MemoryRouter initialEntries={[initial]}>
        {children}
        <Search />
      </MemoryRouter>
    );
    const hook = renderHook(() => useListParams(), { wrapper });
    const location = {
      get search() {
        return screen.getByTestId('search').textContent;
      },
    };
    return { hook, location };
  }

  it('reads and clamps the URL', () => {
    const { hook } = setup('/animals?page=0&pageSize=500&q=rex&sort=-name');
    expect(hook.result.current).toMatchObject({ page: 1, pageSize: 100, q: 'rex', sort: '-name' });
    expect(hook.result.current.sorting).toEqual([{ id: 'name', desc: true }]);
  });

  it('returns to page 1 when the search or a filter changes, and keeps other params', () => {
    const { hook, location } = setup('/animals?page=3&status=in_care');

    act(() => hook.result.current.setQuery('mia'));
    expect(location.search).toBe('?status=in_care&q=mia');

    act(() => hook.result.current.setPage(2));
    expect(location.search).toBe('?status=in_care&q=mia&page=2');

    act(() => hook.result.current.setFilter('status', ''));
    expect(location.search).toBe('?q=mia');
  });

  it('round-trips sort keys', () => {
    expect(sortingToSort(sortToSorting('-number'))).toBe('-number');
    expect(sortToSorting('')).toEqual([]);
  });
});
