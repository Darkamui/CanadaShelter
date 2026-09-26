---
name: close-task
description: Finish an issue - verify acceptance criteria, run checks, update the module doc and ADRs, and draft commit/PR text. Use at the end of every task before committing.
---

# /close-task

1. **Acceptance criteria.** Re-read the issue. For each criterion: met / not met, with evidence (test name or file). Stop and report if any is not met.
2. **Checks.** Run `dotnet build backend`, `dotnet test backend`, `pnpm lint`, `pnpm test`, `pnpm api:generate` (confirm no diff). Report pass/fail.
3. **Module doc.** Update `docs/modules/<module>.md`: new features, contracts, events, invariants, key files. Keep it short; it is a map, not documentation of every line. Create it from `docs/modules/_template.md` if missing.
4. **Decisions.** If a non-obvious choice was made (library, pattern, data shape, trade-off), propose an ADR via `/adr`.
5. **CLAUDE.md drift.** If commands, conventions, or structure changed, update the relevant `CLAUDE.md` in the same change.
6. **French review list.** List any `TODO(fr-review)` markers added.
7. **Commit/PR text.** Conventional commit title; PR body with: what changed, how it was tested, doc sections implemented, follow-ups (not done, intentionally).

Output the PR body ready to paste. Do not push.
