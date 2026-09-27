#!/usr/bin/env node
// PreToolUse guard for Edit/Write/MultiEdit. Blocks (exit 2) edits that break CLAUDE.md hard rules:
//   6  — Québec branches in generic domain code (belongs in rule packs, architecture §10)
//   7  — a module importing another module's internals (only .Contracts, backend/CLAUDE.md)
//   10 — editing a migration that already exists on main
// Fails open: any parsing or git problem allows the edit. ArchitectureTests and CI remain the backstop.
import { execFileSync } from 'node:child_process';
import path from 'node:path';

let raw = '';
process.stdin
  .on('data', (d) => (raw += d))
  .on('end', () => {
    try {
      const problems = check(JSON.parse(raw));
      if (problems.length) {
        process.stderr.write(`Blocked by .claude/hooks/guard.mjs:\n- ${problems.join('\n- ')}\n`);
        process.exit(2);
      }
    } catch {
      // fail open
    }
    process.exit(0);
  });

function check(payload) {
  const input = payload.tool_input ?? {};
  const file = input.file_path;
  if (!file || !file.endsWith('.cs')) return [];

  const root = process.env.CLAUDE_PROJECT_DIR || payload.cwd || process.cwd();
  const rel = path.relative(root, file).split(path.sep).join('/');
  const added = [input.new_string, input.content, ...(input.edits ?? []).map((e) => e.new_string)]
    .filter(Boolean)
    .join('\n');

  return [
    checkMigration(rel, root),
    checkProvince(rel, added),
    checkModuleBoundary(rel, added),
  ].filter(Boolean);
}

// Rule 10: migrations (and their Designer/snapshot files) on main are immutable. New ones are fine.
function checkMigration(rel, root) {
  if (!/(^|\/)Migrations\//.test(rel)) return null;
  try {
    execFileSync('git', ['cat-file', '-e', `main:${rel}`], { cwd: root, stdio: 'ignore' });
  } catch {
    return null; // not on main (or no main ref): editable
  }
  return `${rel} exists on main. Never edit an applied migration (hard rule 10) — add a new migration instead.`;
}

// Rule 6: province/municipality conditionals belong in jurisdiction rule packs, not generic code.
const PROVINCE_BRANCH = [
  /province\w*\s*(==|!=|is\s+(not\s+)?)\s*"QC"/i,
  /"QC"\s*(==|!=)\s*\w*province/i,
  /province\w*\s*(==|!=|is\s+(not\s+)?)\s*\w*Province\w*\.(QC|Quebec)\b/i,
  /\.Equals\(\s*"QC"/,
];
const RULE_PACK_PATHS = /\/(Jurisdictions?|RulePacks?|Compliance)\/|\.Tests\/|\/Tests\//;

function checkProvince(rel, added) {
  if (RULE_PACK_PATHS.test(rel)) return null;
  const hit = PROVINCE_BRANCH.find((re) => re.test(added));
  return hit
    ? `Province-specific conditional in ${rel}. Québec behaviour goes in a jurisdiction rule pack (hard rule 6, architecture §10).`
    : null;
}

// Rule 7: Modules/<X>/... may use Shelter.Modules.<X>.* or Shelter.Modules.<Y>.Contracts only.
// A .Contracts project may use only .Contracts namespaces. Nothing in a module may use Shelter.Host.
function checkModuleBoundary(rel, added) {
  const m = rel.match(/^backend\/Modules\/(\w+)\/Shelter\.Modules\.\w+(\.Contracts)?(\.Tests)?\//);
  if (!m) return null;
  const [, own, isContracts] = m;

  const bad = [];
  for (const u of added.matchAll(
    /^\s*(?:global\s+)?using\s+(?:static\s+)?(?:\w+\s*=\s*)?(Shelter\.[\w.]+)/gm,
  )) {
    const ns = u[1];
    if (ns.startsWith('Shelter.Host')) bad.push(ns);
    const mod = ns.match(/^Shelter\.Modules\.(\w+)(?:\.(\w+))?/);
    if (!mod) continue;
    const [, target, next] = mod;
    const toContracts = next === 'Contracts';
    if (isContracts ? !toContracts : target !== own && !toContracts) bad.push(ns);
  }
  return bad.length
    ? `${rel} (module ${own}${isContracts ? '.Contracts' : ''}) references ${[...new Set(bad)].join(', ')}. Cross-module access is through .Contracts only (hard rule 7).`
    : null;
}
