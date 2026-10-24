import {
  rowSortingFeature,
  tableFeatures,
  useTable,
  type ColumnDef,
  type RowData,
  type SortingState,
} from '@tanstack/react-table';
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, ChevronsUpDown } from 'lucide-react';
import type { ReactNode } from 'react';
import { cn } from '../lib/utils';
import { Button } from './button';
import { Skeleton } from './skeleton';

/** Features every DataTable registers. Sorting is server-side, so there is no sorted row model. */
export const dataTableFeatures = tableFeatures({ rowSortingFeature });

export type DataTableFeatures = typeof dataTableFeatures;

/** A column of a {@link DataTable}. Build them with `createColumnHelper<DataTableFeatures, TData>()`. */
// eslint-disable-next-line @typescript-eslint/no-explicit-any -- the columns of one table have different value types
export type DataTableColumn<TData extends RowData> = ColumnDef<DataTableFeatures, TData, any>;

export type DataTableLabels = {
  /** The table's accessible name (a visually hidden caption). */
  caption: string;
  loading: string;
  empty: string;
  error: string;
  previousPage: string;
  nextPage: string;
  /** "21–40 of 132", formatted by the caller for the current locale. */
  range: (from: number, to: number, total: number) => string;
  /** Accessible name of a sort toggle, e.g. "Sort by name". */
  sortBy: (column: string) => string;
};

/**
 * Classes for a column's header and cells, by column id. Use {@link hideBelow} to drop secondary columns on tablets
 * and `text-right tabular` for numbers.
 */
export type DataTableColumnClasses = Partial<Record<string, string>>;

/** Hides a secondary column under a breakpoint (tablet portrait is below `lg`). */
export const hideBelow = {
  md: 'hidden md:table-cell',
  lg: 'hidden lg:table-cell',
  xl: 'hidden xl:table-cell',
} as const;

export type DataTableProps<TData extends RowData> = {
  columns: DataTableColumn<TData>[];
  data: readonly TData[];
  getRowId: (row: TData) => string;
  status: 'pending' | 'error' | 'success';
  /** 1-based, as the API pages. Omit the paging props for a short, unpaged list: the pager is not shown. */
  page?: number;
  pageSize?: number;
  totalCount?: number;
  onPageChange?: (page: number) => void;
  /** Omit `onSortingChange` for a table that cannot be sorted. */
  sorting?: SortingState;
  onSortingChange?: (sorting: SortingState) => void;
  labels: DataTableLabels;
  /** Rendered above the table (search box, filters). */
  toolbar?: ReactNode;
  /** Replaces the plain "no results" line, e.g. an `EmptyState` with the create action. */
  emptyState?: ReactNode;
  columnClassNames?: DataTableColumnClasses;
  className?: string;
};

const NO_SORTING: SortingState = [];
const SKELETON_ROWS = 5;

/**
 * A server-paged, server-sorted table. It renders what the API returned and reports page and sort changes; the
 * caller owns the query (usually through `useListParams`) and every string.
 */
