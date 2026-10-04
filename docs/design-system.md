# Admin design system

How every screen in `apps/admin` is built. Decision record: [ADR 0022](adr/0022-admin-design-system.md). New screens use these templates and primitives. If a screen needs something they don't cover, add the primitive to `packages/ui` (or `apps/admin/src/lib/page`) and document it here. Don't hand-roll it in the feature.

## Principles

- **Warm and calm.** Shelter work is stressful, so the UI isn't. Use quiet surfaces, one strong color (spruce), and save the status colors for status.
- **The record is the unit.** An animal, a person or a location has one page. Its header answers "who is this and what state is it in", and its tabs hold the rest.
- **French first.** Layouts must hold French strings, which run about 20% longer than English. Check both locales.
- **Desktop and tablet.** Design at 1280 px and check at 820 px. Phones must work but aren't designed for.
- **Accessible by default.** Accessibility lives in the primitives: labels, focus rings, live regions, keyboard support. Don't reimplement it in features.

## Tokens

They live in `packages/ui/src/styles/globals.css` and are exposed as Tailwind utilities.

| Token                                               | Use                                                                              |
| --------------------------------------------------- | -------------------------------------------------------------------------------- |
| `background`, `card`, `subtle`                      | Page, raised surfaces, insets (code blocks, table header)                        |
| `foreground`, `muted-foreground`                    | Text, secondary text                                                             |
| `primary`, `secondary`                              | Spruce actions, soft spruce fills (avatars, step numbers)                        |
| `success`, `warning`, `danger`, `info`              | Status only (badges, alerts, meter, record edge). Never decorative                |
| `border`, `border-strong`, `ring`                   | Hairlines, emphasised borders, focus rings (`focus-visible:ring-[3px] ring-ring/45`) |
| `sidebar-*`                                         | The dark spruce navigation and the auth band                                     |
| `shadow-card`, `shadow-raised`                      | Cards and tables; dialogs and the auth card                                      |

- **Dark mode.** The `.dark` set exists but doesn't ship. Never use raw colors (`bg-white`, `text-gray-500`), so dark mode stays a switch.
- **Type.**
  - `font-sans` (Inter Variable) is for everything.
  - `font-display` (Bricolage Grotesque) is for page and record titles and section `h2`s only.
  - Numbers in tables and facts use `tabular`.
- **Headings.**
  - Page `h1`: `PageHeader`, `RecordHeader` or `AuthHeader` (never hand-styled).
  - Section `h2` on a page: `font-display text-lg font-semibold`.
  - Card title: `CardTitle`.
- **Radius.** Cards and tables use `rounded-xl`, dialogs and the auth card `rounded-2xl`, and controls the default.

## Page templates

### List

`PageHeader` (title, one-line `description`, primary action), then an optional `FilterBar`, then a `DataTable`.

- Server-paged lists pass `page`/`pageSize`/`totalCount`/`onPageChange` with `useListParams`, which keeps the URL as the state. Short lists (staff, invitations) omit paging.
- Hide secondary columns with `columnClassNames={{ col: hideBelow.md }}` (`md`/`lg`/`xl`). The first column and the status stay visible.
- `emptyState`: an `EmptyState` with an icon, a title, what to do next, and the create action when allowed. When the list is empty, the header's create action is hidden (the empty state carries it).
- The table caption comes from `useDataTableLabels(t('…caption'))`.
- Examples: `features/animals/routes/AnimalsListPage.tsx`, `features/people/routes/PeopleListPage.tsx`.

### Record

`RecordHeader`, then `RecordTabs`, then the tab content.

- `RecordHeader`:
  - `tone` colors the left edge with the record's status.
  - `avatar` is an `Avatar size="lg"` with initials or an icon.
  - `title` is the `h1`.
  - `status` and `chips` are badges; `actions` are Edit and the record's commands.
  - `facts` lists the 3 or 4 things staff check first, as `{label, value}`.
- Tabs are routes (`/animals/:id/history`), so they can be linked and the back button works.
- Tab content goes in `Card`s or a `DescriptionList`. Read only.
- Loading uses `RecordSkeleton`; a failure uses `PageError` with a `BackLink`.
- Examples: `features/animals/routes/AnimalRecordLayout.tsx`, `features/people/routes/PersonDetailPage.tsx`, `features/locations/routes/LocationDetailPage.tsx`.

