import type {
  AnimalResponse,
  CreateAnimalRequest,
  UpdateAnimalRequest,
} from '@shelter/api-client/model';
import { z } from 'zod';
import { requiredText } from '../../lib/forms/schemas';

/** Sex codes (`AnimalSexes` on the server). Label key: `animals:sexes.<code>`. */
export const SEXES = ['unknown', 'male', 'female'] as const;

/** Reproductive status codes (`ReproductiveStatuses`). Label key: `animals:reproductiveStatuses.<code>`. */
export const REPRODUCTIVE_STATUSES = ['unknown', 'intact', 'sterilized'] as const;

/** Custody status codes (`CustodyStatuses`). Label key: `animals:custodyStatuses.<code>`. */
export const CUSTODY_STATUSES = ['not_in_care', 'in_care', 'outcome'] as const;

/** Identifier type codes (`AnimalIdentifierTypes`). Label key: `animals:identifierTypes.<code>`. */
export const IDENTIFIER_TYPES = ['microchip', 'licence', 'external'] as const;

/** Server limits (`Animal`, `AnimalIdentifier`). */
export const NAME_MAX_LENGTH = 100;
export const TEXT_MAX_LENGTH = 100;
export const NOTE_MAX_LENGTH = 2000;
export const IDENTIFIER_MAX_LENGTH = 50;
const EARLIEST_BIRTH_DATE = '1950-01-01';

const tooLong = 'animals:form.tooLong';
const optionalText = (max: number) => z.string().trim().max(max, { error: tooLong });

/** Today in the browser's time zone, as `YYYY-MM-DD`. */
export function today(): string {
  const now = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

/** The animal form. Blank strings stand for "not given"; the `to…Request` functions turn them into `null`. */
export const animalSchema = z.object({
  name: optionalText(NAME_MAX_LENGTH),
  speciesCode: requiredText('animals:form.speciesRequired'),
  breed: optionalText(TEXT_MAX_LENGTH),
  secondaryBreed: optionalText(TEXT_MAX_LENGTH),
  colour: optionalText(TEXT_MAX_LENGTH),
  sex: z.enum(SEXES),
  reproductiveStatus: z.enum(REPRODUCTIVE_STATUSES),
  birthDate: z.string().refine((v) => v === '' || (v >= EARLIEST_BIRTH_DATE && v <= today()), {
    error: 'animals:form.birthDateRange',
  }),
  birthDateEstimated: z.boolean(),
  marks: optionalText(NOTE_MAX_LENGTH),
  behaviourAlert: optionalText(NOTE_MAX_LENGTH),
  medicalAlert: optionalText(NOTE_MAX_LENGTH),
  legalAlert: optionalText(NOTE_MAX_LENGTH),
  // Create only: later identifiers go through the identifiers panel.
  microchip: optionalText(IDENTIFIER_MAX_LENGTH),
});

export type AnimalFormValues = z.input<typeof animalSchema>;
export type AnimalFormOutput = z.output<typeof animalSchema>;

const blankToNull = (value: string) => (value === '' ? null : value);

function details(values: AnimalFormOutput) {
  return {
    name: blankToNull(values.name),
    speciesCode: values.speciesCode,
    breed: blankToNull(values.breed),
    secondaryBreed: blankToNull(values.secondaryBreed),
    colour: blankToNull(values.colour),
    sex: values.sex,
    reproductiveStatus: values.reproductiveStatus,
    birthDate: blankToNull(values.birthDate),
    birthDateEstimated: values.birthDate !== '' && values.birthDateEstimated,
    marks: blankToNull(values.marks),
    behaviourAlert: blankToNull(values.behaviourAlert),
    medicalAlert: blankToNull(values.medicalAlert),
    legalAlert: blankToNull(values.legalAlert),
  };
}

export function toCreateRequest(values: AnimalFormOutput): CreateAnimalRequest {
  return { ...details(values), microchip: blankToNull(values.microchip) };
}

export function toUpdateRequest(values: AnimalFormOutput, version: number): UpdateAnimalRequest {
  return { ...details(values), version };
}

export function emptyAnimal(): AnimalFormValues {
  return {
    name: '',
    speciesCode: '',
    breed: '',
    secondaryBreed: '',
    colour: '',
    sex: 'unknown',
    reproductiveStatus: 'unknown',
    birthDate: '',
    birthDateEstimated: false,
    marks: '',
    behaviourAlert: '',
    medicalAlert: '',
    legalAlert: '',
    microchip: '',
  };
}

export function fromAnimal(animal: AnimalResponse): AnimalFormValues {
  return {
    name: animal.name ?? '',
    speciesCode: animal.speciesCode,
    breed: animal.breed ?? '',
    secondaryBreed: animal.secondaryBreed ?? '',
    colour: animal.colour ?? '',
    sex: SEXES.find((s) => s === animal.sex) ?? 'unknown',
    reproductiveStatus:
      REPRODUCTIVE_STATUSES.find((s) => s === animal.reproductiveStatus) ?? 'unknown',
    birthDate: animal.birthDate ?? '',
    birthDateEstimated: animal.birthDateEstimated,
    marks: animal.marks ?? '',
    behaviourAlert: animal.behaviourAlert ?? '',
    medicalAlert: animal.medicalAlert ?? '',
    legalAlert: animal.legalAlert ?? '',
    microchip: '',
  };
}

/** `#12 Éclair`, or `#12` for an animal without a name. */
export function animalTitle(animal: { number: number; name: string | null }): string {
  return animal.name ? `#${animal.number} ${animal.name}` : `#${animal.number}`;
}
