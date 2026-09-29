import type { SortingState } from '@tanstack/react-table';
import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router';

export type ListParams = {
  /** 1-based. */
  page: number;
  pageSize: number;
  /** Search text as typed; the server normalizes it. */
  q: string;
  /** Server sort key: `name` ascending, `-name` descending, or empty for the API default. */
  sort: string;
};

export const DEFAULT_PAGE_SIZE = 25;
const MAX_PAGE_SIZE = 100;

function positiveInt(value: string | null, fallback: number, max = Number.MAX_SAFE_INTEGER) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed >= 1 ? Math.min(parsed, max) : fallback;
}

/** `-name` ⇄ `[{ id: 'name', desc: true }]`. */
export function sortToSorting(sort: string): SortingState {
  if (!sort) return [];
  const desc = sort.startsWith('-');
  return [{ id: desc ? sort.slice(1) : sort, desc }];
}

export function sortingToSort(sorting: SortingState): string {
  const first = sorting[0];
  if (!first) return '';
  return first.desc ? `-${first.id}` : first.id;
}

/**
 * Page, size, search and sort of a list, kept in the URL (`?page=2&q=rex&sort=-name`) so a list can be linked,
 * reloaded and navigated back to. Changing the search, sort, size or a filter returns to page 1. Filters specific
 * to one list use `filter` / `setFilter`.
 */
export function useListParams() {
  const [searchParams, setSearchParams] = useSearchParams();

  const params = useMemo<ListParams>(
    () => ({
      page: positiveInt(searchParams.get('page'), 1),
      pageSize: positiveInt(searchParams.get('pageSize'), DEFAULT_PAGE_SIZE, MAX_PAGE_SIZE),
      q: searchParams.get('q') ?? '',
      sort: searchParams.get('sort') ?? '',
    }),
    [searchParams],
  );

  const update = useCallback(
    (changes: Record<string, string | number | null>, resetPage: boolean) => {
      setSearchParams(
        (current) => {
          const next = new URLSearchParams(current);
          for (const [key, value] of Object.entries(changes)) {
            if (value === null || value === '') next.delete(key);
            else next.set(key, String(value));
          }
          if (resetPage || next.get('page') === '1') next.delete('page');
          return next;
        },
        { replace: true },
      );
    },
    [setSearchParams],
  );

  const sorting = useMemo(() => sortToSorting(params.sort), [params.sort]);
  const filter = useCallback((key: string) => searchParams.get(key) ?? '', [searchParams]);
  const setPage = useCallback((page: number) => update({ page }, false), [update]);
  const setPageSize = useCallback((pageSize: number) => update({ pageSize }, true), [update]);
  const setQuery = useCallback((q: string) => update({ q }, true), [update]);
  const setSorting = useCallback(
    (next: SortingState) => update({ sort: sortingToSort(next) }, true),
    [update],
  );
  const setFilter = useCallback(
    (key: string, value: string) => update({ [key]: value }, true),
    [update],
  );

  return { ...params, sorting, filter, setPage, setPageSize, setQuery, setSorting, setFilter };
}
