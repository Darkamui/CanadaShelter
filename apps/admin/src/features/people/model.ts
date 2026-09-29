import type { PersonRequest, PersonResponse } from '@shelter/api-client/model';
import { z } from 'zod';
import { formMessages } from '../../lib/forms/schemas';

/** Role tag codes (`PersonRoles` on the server), in display order. Label key: `people:roles.<code>`. */
export const PERSON_ROLES = [
  'owner',
  'adopter',
  'foster',
  'volunteer',
  'donor',
  'finder',
  'surrenderer',
] as const;
export type PersonRole = (typeof PERSON_ROLES)[number];

export function isPersonRole(value: string): value is PersonRole {
  return (PERSON_ROLES as readonly string[]).includes(value);
}

/** Province and territory codes. Label key: `people:provinces.<code>`. */
export const PROVINCES = [
  'QC',
  'ON',
  'NB',
  'NS',
  'PE',
  'NL',
  'MB',
  'SK',
  'AB',
  'BC',
  'YT',
  'NT',
  'NU',
] as const;

const tooLong = 'people:form.tooLong';
const optionalText = (max: number) => z.string().trim().max(max, { error: tooLong });

/** The person form. Blank strings stand for "not given"; `toRequest` turns them into `null`. */
export const personSchema = z
  .object({
    firstName: optionalText(100),
    lastName: optionalText(100),
    displayName: optionalText(200),
    email: optionalText(256).refine((v) => v === '' || z.email().safeParse(v).success, {
      error: formMessages.email,
    }),
    phone: optionalText(50),
    secondaryPhone: optionalText(50),
    addressLine: optionalText(200),
    city: optionalText(100),
    province: z.union([z.enum(PROVINCES), z.literal('')]),
    postalCode: optionalText(10),
    preferredLanguage: z.enum(['fr', 'en']),
    roleTags: z.array(z.enum(PERSON_ROLES)),
    notes: optionalText(4000),
  })
  .refine((v) => v.displayName !== '' || v.firstName !== '' || v.lastName !== '', {
    path: ['displayName'],
    error: 'people:form.nameRequired',
  });

export type PersonFormValues = z.input<typeof personSchema>;
type PersonFormOutput = z.output<typeof personSchema>;

const blankToNull = (value: string) => (value === '' ? null : value);

export function toRequest(values: PersonFormOutput): PersonRequest {
  return {
    firstName: blankToNull(values.firstName),
    lastName: blankToNull(values.lastName),
    displayName: blankToNull(values.displayName),
    email: blankToNull(values.email),
    phone: blankToNull(values.phone),
    secondaryPhone: blankToNull(values.secondaryPhone),
    addressLine: blankToNull(values.addressLine),
    city: blankToNull(values.city),
    province: blankToNull(values.province),
    postalCode: blankToNull(values.postalCode),
    preferredLanguage: values.preferredLanguage,
    roleTags: values.roleTags,
    notes: blankToNull(values.notes),
  };
}

export function emptyPerson(uiLanguage: string): PersonFormValues {
  return {
    firstName: '',
    lastName: '',
    displayName: '',
    email: '',
    phone: '',
    secondaryPhone: '',
    addressLine: '',
    city: '',
    province: '',
    postalCode: '',
    preferredLanguage: uiLanguage === 'en-CA' ? 'en' : 'fr',
    roleTags: [],
    notes: '',
  };
}

export function fromPerson(person: PersonResponse): PersonFormValues {
  const province = PROVINCES.find((p) => p === person.province) ?? '';
  return {
    firstName: person.firstName ?? '',
    lastName: person.lastName ?? '',
    // Blank when it is the default, so editing a name updates it.
    displayName:
      person.displayName === [person.firstName, person.lastName].filter(Boolean).join(' ')
        ? ''
        : person.displayName,
    email: person.email ?? '',
    phone: person.phone ?? '',
    secondaryPhone: person.secondaryPhone ?? '',
    addressLine: person.addressLine ?? '',
    city: person.city ?? '',
    province,
    postalCode: person.postalCode ?? '',
    preferredLanguage: person.preferredLanguage === 'en' ? 'en' : 'fr',
    roleTags: person.roleTags.filter(isPersonRole),
    notes: person.notes ?? '',
  };
}
