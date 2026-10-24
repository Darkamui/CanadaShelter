import { useListPeople } from '@shelter/api-client/hooks/people';
import { Input } from '@shelter/ui/components/input';
import { Label } from '@shelter/ui/components/label';
import { NativeSelect } from '@shelter/ui/components/native-select';
import { Search } from 'lucide-react';
import { keepPreviousData } from '@tanstack/react-query';
import { useEffect, useId, useState } from 'react';
import type { FieldError } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { FieldMessage } from '../../../lib/forms/FormField';

const RESULTS = 10;
const DEBOUNCE_MS = 250;

/**
 * Finds an active person by name, email or phone, then picks one from the matches. The chosen person stays in the
 * list while the search changes. `value` is a person ID, '' for none.
 */
export function PersonPicker({
  label,
  value,
  onChange,
  error,
}: {
  label: string;
  value: string;
  onChange: (personId: string) => void;
  error?: Pick<FieldError, 'message'>;
}) {
  const { t } = useTranslation('movements');
  const searchId = useId();
  const selectId = useId();
  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');
  const [chosen, setChosen] = useState<{ id: string; name: string } | undefined>();

  useEffect(() => {
    const timer = setTimeout(() => setQuery(search.trim()), DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [search]);

  const people = useListPeople(
    { q: query || undefined, archived: false, pageSize: RESULTS, sort: 'name' },
    { query: { enabled: query !== '', placeholderData: keepPreviousData } },
  );
  const matches = (query === '' ? [] : (people.data?.items ?? [])).map((p) => ({
    id: p.id,
    name: [p.displayName, p.email ?? p.phone].filter(Boolean).join(' · '),
  }));
  const options =
    chosen && chosen.id === value && !matches.some((m) => m.id === chosen.id)
      ? [chosen, ...matches]
      : matches;
  const message = error?.message ? t(error.message) : undefined;

  return (
    <fieldset className="flex min-w-0 flex-col gap-2 rounded-lg border bg-subtle p-3">
      <legend className="float-left mb-1 text-sm font-medium">{label}</legend>
      <div className="clear-both flex flex-col gap-1.5">
        <Label htmlFor={searchId} className="text-[0.8125rem] font-normal text-muted-foreground">
          {t('person.search')}
        </Label>
        <div className="relative">
          <Search
            aria-hidden="true"
            className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
          />
          <Input
            id={searchId}
            type="search"
            autoComplete="off"
            className="pl-9"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
      </div>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor={selectId} className="text-[0.8125rem] font-normal text-muted-foreground">
          {t('person.choose')}
        </Label>
        <NativeSelect
          id={selectId}
          aria-invalid={message ? true : undefined}
          value={value}
          onChange={(e) => {
            const id = e.target.value;
            const option = options.find((o) => o.id === id);
            setChosen(option);
            onChange(id);
          }}
        >
          <option value="">
            {query !== '' && people.isSuccess && matches.length === 0
              ? t('person.noMatch')
              : t('person.none')}
          </option>
          {options.map((o) => (
            <option key={o.id} value={o.id}>
              {o.name}
            </option>
          ))}
        </NativeSelect>
      </div>
      {message && <FieldMessage>{message}</FieldMessage>}
    </fieldset>
  );
}