### Edit

- **Create and edit pages** (`/new`, `/:id/edit`):
  - Start with a `BackLink` and a `PageHeader`.
  - The form is a list of `FormSection`s (title and explanation beside the fields), followed by `FormActions` (`sticky` on long forms).
  - Fields are `FormField`/`SelectField`/`TextareaField`/`CheckboxField` from `lib/forms` (ADR 0019); `wide` spans both columns.
  - Example: `features/animals/routes/AnimalEditPage.tsx`.
- **Small forms open in a `Dialog`.** "Small" means one entity and a few fields (locations, invite, roles).
  - Put the description in `DialogDescription`.
  - End with this footer:
    ```tsx
    <DialogFooter className="mt-1">
      <DialogClose asChild><Button type="button" variant="ghost">{t('common.cancel')}</Button></DialogClose>
      <Button type="submit">…</Button>
    </DialogFooter>
    ```
  - On success, close the dialog, toast, and invalidate the query.

### Auth card

`AuthLayout` renders the brand band and one centered card. Each page renders, in order:

1. `AuthHeader title description`.
2. Any `FormAlert`.
3. The form. Its submit button is `size="lg"` and full width.
4. `AuthFooter`, holding an `AuthBackLink` or a `LogoutButton`.

Inline links such as "Forgot password?" use `AuthTextLink`. Examples: `features/platform/routes/LoginPage.tsx` and `OrganizationPickerPage.tsx`.

## Feedback

| Situation                                      | Use                                                        |
| ---------------------------------------------- | ---------------------------------------------------------- |
| An action succeeded                            | `toast.success(...)` from `@shelter/ui/components/toaster` |
| A row action failed (no form to attach to)     | `toast.error(...)`                                         |
| A form failed, or a state the user must fix    | `FormAlert` (stays until fixed) or the field's error       |
| A conflict, warning or explanation on a page   | `Alert tone=…` with a `title`                              |
| Loading                                        | `Skeleton` shaped like the content; never a lone spinner   |
| Nothing to show                                | `EmptyState`: what this is and what to do                  |

- Toast text names what changed ("Rôles de Jean-François Gagnon enregistrés.") and uses the same verb as the button.
- Never toast a validation error.

## Components

- **`Badge tone dot`.** Status uses `dot`; categories (roles, kinds) don't. Use `success` for active or in care, `info` for pending, `warning` for expiring or attention, `danger` for alerts, `neutral` for inactive or archived, and `primary` for the one category that matters most (administrator).
- **`IconAction label hint`.** For repeated row actions. `label` is the full accessible name ("Suspendre Sophie Côté") and `hint` the tooltip verb. Never put text buttons in a table row.
- **`Avatar`.** Decorative (`aria-hidden`); the name is always next to it. Use `initialsOf(name)` for people and organizations, and an icon for species and location kinds.
- **`Meter`.** Capacity and other ratios, always shown with the numbers in text.
- **`Tooltip`.** Only for icon-only controls. Never put needed information only in a tooltip.
- Icons come from `lucide-react`, with `aria-hidden="true"` when next to text.

## Copy

- No hard-coded strings (lint enforced). Write fr-CA first, in Québec French (_courriel_, _famille d’accueil_). Use typographic apostrophes (’) in new strings, and add uncertain terms to `_frReview`.
- Use sentence case. Avoid all-caps labels and `A · B` joins.
- Buttons say what happens ("Inviter un membre", "Enregistrer"), and the toast echoes the verb.
- A page `description` says in one line what the page is for, not how to use it.
- Empty and error states say what to do next. Errors don't apologise.

## Checklist for a new screen

1. Pick a template: list, record, edit page, dialog, or auth card.
2. Use the existing primitives. A new pattern means a new primitive here, not a one-off.
3. Use a toast for success, `FormAlert` for errors, a skeleton for loading, and an `EmptyState` for empty.
4. Check fr-CA and en-CA at 1280 and 820 px.
5. Make sure the keyboard reaches everything, with a visible focus ring.
