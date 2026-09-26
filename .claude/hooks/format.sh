#!/usr/bin/env bash
# Formats the single file Claude just edited. Silent on success; never blocks.
set -uo pipefail

file=$(jq -r '.tool_input.file_path // empty' 2>/dev/null)
[ -z "$file" ] || [ ! -f "$file" ] && exit 0

case "$file" in
  *.cs)
    dotnet format "$CLAUDE_PROJECT_DIR/backend" --include "$file" --verbosity quiet >/dev/null 2>&1 || true
    ;;
  *.ts|*.tsx|*.js|*.jsx|*.json|*.css|*.md)
    (cd "$CLAUDE_PROJECT_DIR" && pnpm exec prettier --write "$file" >/dev/null 2>&1) || true
    ;;
esac
exit 0
