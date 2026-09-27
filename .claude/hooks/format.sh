#!/usr/bin/env bash
# Formats the single file Claude just edited. Silent on success; never blocks.
# Uses node (a repo prerequisite) to parse the hook payload, so jq is not required.
set -uo pipefail

file=$(node -e 'let s="";process.stdin.on("data",d=>s+=d).on("end",()=>{try{process.stdout.write(JSON.parse(s).tool_input?.file_path??"")}catch{}})' 2>/dev/null)
{ [ -z "$file" ] || [ ! -f "$file" ]; } && exit 0

case "$file" in
  *.cs)
    dotnet format "$CLAUDE_PROJECT_DIR/backend" --include "$file" --verbosity quiet >/dev/null 2>&1 || true
    ;;
  *.ts|*.tsx|*.js|*.jsx|*.json|*.css|*.md)
    (cd "$CLAUDE_PROJECT_DIR" && pnpm exec prettier --write "$file" >/dev/null 2>&1) || true
    ;;
esac
exit 0
