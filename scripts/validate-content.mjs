import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';

const root = process.cwd();
const errors = [];

function rel(filePath) {
  return path.relative(root, filePath);
}

function walk(dir, predicate) {
  if (!fs.existsSync(dir)) return [];
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const abs = path.join(dir, entry.name);
    if (entry.isDirectory()) return walk(abs, predicate);
    if (entry.isFile() && predicate(abs)) return [abs];
    return [];
  });
}

function lineNumber(text, index) {
  return text.slice(0, index).split('\n').length;
}

function checkRuntimeCanonDrift() {
  const srcFiles = walk(path.join(root, 'src'), (filePath) => filePath.endsWith('.ts'));
  const allowedContext = /(кромк|границ|лес|урман edge|forest|edge|урочищ|запрет|стар[а-я]*\s+(?:назван|мест)|warning|label|section|folder|launchSection|схем|подступ|route_kara_urman|loc_kara_urman|kara_urman|Кара-Урману)/iu;
  const karaRe = /Кара-Урман(?:а|у|ом|е|га)?/giu;

  for (const filePath of srcFiles) {
    const relative = rel(filePath);
    const text = fs.readFileSync(filePath, 'utf8');
    let match;
    while ((match = karaRe.exec(text))) {
      const start = Math.max(0, match.index - 90);
      const end = Math.min(text.length, match.index + 120);
      const context = text.slice(start, end).replace(/\s+/g, ' ').trim();
      if (relative === 'src/os/apps/oldPcHub.ts') continue;
      if (!allowedContext.test(context)) {
        errors.push(`${relative}:${lineNumber(text, match.index)}: active runtime text may treat Кара-Урман as the village/place of delivery: "${context}"`);
      }
    }

    const debugRe = /debug_bypass['"`]?\s*:\s*true/g;
    while ((match = debugRe.exec(text))) {
      errors.push(`${relative}:${lineNumber(text, match.index)}: debug_bypass defaults to true`);
    }
  }
}

function runValidator(label, args) {
  const result = spawnSync(process.execPath, args, {
    cwd: root,
    stdio: 'inherit',
  });
  if (result.status !== 0) {
    errors.push(`${label} failed with exit code ${result.status ?? 'unknown'}`);
  }
}

checkRuntimeCanonDrift();
runValidator('validate:old-pc', ['scripts/validate-old-pc-content.mjs']);
runValidator('validate:clue-graph', ['scripts/validate-clue-graph.mjs']);
runValidator('validate:route-graph', ['scripts/validate-route-graph.mjs']);

if (errors.length) {
  console.error(`content validation failed: ${errors.length} issue(s)`);
  for (const error of [...new Set(errors)].sort()) console.error(`- ${error}`);
  process.exit(1);
}

console.log('content validation ok');
