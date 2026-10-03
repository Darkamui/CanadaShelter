import {
  rowSortingFeature,
  tableFeatures,
  useTable,
  type ColumnDef,
  type RowData,
  type SortingState,
} from '@tanstack/react-table';
import type { ReactNode } from 'react';
import { cn } from '../lib/utils';
import { Button } from './button';

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

export type DataTableProps<TData extends RowData> = {
  columns: DataTableColumn<TData>[];
  data: readonly TData[];
  getRowId: (row: TData) => string;
  status: 'pending' | 'error' | 'success';
  /** 1-based, as the API pages. */
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  /** Omit `onSortingChange` for a table that cannot be sorted. */
  sorting?: SortingState;
  onSortingChange?: (sorting: SortingState) => void;
  labels: DataTableLabels;
  /** Rendered above the table (search box, filters). */
  toolbar?: ReactNode;
  className?: string;
};

const NO_SORTING: SortingState = [];

// Decorative only (aria-hidden); the header's aria-sort carries the state.
const SORT_GLYPH = { asc: '▲', desc: '▼' } as const;

/**
 * A server-paged, server-sorted table. It renders what the API returned and reports page and sort changes; the
 * caller owns the query (usually through `useListParams`) and every string.
 */
export function DataTable<TData extends RowData>({
  columns,
  data,
  getRowId,
  status,
  page,
  pageSize,
  totalCount,
  onPageChange,
  sorting = NO_SORTING,
  onSortingChange,
  labels,
  toolbar,
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
  const columnCount = table.getAllLeafColumns().length;

  let message: string | null = null;
  if (status === 'pending') message = labels.loading;
  else if (status === 'error') message = labels.error;
  else if (data.length === 0) message = labels.empty;

  return (
    <div data-slot="data-table" className={cn('flex flex-col gap-3', className)}>
      {toolbar}
      <div className="overflow-x-auto rounded-md border">
        <table className="w-full text-sm" aria-busy={status === 'pending'}>
          <caption className="sr-only">{labels.caption}</caption>
          <thead className="bg-muted/50">
            {table.getHeaderGroups().map((group) => (
              <tr key={group.id} className="border-b">
                {group.headers.map((header) => {
                  const sorted = header.column.getIsSorted();
                  const heading = header.column.columnDef.header;
                  return (
                    <th
                      key={header.id}
                      scope="col"
                      className="h-10 px-3 text-left align-middle font-medium whitespace-nowrap"
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
                          className="inline-flex items-center gap-1 hover:underline"
                          onClick={header.column.getToggleSortingHandler()}
                          aria-label={labels.sortBy(
                            typeof heading === 'string' ? heading : header.id,
                          )}
                        >
                          <table.FlexRender header={header} />
                          <span aria-hidden="true">{sorted ? SORT_GLYPH[sorted] : null}</span>
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
            {message !== null ? (
              <tr>
                <td
                  colSpan={columnCount}
                  className={cn(
                    'h-24 px-3 text-center text-muted-foreground',
                    status === 'error' && 'text-destructive',
                  )}
                  role={status === 'error' ? 'alert' : undefined}
                >
                  {message}
                </td>
              </tr>
            ) : (
              table.getRowModel().rows.map((row) => (
                <tr key={row.id} className="border-b last:border-0 hover:bg-muted/30">
                  {row.getAllCells().map((cell) => (
                    <td key={cell.id} className="px-3 py-2 align-middle">
                      <table.FlexRender cell={cell} />
                    </td>
                  ))}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
      <div className="flex flex-wrap items-center justify-between gap-2 text-sm text-muted-foreground">
        <span aria-live="polite">
          {status === 'success' ? labels.range(from, to, totalCount) : null}
        </span>
        <div className="flex gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={page <= 1 || status !== 'success'}
            onClick={() => onPageChange(page - 1)}
          >
            {labels.previousPage}
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={page >= lastPage || status !== 'success'}
            onClick={() => onPageChange(page + 1)}
          >
            {labels.nextPage}
          </Button>
        </div>
      </div>
    </div>
  );
}
