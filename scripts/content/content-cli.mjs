#!/usr/bin/env node
import { writeFile } from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

import { auditWorkspace, compileContent, resolveWorkspaceModuleSelection } from './compile-content.mjs';

function parseArguments(argv) {
  const [command = 'check', ...tokens] = argv;
  const options = { command, modules: [] };
  for (let index = 0; index < tokens.length; index += 1) {
    const token = tokens[index];
    const value = tokens[index + 1];
    if (token === '--module' && value) { options.modules.push(value); index += 1; }
    else if (token === '--campaign' && value) { options.campaign = value; index += 1; }
    else if (token === '--out' && value) { options.out = value; index += 1; }
    else throw new Error(`Unknown or incomplete argument: ${token}`);
  }
  return options;
}

function printDiagnostics(diagnostics, io = console) {
  for (const entry of diagnostics) {
    io.error(`${entry.code} ${entry.sourcePath}${entry.jsonPointer}: ${entry.message}`);
  }
}

export async function runContentCli(argv = process.argv.slice(2), io = console, { cwd = process.cwd() } = {}) {
  let options;
  try { options = parseArguments(argv); } catch (error) {
    io.error(error.message);
    return 2;
  }
  if (options.command === 'check') {
    if (options.modules.length > 1) {
      io.error('check accepts at most one --module selection.');
      return 2;
    }
    const result = await auditWorkspace({ cwd, moduleSelection: options.modules[0] });
    if (!result.ok) { printDiagnostics(result.diagnostics, io); return 1; }
    io.log(`checked ${result.checked.modules} modules, ${result.checked.campaigns} campaigns`);
    return 0;
  }
  if (options.command === 'compile') {
    const modulePaths = [];
    for (const selection of options.modules) {
      const resolved = await resolveWorkspaceModuleSelection(selection, cwd);
      if (!resolved.ok) { printDiagnostics(resolved.diagnostics, io); return 1; }
      modulePaths.push(resolved.modulePath);
    }
    const result = await compileContent({ cwd, campaignPath: options.campaign, moduleManifestPaths: modulePaths });
    if (!result.ok) { printDiagnostics(result.diagnostics, io); return 1; }
    const output = `${JSON.stringify(result.pack, null, 2)}\n`;
    if (options.out) await writeFile(path.resolve(cwd, options.out), output);
    else io.log(output.trimEnd());
    return 0;
  }
  io.error(`Unknown content command: ${options.command}`);
  return 2;
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
  process.exitCode = await runContentCli();
}
