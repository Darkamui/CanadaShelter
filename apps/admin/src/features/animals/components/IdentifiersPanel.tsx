import {
  getGetAnimalQueryKey,
  getListAnimalsQueryKey,
  useAddAnimalIdentifier,
  useDeactivateAnimalIdentifier,
} from '@shelter/api-client/hooks/animals';
import type { AnimalIdentifierItem } from '@shelter/api-client/model';
import { Button } from '@shelter/ui/components/button';
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@shelter/ui/components/card';
import { EmptyState } from '@shelter/ui/components/empty-state';
import { cn } from '@shelter/ui/lib/utils';
import { useQueryClient } from '@tanstack/react-query';
import { Fingerprint, Plus } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { FormField, SelectField } from '../../../lib/forms/FormField';
import { requiredText } from '../../../lib/forms/schemas';
import { applyValidationErrors } from '../../../lib/forms/serverErrors';
import { useZodForm } from '../../../lib/forms/useZodForm';
import { formatDate } from '../../../lib/format';
import { IDENTIFIER_MAX_LENGTH, IDENTIFIER_TYPES } from '../model';

const identifierSchema = z.object({
  type: z.enum(IDENTIFIER_TYPES),
  value: requiredText().max(IDENTIFIER_MAX_LENGTH, { error: 'animals:form.tooLong' }),
});

// The server answers a duplicate active microchip on `value`.
const serverFields = {
  type: ['type', 'animals:form.invalid'],
  value: ['value', 'animals:identifiers.valueRejected'],
} as const;

/** Microchips, licences and external numbers. Deactivated ones stay listed; only an active microchip is unique. */
export function IdentifiersPanel({
  animalId,
  identifiers,
  canWrite,
}: {
  animalId: string;
  identifiers: AnimalIdentifierItem[];
  canWrite: boolean;
}) {
  const { t, i18n } = useTranslation('animals');
  const queryClient = useQueryClient();
  const deactivate = useDeactivateAnimalIdentifier();
  const [failed, setFailed] = useState(false);

  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: getGetAnimalQueryKey(animalId) }),
      queryClient.invalidateQueries({ queryKey: getListAnimalsQueryKey() }),
    ]);

  return (
    <Card aria-labelledby="identifiers-title">
      <CardHeader>
        <CardTitle id="identifiers-title">{t('identifiers.title')}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {failed && <FormAlert tone="error">{t('form.unexpectedError')}</FormAlert>}
        {identifiers.length === 0 ? (
          <EmptyState inCard icon={<Fingerprint />} title={t('identifiers.none')} />
        ) : (
          <ul className="flex flex-col divide-y">
            {identifiers.map((identifier) => (
              <li
                key={identifier.id}
                className="flex flex-wrap items-center gap-x-4 gap-y-1 py-3 first:pt-0 last:pb-0"
              >
                <span className="w-32 text-sm text-muted-foreground">
                  {t(`identifierTypes.${identifier.type}`, { defaultValue: identifier.type })}
                </span>
                <span
                  className={cn(
                    'tabular min-w-0 flex-1 font-medium break-all',
                    !identifier.isActive && 'font-normal text-muted-foreground line-through',
                  )}
                >
                  {identifier.value}
                </span>
                {!identifier.isActive && identifier.deactivatedAt && (
                  <span className="text-[0.8125rem] text-muted-foreground">
                    {t('identifiers.deactivatedOn', {
                      date: formatDate(identifier.deactivatedAt, i18n.language),
                    })}
                  </span>
                )}
                {canWrite && identifier.isActive && (
                  <Button
                    variant="ghost"
                    size="sm"
                    disabled={deactivate.isPending}
                    aria-label={t('identifiers.deactivateLabel', { value: identifier.value })}
                    onClick={() =>
                      deactivate.mutate(
                        { animalId, identifierId: identifier.id },
                        {
                          onSuccess: async () => {
                            setFailed(false);
                            await refresh();
                          },
                          onError: () => setFailed(true),
                        },
                      )
                    }
                  >
                    {t('identifiers.deactivate')}
                  </Button>
                )}
              </li>
            ))}
          </ul>
        )}
      </CardContent>
      {canWrite && (
        <CardFooter className="py-4">
          <AddIdentifierForm animalId={animalId} onAdded={refresh} />
        </CardFooter>
      )}
    </Card>
  );
}

function AddIdentifierForm({
  animalId,
  onAdded,
}: {
  animalId: string;
  onAdded: () => Promise<unknown>;
}) {
  const { t } = useTranslation('animals');
  const add = useAddAnimalIdentifier();
  const form = useZodForm(identifierSchema, { defaultValues: { type: 'microchip', value: '' } });
  const { errors, isSubmitting } = form.formState;
  const [failed, setFailed] = useState(false);

  const submit = form.handleSubmit(async (data) => {
    setFailed(false);
    try {
      await add.mutateAsync({ animalId, data });
      form.reset({ type: data.type, value: '' });
      await onAdded();
    } catch (error) {
      if (!applyValidationErrors(error, form.setError, serverFields)) setFailed(true);
    }
  });

  return (
    <form className="flex w-full flex-col gap-3" onSubmit={submit} noValidate>
      {failed && <FormAlert tone="error">{t('form.unexpectedError')}</FormAlert>}
      <div className="flex flex-wrap items-start gap-3 [&_[data-slot=field]]:min-w-40 [&_[data-slot=field]]:flex-1">
        <SelectField label={t('identifiers.type')} {...form.register('type')}>
          {IDENTIFIER_TYPES.map((code) => (
            <option key={code} value={code}>
              {t(`identifierTypes.${code}`)}
            </option>
          ))}
        </SelectField>
        <FormField
          label={t('identifiers.value')}
          autoComplete="off"
          error={errors.value}
          {...form.register('value')}
        />
        <Button type="submit" variant="outline" className="mt-[1.375rem]" disabled={isSubmitting}>
          <Plus aria-hidden="true" />
          {t('identifiers.add')}
        </Button>
      </div>
    </form>
  );
}
