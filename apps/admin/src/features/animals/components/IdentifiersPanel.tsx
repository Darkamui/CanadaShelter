import {
  getGetAnimalQueryKey,
  getListAnimalsQueryKey,
  useAddAnimalIdentifier,
  useDeactivateAnimalIdentifier,
} from '@shelter/api-client/hooks/animals';
import type { AnimalIdentifierItem } from '@shelter/api-client/model';
import { Button } from '@shelter/ui/components/button';
import { Label } from '@shelter/ui/components/label';
import { useQueryClient } from '@tanstack/react-query';
import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { FormField } from '../../../lib/forms/FormField';
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
    <section aria-labelledby="identifiers-title" className="flex max-w-2xl flex-col gap-3">
      <h2 id="identifiers-title" className="text-lg font-semibold">
        {t('identifiers.title')}
      </h2>
      {failed && <FormAlert tone="error">{t('form.unexpectedError')}</FormAlert>}
      {identifiers.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t('identifiers.none')}</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {identifiers.map((identifier) => (
            <li key={identifier.id} className="flex flex-wrap items-center gap-x-4 gap-y-1">
              <span className="text-sm text-muted-foreground">
                {t(`identifierTypes.${identifier.type}`, { defaultValue: identifier.type })}
              </span>
              <span className={identifier.isActive ? 'font-mono' : 'font-mono line-through'}>
                {identifier.value}
              </span>
              {!identifier.isActive && identifier.deactivatedAt && (
                <span className="text-sm text-muted-foreground">
                  {t('identifiers.deactivatedOn', {
                    date: formatDate(identifier.deactivatedAt, i18n.language),
                  })}
                </span>
              )}
              {canWrite && identifier.isActive && (
                <Button
                  variant="outline"
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
      {canWrite && <AddIdentifierForm animalId={animalId} onAdded={refresh} />}
    </section>
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
  const typeId = useId();

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
    <form className="flex flex-col gap-2" onSubmit={submit} noValidate>
      {failed && <FormAlert tone="error">{t('form.unexpectedError')}</FormAlert>}
      <div className="flex flex-wrap items-start gap-4">
        <div className="flex flex-col gap-2">
          <Label htmlFor={typeId}>{t('identifiers.type')}</Label>
          <select
            id={typeId}
            className="h-9 rounded-md border bg-transparent px-3 text-sm"
            {...form.register('type')}
          >
            {IDENTIFIER_TYPES.map((code) => (
              <option key={code} value={code}>
                {t(`identifierTypes.${code}`)}
              </option>
            ))}
          </select>
        </div>
        <FormField
          label={t('identifiers.value')}
          autoComplete="off"
          error={errors.value}
          {...form.register('value')}
        />
        <Button type="submit" variant="outline" className="mt-7" disabled={isSubmitting}>
          {t('identifiers.add')}
        </Button>
      </div>
    </form>
  );
}
