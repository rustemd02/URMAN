import type { Plugin } from 'vite';
import { cp, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';

import { compileContent, discoverWorkspaceManifests } from '../../scripts/content/compile-content.mjs';

declare const process: { readonly env: Readonly<Record<string, string | undefined>>; cwd(): string };

const VIRTUAL_LAB_CATALOG_ID = 'virtual:urman-content-lab-catalog';
const RESOLVED_VIRTUAL_LAB_CATALOG_ID = `\0${VIRTUAL_LAB_CATALOG_ID}`;
const LAB_VARIANTS = ['character', 'dialogue', 'asset', 'quest-order'] as const;

type LabVariant = typeof LAB_VARIANTS[number];
type CampaignSource = {
  id: string;
  readonly modules: readonly { readonly moduleId: string }[];
  roleBindings: Record<string, string>;
  narrativeOrder: string[];
};
type ManifestSource = { readonly moduleId: string; readonly sourceFiles: readonly string[] };

function parseJson(source: string): any {
  return JSON.parse(source);
}

async function writeJson(file: string, value: unknown): Promise<void> {
  await writeFile(file, `${JSON.stringify(value, null, 2)}\n`);
}

function swapRoleBindings(campaign: CampaignSource): void {
  const roles = Object.keys(campaign.roleBindings).sort();
  if (roles.length < 2) return;
  [campaign.roleBindings[roles[0]], campaign.roleBindings[roles[1]]] = [campaign.roleBindings[roles[1]], campaign.roleBindings[roles[0]]];
}

function reorderIndependentQuests(campaign: CampaignSource): void {
  const quests = campaign.narrativeOrder.filter((id) => id.includes(':quest/')).reverse();
  let index = 0;
  campaign.narrativeOrder = campaign.narrativeOrder.map((id) => (id.includes(':quest/') ? quests[index++] : id));
}

function swapDefinitionFields(definitions: any[], kind: 'dialogue' | 'asset'): boolean {
  const candidates = definitions.filter((definition) => (
    kind === 'dialogue'
      ? definition?.id?.includes(':dialogue/') && definition.nodes?.[0]?.textId
      : definition?.id?.includes(':asset/') && definition.kind === 'image' && typeof definition.file === 'string'
  ));
  if (candidates.length < 2) return false;
  if (kind === 'dialogue') {
    [candidates[0].nodes[0].textId, candidates[1].nodes[0].textId] = [candidates[1].nodes[0].textId, candidates[0].nodes[0].textId];
  } else {
    [candidates[0].file, candidates[1].file] = [candidates[1].file, candidates[0].file];
  }
  return true;
}

async function applyVariantToCopiedModules(copiedManifests: readonly string[], variant: LabVariant): Promise<void> {
  if (variant !== 'dialogue' && variant !== 'asset') return;
  for (const manifestPath of copiedManifests) {
    const manifest = parseJson(await readFile(manifestPath, 'utf8')) as ManifestSource;
    for (const sourceFile of manifest.sourceFiles.filter((entry) => entry.endsWith('.json'))) {
      const definitionPath = path.join(path.dirname(manifestPath), sourceFile);
      const source = parseJson(await readFile(definitionPath, 'utf8'));
      const definitions = Array.isArray(source) ? source : [source];
      if (!swapDefinitionFields(definitions, variant)) continue;
      await writeJson(definitionPath, Array.isArray(source) ? definitions : definitions[0]);
      return;
    }
  }
}

/**
 * A Lab run is always a compiler result. Variants are authored-style temporary
 * copies in the OS temp directory, compiled and then discarded before their
 * immutable pack reaches the browser; source content is never mutated.
 */
async function compileLabVariant({
  cwd,
  campaignPath,
  manifestPathsById,
  variant,
}: {
  readonly cwd: string;
  readonly campaignPath: string;
  readonly manifestPathsById: ReadonlyMap<string, string>;
  readonly variant: LabVariant;
}): Promise<any> {
  const campaign = parseJson(await readFile(campaignPath, 'utf8')) as CampaignSource;
  const temporaryRoot = await mkdtemp(path.join(os.tmpdir(), 'urman-content-lab-'));
  try {
    const copiedManifests: string[] = [];
    for (const { moduleId } of campaign.modules) {
      const sourceManifest = manifestPathsById.get(moduleId);
      if (!sourceManifest) throw new Error(`Content Lab cannot resolve manifest for ${moduleId} in ${campaignPath}.`);
      const copiedModule = path.join(temporaryRoot, 'modules', moduleId);
      await cp(path.dirname(sourceManifest), copiedModule, { recursive: true });
      copiedManifests.push(path.join(copiedModule, path.basename(sourceManifest)));
    }
    campaign.id = `${campaign.id}.lab-${variant}`;
    if (variant === 'character') swapRoleBindings(campaign);
    if (variant === 'quest-order') reorderIndependentQuests(campaign);
    await applyVariantToCopiedModules(copiedManifests, variant);
    const copiedCampaignPath = path.join(temporaryRoot, 'campaign.json');
    await writeJson(copiedCampaignPath, campaign);
    const result = await compileContent({ cwd, campaignPath: copiedCampaignPath, moduleManifestPaths: copiedManifests });
    if (!result.ok) {
      const details = result.diagnostics.map((entry) => `${entry.code} ${entry.sourcePath}${entry.jsonPointer}: ${entry.message}`).join('\n');
      throw new Error(`Content Lab ${variant} variant failed to compile for ${campaignPath}:\n${details}`);
    }
    return result.pack;
  } finally {
    await rm(temporaryRoot, { recursive: true, force: true });
  }
}

async function compiledLabCatalogSource(cwd: string): Promise<string> {
  const discovered = await discoverWorkspaceManifests(cwd) as {
    readonly modulePaths: readonly string[];
    readonly campaignPaths: readonly string[];
  };
  const manifestEntries = await Promise.all(discovered.modulePaths.map(async (manifestPath) => {
    const manifest = parseJson(await readFile(manifestPath, 'utf8')) as { readonly moduleId: string };
    return [manifest.moduleId, manifestPath] as const;
  }));
  const manifestPathsById = new Map(manifestEntries);
  const packs: Array<{ readonly campaign: { readonly id: string } }> = [];
  const variants: Array<{ readonly baseCampaignId: string; readonly swap: LabVariant; readonly pack: any }> = [];
  for (const campaignPath of discovered.campaignPaths) {
    const campaign = parseJson(await readFile(campaignPath, 'utf8')) as CampaignSource;
    const moduleManifestPaths = campaign.modules.map(({ moduleId }) => {
      const manifestPath = manifestPathsById.get(moduleId);
      if (!manifestPath) throw new Error(`Content Lab cannot resolve manifest for ${moduleId} in ${campaignPath}.`);
      return path.relative(cwd, manifestPath);
    });
    const result = await compileContent({ cwd, campaignPath: path.relative(cwd, campaignPath), moduleManifestPaths });
    if (!result.ok) {
      const details = result.diagnostics.map((entry) => `${entry.code} ${entry.sourcePath}${entry.jsonPointer}: ${entry.message}`).join('\n');
      throw new Error(`Content Lab compilation failed for ${campaignPath}:\n${details}`);
    }
    packs.push(result.pack);
    if (campaign.modules.some(({ moduleId }) => moduleId === 'urman.oldpc')) {
      for (const swap of LAB_VARIANTS) {
        variants.push(Object.freeze({
          baseCampaignId: result.pack.campaign.id,
          swap,
          pack: await compileLabVariant({ cwd, campaignPath, manifestPathsById, variant: swap }),
        }));
      }
    }
  }
  const serialized = JSON.stringify({
    packs: packs.sort((left, right) => left.campaign.id.localeCompare(right.campaign.id)),
    variants: variants.sort((left, right) => `${left.baseCampaignId}:${left.swap}`.localeCompare(`${right.baseCampaignId}:${right.swap}`)),
  });
  return [
    'const deepFreeze = (value) => {',
    "  if (!value || typeof value !== 'object' || Object.isFrozen(value)) return value;",
    '  for (const child of Object.values(value)) deepFreeze(child);',
    '  return Object.freeze(value);',
    '};',
    `const catalog = deepFreeze(${serialized});`,
    'export { catalog };',
    'export default catalog;',
    '',
  ].join('\n');
}

function labHtml({ title, entry }: { readonly title: string; readonly entry: string }): string {
  return `<!doctype html>
<html lang="ru">
  <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width, initial-scale=1.0"><title>${title}</title></head>
  <body><div id="app"></div><script type="module" src="${entry}"></script></body>
</html>`;
}

/** This plugin is serve-only: production has no Lab entry or import. */
export function viteContentLabPlugin(): Plugin {
  let labCatalogSource: string | null = null;
  return {
    name: 'urman-content-lab',
    apply: 'serve',
    resolveId(id) {
      return id === VIRTUAL_LAB_CATALOG_ID ? RESOLVED_VIRTUAL_LAB_CATALOG_ID : null;
    },
    async load(id) {
      if (id !== RESOLVED_VIRTUAL_LAB_CATALOG_ID) return null;
      labCatalogSource ??= await compiledLabCatalogSource(process.cwd());
      return labCatalogSource;
    },
    configureServer(server) {
      server.middlewares.use(async (request, response, next) => {
        const requestUrl = (request as { readonly url?: string }).url;
        if (!requestUrl) return next();
        const url = new URL(requestUrl, 'http://urman.local');
        if (url.pathname !== '/' && url.pathname !== '/index.html') return next();
        const wantsLab = url.searchParams.get('lab') === '1';
        const wantsTestHarness = url.searchParams.get('e2e') === '1' && process.env.URMAN_E2E === '1';
        if (!wantsLab && !wantsTestHarness) return next();
        const html = wantsLab
          ? labHtml({ title: 'УРМАН — Content Lab', entry: '/src/lab/main.ts' })
          : labHtml({ title: 'УРМАН — E2E harness', entry: '/src/lab/e2e-harness.ts' });
        try {
          const transformed = await server.transformIndexHtml(url.pathname, html);
          response.statusCode = 200;
          response.setHeader('Content-Type', 'text/html; charset=utf-8');
          response.end(transformed);
        } catch (error) {
          next(error);
        }
      });
    },
  };
}