export function DataTable<TData extends RowData>({
  columns,
  data,
  getRowId,
  status,
  page = 1,
  pageSize = Math.max(1, data.length),
  totalCount = data.length,
  onPageChange,
  sorting = NO_SORTING,
  onSortingChange,
  labels,
  toolbar,
  emptyState,
  columnClassNames = {},
  className,
}: DataTableProps<TData>) {
  const table = useTable({
    features: dataTableFeatures,
    columns,
    data: data as TData[],
    getRowId: (row) => getRowId(row),
    manualSorting: true,
    enableSortingRemoval: false,
    enableSorting: onSortingChange !== undefined,
    state: { sorting },
    onSortingChange: (updater) =>
      onSortingChange?.(typeof updater === 'function' ? updater(sorting) : updater),
  });

  const lastPage = Math.max(1, Math.ceil(totalCount / pageSize));
  const from = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, totalCount);
  const leafColumns = table.getAllLeafColumns();
  const isEmpty = status === 'success' && data.length === 0;

  return (
    <div data-slot="data-table" className={cn('flex flex-col gap-3', className)}>
      {toolbar}
      {isEmpty && emptyState ? (
        emptyState
      ) : (
        <div className="overflow-x-auto rounded-xl border bg-card shadow-card">
          <table className="w-full text-sm" aria-busy={status === 'pending'}>
            <caption className="sr-only">{labels.caption}</caption>
            <thead>
              {table.getHeaderGroups().map((group) => (
                <tr key={group.id} className="border-b bg-subtle">
                  {group.headers.map((header) => {
                    const sorted = header.column.getIsSorted();
                    const heading = header.column.columnDef.header;
                    const SortIcon =
                      sorted === 'asc' ? ArrowUp : sorted === 'desc' ? ArrowDown : ChevronsUpDown;
                    return (
                      <th
                        key={header.id}
                        scope="col"
                        className={cn(
                          'h-11 px-4 text-left align-middle text-[0.8125rem] font-medium whitespace-nowrap text-muted-foreground',
                          columnClassNames[header.column.id],
                        )}
                        aria-sort={
                          sorted === 'asc'
                            ? 'ascending'
                            : sorted === 'desc'
                              ? 'descending'
                              : undefined
                        }
                      >
                        {header.isPlaceholder ? null : header.column.getCanSort() ? (
                          <button
                            type="button"
                            className={cn(
                              'group -mx-1.5 inline-flex h-8 items-center gap-1 rounded-md px-1.5 transition-colors outline-none hover:bg-accent hover:text-foreground focus-visible:ring-[3px] focus-visible:ring-ring/45',
                              sorted && 'text-foreground',
                            )}
                            onClick={header.column.getToggleSortingHandler()}
                            aria-label={labels.sortBy(
                              typeof heading === 'string' ? heading : header.id,
                            )}
                          >
                            <table.FlexRender header={header} />
                            <SortIcon
                              aria-hidden="true"
                              className={cn(
                                'size-3.5',
                                !sorted && 'opacity-40 group-hover:opacity-80',
                              )}
                            />
                          </button>
                        ) : (
                          <table.FlexRender header={header} />
                        )}
                      </th>
                    );
                  })}
                </tr>
              ))}
            </thead>
            <tbody>
              {status === 'pending' ? (
                Array.from({ length: SKELETON_ROWS }, (_, row) => (
                  <tr key={row} className="border-b last:border-0">
                    {leafColumns.map((column, index) => (
                      <td key={column.id} className={cn('h-13 px-4', columnClassNames[column.id])}>
                        {row === 0 && index === 0 && (
                          <span className="sr-only">{labels.loading}</span>
                        )}
                        <Skeleton className={cn('h-3.5', index === 0 ? 'w-3/4' : 'w-1/2')} />
                      </td>
                    ))}
                  </tr>
                ))
              ) : status === 'error' || isEmpty ? (
                <tr>
                  <td
                    colSpan={leafColumns.length}
                    className={cn(
                      'h-28 px-4 text-center text-muted-foreground',
                      status === 'error' && 'text-danger',
                    )}
                    role={status === 'error' ? 'alert' : undefined}
                  >
                    {status === 'error' ? labels.error : labels.empty}
                  </td>
                </tr>
              ) : (
                table.getRowModel().rows.map((row) => (
                  <tr
                    key={row.id}
                    className="border-b transition-colors last:border-0 hover:bg-accent/50"
                  >
                    {row.getAllCells().map((cell) => (
                      <td
                        key={cell.id}
                        className={cn(
                          'h-13 px-4 py-2 align-middle',
                          columnClassNames[cell.column.id],
                        )}
                      >
                        <table.FlexRender cell={cell} />
                      </td>
                    ))}
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}
      {onPageChange && (
        <div className="flex flex-wrap items-center justify-between gap-2 text-sm text-muted-foreground">
          <span aria-live="polite" className="tabular">
            {status === 'success' && totalCount > 0 ? labels.range(from, to, totalCount) : null}
          </span>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              disabled={page <= 1 || status !== 'success'}
              onClick={() => onPageChange(page - 1)}
            >
              <ChevronLeft aria-hidden="true" />
              {labels.previousPage}
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={page >= lastPage || status !== 'success'}
              onClick={() => onPageChange(page + 1)}
            >
              {labels.nextPage}
              <ChevronRight aria-hidden="true" />
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
