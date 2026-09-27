---
name: bilingual-strings
description: Québec French glossary, i18n catalog conventions, and LocalizedText rules. Use when adding or changing UI strings, fr-CA/en-CA catalogs, bilingual reference data, or any French user-facing text.
user-invocable: false
---

# Bilingual strings (fr-CA first)

French (`fr-CA`) is the default locale. Write French first, then English. Sources: architecture §11, ADR 0013, `apps/admin/CLAUDE.md`.

## Where text lives

| Kind                                                 | Home                                                                          |
| ---------------------------------------------------- | ----------------------------------------------------------------------------- |
| UI strings (labels, buttons, errors, empty states)   | i18next catalogs: `src/app/i18n/<locale>.json` (namespace `shell`)            |
| Module UI strings                                    | `src/features/<module>/i18n/<locale>.json` (namespace = module, auto-loaded)  |
| Reference data (breeds, reasons…) and tenant content | Database, `LocalizedText` (`Fr`, `En`) → two columns `<name>_fr`, `<name>_en` |
| Emails, SMS, PDFs to a contact                       | Recipient's preferred language, not the staff user's UI locale (§11.3)        |

Never hard-code a user-facing string in JSX (ESLint enforces it). Never store UI strings in the database.

## Catalog rules

- `fr-CA.json` and `en-CA.json` in the same folder have **identical key sets**. `catalogs.test.ts` fails otherwise, and a hook reports gaps after each edit.
- Keys are nested objects, camelCase, grouped by screen or component: `animals.intake.form.submit`.
- Plurals use i18next suffixes (`_one`, `_other`); interpolation uses `{{name}}`.
- A French term you are not sure of: keep your best Québec French wording, and add the key path to the top-level `"_frReview": [...]` array in `fr-CA.json`. In C# or SQL, use a `TODO(fr-review)` comment. Never fall back to English in the French file.
- Typography: use `’` for apostrophes, `…` for ellipses, and a non-breaking space before `: ; ! ?` and inside `« »`.

## Québec French glossary

Use the Québec term, not the France or anglicized one.

| en-CA                | fr-CA (use)                            | Avoid                   |
| -------------------- | -------------------------------------- | ----------------------- |
| email                | courriel                               | e-mail, mail            |
| foster home / foster | famille d'accueil                      | foster                  |
| licence / tag (pet)  | médaille                               | licence (for the tag)   |
| spay / neuter        | stérilisation / stériliser             | spay                    |
| shelter              | refuge                                 |                         |
| rescue (org)         | organisme de sauvetage TODO(fr-review) | rescue                  |
| adoption             | adoption                               |                         |
| intake               | admission TODO(fr-review)              | intake                  |
| outcome              | sortie TODO(fr-review)                 |                         |
| microchip            | micropuce                              | puce (alone, ambiguous) |
| vaccine              | vaccin                                 |                         |
| veterinarian         | vétérinaire                            | véto (informal)         |
| volunteer            | bénévole                               | volontaire              |
| donation             | don                                    |                         |
| receipt (tax)        | reçu (fiscal)                          |                         |
| cell phone           | cellulaire                             | portable                |
| postal code          | code postal                            |                         |
| weekend              | fin de semaine                         | week-end                |
| sign in / sign out   | se connecter / se déconnecter          | login, logout           |
| save                 | enregistrer                            | sauver, sauvegarder     |
| settings             | paramètres                             | settings                |
| dashboard            | tableau de bord                        | dashboard               |

Rows marked `TODO(fr-review)` are not yet confirmed with a Québec shelter. When you use one of them, or any term not listed here, add the key to `_frReview`.

## Formatting

Dates, numbers and currency go through the `Intl` helpers in `apps/admin/src/lib/format`, never string concatenation. In fr-CA, amounts look like `1 234,56 $` and dates are `yyyy-MM-dd` or long form (`26 septembre 2026`).
