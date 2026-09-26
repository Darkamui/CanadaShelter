---
name: adr
description: Record an architecture decision as a numbered ADR in docs/adr. Use when a significant or non-obvious technical choice is made or changed.
---

# /adr

1. Find the next number in `docs/adr/` (`NNNN-kebab-title.md`).
2. Write it from `docs/adr/0000-template.md`. Keep it under one page.
3. **Alternatives** must list real options considered and why they lost.
4. **Consequences** must include the downsides accepted.
5. If it supersedes an ADR, set the old one's status to `Superseded by NNNN` and link both ways.
6. Add a line to the index in `docs/adr/README.md`.
7. If the decision changes a rule in a `CLAUDE.md` or a section of `docs/architecture.md`, update that too and say which.
