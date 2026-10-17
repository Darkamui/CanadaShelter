# 0022. Admin design system

- **Status:** Accepted
- **Date:** 2026-10-03

## Context

The first screens (animals, people, locations, platform, sign-in) were each laid out by hand. Spacing, headings, empty states, errors and confirmations differed from screen to screen, and every new module would copy whichever screen it started from. Staff use the app on desktops and on tablets at the kennel, in French first (CLAUDE.md rule 5), and public routes target WCAG 2.2 AA. We want one look, "warm and calm", and one set of page shapes that future modules adopt without redesigning them.

## Decision

- **Tokens in `packages/ui/src/styles/globals.css`.**
  - Colors are OKLCH semantic tokens (`--background`, `--primary`, `--sidebar`, the status tones and others), mapped into Tailwind v4 through `@theme`. Components use the tokens, never raw colors.
  - The palette is warm neutral surfaces with a spruce-green primary and a dark spruce sidebar.
  - A dark set is defined under `.dark`, but V1 ships light only.
- **Type, self-hosted with Fontsource.** Inter Variable is the text face and Bricolage Grotesque Variable the display face (titles, record names). There are no third-party font requests.
- **Primitives in `packages/ui`.** These are shadcn/Radix-based Button, Badge, Alert, Card, Dialog, Tooltip, DataTable, FilterBar, EmptyState, Avatar, Meter, DescriptionList, Skeleton and the form controls. Icons come from `lucide-react`.
- **Page templates. Every screen is one of these:**
  - **List.** `PageHeader` (title, one-line description, primary action), then optional `FilterBar`, then `DataTable` with an `EmptyState`.
  - **Record.** `RecordHeader` (avatar, name, status, facts, actions), then `RecordTabs` (URL-addressed tabs), then the read view in Cards or a `DescriptionList`.
  - **Edit.**
    - Create and edit are separate pages (`/new`, `/:id/edit`) using `FormSection`/`FormActions`. There is no inline editing on the record.
    - Small forms (one entity, a few fields) open in a `Dialog` from the list or record instead.
  - **Auth card.** `AuthLayout` with `AuthHeader`, the form, and `AuthFooter` for the way back or out.
- **Feedback.**
  - A completed action confirms with a `sonner` toast.
  - Errors the user must fix stay on the page as a `FormAlert`, or on the field.
  - Loading uses skeletons shaped like the content.
  - Empty lists say what to do next.
- **Row actions.** Repeated per-row actions are `IconAction` (an icon button with a tooltip, and the full action and name as its accessible label). A row never carries text buttons.
- **Responsive.** The targets are desktop and tablet (820 px and up).
  - The sidebar collapses to a sheet.
  - `DataTable` hides secondary columns below a breakpoint (`hideBelow.md/lg/xl`); it does not switch to a card view.
  - Phone layouts are usable but not designed.
- **Packages added (UI only):** `sonner`, `lucide-react`, `@fontsource-variable/inter` and `@fontsource-variable/bricolage-grotesque`.

The full reference, with examples, is `docs/design-system.md`.

## Alternatives considered

- **Stay on plain shadcn defaults (neutral grey, system font).** This is the quickest option, but the app would look like every other shadcn app and nothing would stop screens drifting.
- **A full component library (MUI, Mantine, Chakra).** These bring their own styling runtime and theme model, and fight Tailwind and the shadcn primitives already in place (ADR 0019 forms). The bundle is heavier for the PWA.
- **Inline editing on the record view.** Fewer page changes, but every field needs edit, error and conflict states, it is harder to make accessible, and it clashes with optimistic-concurrency messages. A separate edit page is easier to keep consistent.
- **Card view for tables on small screens.** Better on phones, but phones are not a target, and it doubles the layout each list must maintain.
- **Google Fonts CDN.** Simpler, but it is a third-party request (a privacy concern under Law 25) and fails offline in the PWA.

## Consequences

- Positive:
  - New modules assemble pages from templates, so a slice spends no time on layout decisions.
  - Screens look and behave alike, and accessibility (labels, focus, live regions) is solved once, in the primitives.
  - Dark mode later is a token switch, not a rewrite.
- Negative / accepted trade-offs:
  - Two variable fonts add weight to the precached shell.
  - Hidden columns mean tablet users see less in lists and must open the record.
  - The `RecordHeader` scrolls with the page; it is not sticky.
  - Templates constrain unusual screens (boards, calendars); those need a deliberate exception noted in the module doc.
- Follow-ups:
  - Ship dark mode once it has been reviewed for contrast.
  - Global search in the top bar.
  - Animal photos in `Avatar`.
