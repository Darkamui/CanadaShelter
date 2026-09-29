import type { DataTableLabels } from '@shelter/ui/components/data-table';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';

/** The shared DataTable strings in the UI locale, with the list's own accessible name. */
export function useDataTableLabels(caption: string): DataTableLabels {
  const { t, i18n } = useTranslation('shell');

  return useMemo(() => {
    const number = new Intl.NumberFormat(i18n.language);
    return {
      caption,
      loading: t('list.loading'),
      empty: t('list.empty'),
      error: t('list.error'),
      previousPage: t('list.previousPage'),
      nextPage: t('list.nextPage'),
      range: (from, to, total) =>
        t('list.range', {
          from: number.format(from),
          to: number.format(to),
          total: number.format(total),
        }),
      sortBy: (column) => t('list.sortBy', { column }),
    };
  }, [caption, t, i18n.language]);
}
