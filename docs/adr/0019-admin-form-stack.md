# 0019. Admin form stack: React Hook Form + Zod

- **Status:** Accepted
- **Date:** 2026-09-27

## Context

M2 shipped seven auth and staff forms using plain `useState`, because no form library had been approved. M3 (People, Animals, Locations, Movements) will add many larger forms. They need:

- one way to validate them;
- errors that are accessible and bilingual (CLAUDE.md hard rule 5);
- server validation shown on the right field.

`apps/admin/CLAUDE.md` already named React Hook Form + Zod as the convention. Zod 4 is already in the workspace: `@shelter/api-client` depends on it, and Orval generates Zod schemas from the OpenAPI contract.

## Decision

- **Libraries.** The admin app uses `react-hook-form`, `@hookform/resolvers` and `zod`, all pinned. Zod is the same version as in api-client.
- **Hook.** Every form goes through `useZodForm(schema, options)` (`apps/admin/src/lib/forms`).
  - Errors appear on submit, then update as the user types.
  - The schema alone decides the field types.
- **Messages are catalog keys, never text.** A schema's error message is an i18n key with its namespace, for example `shell:forms.required` or `platform:password.tooShort`. `FormField` translates the key at render time, so a language switch re-renders errors in the new language. Shared builders live in `lib/forms/schemas.ts`: `requiredText` (trims) and `emailAddress`. Passwords are never trimmed.
- **Server validation.** A 400 ValidationProblem goes through `applyValidationErrors(error, setError, map)`.
  - The server's messages are English only, so the map sends each request field to a form field and a catalog key.
  - Errors the map doesn't cover show as a form-level `FormAlert`.
- **The server is the authority.** Client schemas repeat only rules the UI states, such as the 12-character password minimum from ADR 0018. The client never invents stricter rules.
- **Orval Zod schemas.** Reuse them with `.pick`/`.extend` when a form's fields match the request body one-to-one. Their default messages are English, so override the messages with catalog keys. The M2 auth forms don't match their request bodies one-to-one (confirmation fields, different field names), so they use local schemas.
- **Lint.** The `i18next/no-literal-string` rule ignores React Hook Form methods whose string argument is a field name (`register`, `setError`, and so on). See `packages/config/eslint/react.js`.

## Alternatives considered

- **TanStack Form + Zod.** Validates with Zod directly and matches our TanStack Query/Table stack. It lost for three reasons:
  - It contradicts the convention already documented.
  - The shadcn/ui form integration (which `packages/ui` follows) is built on React Hook Form; the TanStack Form one is younger.
  - We would gain one fewer package and little else.
- **Keep plain state.** Zero dependencies, but every form would re-implement validation, error display and server-error mapping. That doesn't scale to M3's forms.
- **Zod's global error map for i18n.** Less code per schema, but hidden global state. It also translates at parse time, so errors would not follow a language switch. Per-schema keys are explicit and testable.

## Consequences

- Positive:
  - One pattern for every form.
  - Accessible errors (`aria-invalid`, `aria-describedby`, focus on the first invalid field).
  - Bilingual messages, and server validation shown on the field it concerns.
- Negative / accepted trade-offs:
  - Two new npm dependencies (`react-hook-form`, `@hookform/resolvers`), plus `zod` added to the admin app.
  - Message keys are plain strings, so a mistyped key shows the raw key. The catalog tests check the shared keys only.
  - The ESLint rule's callee list copies the plugin's defaults, because the option replaces them rather than merging. Re-check it when upgrading the plugin.
- Follow-ups:
  - Move `FormField` into `packages/ui` as a shadcn `Form` when a second app needs it.
  - The roles editor in `StaffMembersTable` is not a `<form>` and still uses plain state.
