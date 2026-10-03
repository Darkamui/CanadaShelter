import { useListAnimalsSpecies } from '@shelter/api-client/hooks/animals';
import { useCallback, useMemo } from 'react';
import { useLocalize } from '../../lib/i18n/localized';

/**
 * The organization's visible species, labelled in the UI locale. `labelOf` falls back to the code for a species the
 * organization has since hidden, so existing animals still show something.
 */
export function useSpecies() {
  const localize = useLocalize();
  const species = useListAnimalsSpecies();

  const options = useMemo(
    () => (species.data ?? []).map((s) => ({ code: s.code, label: localize(s.label) })),
    [species.data, localize],
  );
  const labelOf = useCallback(
    (code: string) => options.find((s) => s.code === code)?.label ?? code,
    [options],
  );

  return { options, labelOf, status: species.status };
}
