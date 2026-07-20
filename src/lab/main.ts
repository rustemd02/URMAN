import catalog from 'virtual:urman-content-lab-catalog';

import { RuntimeBootstrap, PRODUCTION_OLD_PC_INSTANCE_ID } from '../runtime/bootstrap/runtime-bootstrap.mjs';
import { replaceActiveLabRun } from './run-replacement.mjs';

type LabPack = {
  readonly campaign: {
    readonly id: string;
    readonly entrypoint: string;
    readonly roleBindings: Readonly<Record<string, string>>;
    readonly capabilityRequirements: readonly { readonly protocolId: string; readonly exactVersion: string }[];
    readonly narrativeOrder: readonly string[];
  };
  readonly moduleFingerprints: readonly { readonly moduleId: string; readonly exactVersion: string }[];
  readonly dependencyGraph: { readonly nodes: readonly string[]; readonly edges: readonly { readonly from: string; readonly to: string }[] };
  readonly registries: Readonly<Record<string, readonly any[]>>;
  readonly campaignFingerprint: string;
};

type SwapKind = 'character' | 'dialogue' | 'asset' | 'quest-order';

type LabCatalog = {
  readonly packs: readonly LabPack[];
  readonly variants: readonly { readonly baseCampaignId: string; readonly swap: SwapKind; readonly pack: LabPack }[];
};

const labCatalog = catalog as unknown as LabCatalog;
if (labCatalog.packs.length === 0) throw new Error('Content Lab requires at least one compiled campaign.');
const STORAGE_NAMESPACE = 'urman.content-lab.v1';
const appElement = document.querySelector<HTMLElement>('#app');
if (!appElement) throw new Error('Content Lab requires #app.');
const app: HTMLElement = appElement;

let activeRun: RuntimeBootstrap | null = null;
let runSequence = 0;
let cleanupState = 'No capability has been started yet.';

const state = {
  campaignId: labCatalog.packs[0].campaign.id,
  moduleId: labCatalog.packs[0].moduleFingerprints[0]?.moduleId ?? '',
  questId: labCatalog.packs[0].registries.quests?.[0]?.id ?? '',
  sceneId: labCatalog.packs[0].campaign.entrypoint,
  preset: 'accessible',
  seed: 'content-lab-seed',
  swap: 'character' as SwapKind,
  runId: '',
  eventLog: [] as string[],
};

function selectedPack(): LabPack {
  const selected = labCatalog.packs.find((candidate) => candidate.campaign.id === state.campaignId);
  if (!selected) throw new Error(`Content Lab campaign ${state.campaignId} is not in the dev catalog.`);
  return selected;
}

function selectCampaign(campaignId: string): void {
  const next = labCatalog.packs.find((candidate) => candidate.campaign.id === campaignId);
  if (!next) throw new Error(`Content Lab campaign ${campaignId} is not in the dev catalog.`);
  state.campaignId = next.campaign.id;
  state.moduleId = next.moduleFingerprints[0]?.moduleId ?? '';
  state.questId = next.registries.quests?.[0]?.id ?? '';
  state.sceneId = next.campaign.entrypoint;
}

function isRuntimeLaunchable(pack: LabPack): boolean {
  return pack.campaign.capabilityRequirements.some(({ protocolId, exactVersion }) => (
    protocolId === 'urman.oldpc:capability/archive-hub' && exactVersion === '1.0.0'
  ));
}

function selectedCompiledRunPack(): LabPack | null {
  return labCatalog.variants.find((candidate) => (
    candidate.baseCampaignId === state.campaignId && candidate.swap === state.swap
  ))?.pack ?? null;
}

function inspectionPack(): LabPack {
  return selectedCompiledRunPack() ?? selectedPack();
}

function syncInspectionSelection(pack: LabPack): void {
  const modules = pack.moduleFingerprints.map(({ moduleId }) => moduleId);
  const quests = (pack.registries.quests ?? []).map(({ id }) => id);
  const scenes = (pack.registries.scenes ?? []).map(({ id }) => id);
  if (!modules.includes(state.moduleId)) state.moduleId = modules[0] ?? '';
  if (!quests.includes(state.questId)) state.questId = quests[0] ?? '';
  if (!scenes.includes(state.sceneId)) state.sceneId = scenes[0] ?? '';
}

function resolveRunSelection(pack: LabPack): Readonly<{
  readonly moduleId: string;
  readonly questId: string;
  readonly sceneId: string;
  readonly preset: string;
}> {
  if (!pack.moduleFingerprints.some(({ moduleId }) => moduleId === state.moduleId)) {
    throw new Error(`Selected module ${state.moduleId} is not present in compiled run ${pack.campaign.id}.`);
  }
  if (!pack.registries.quests.some(({ id }) => id === state.questId)) {
    throw new Error(`Selected quest ${state.questId} is not present in compiled run ${pack.campaign.id}.`);
  }
  if (!pack.registries.scenes.some(({ id }) => id === state.sceneId)) {
    throw new Error(`Selected scene ${state.sceneId} is not present in compiled run ${pack.campaign.id}.`);
  }
  return Object.freeze({ moduleId: state.moduleId, questId: state.questId, sceneId: state.sceneId, preset: state.preset });
}

function escape(value: string): string {
  return value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#039;');
}

function option(value: string, selected: string): string {
  return `<option value="${escape(value)}"${value === selected ? ' selected' : ''}>${escape(value)}</option>`;
}

function selectOptions(values: readonly string[], selected: string): string {
  return values.map((value) => option(value, selected)).join('');
}

function startIsolatedRun(): void {
  const runPack = selectedCompiledRunPack();
  if (!runPack) {
    cleanupState = `${state.campaignId} has no separately compiled ${state.swap} variant; it is inspectable only.`;
    return;
  }
  if (!isRuntimeLaunchable(runPack)) {
    cleanupState = `${runPack.campaign.id} is inspectable in Content Lab but needs its explicit dev-module host before it can run.`;
    return;
  }
  let selection;
  try {
    selection = resolveRunSelection(runPack);
  } catch (error) {
    cleanupState = error instanceof Error ? error.message : String(error);
    return;
  }
  const replacement = replaceActiveLabRun(
    activeRun,
    (run: RuntimeBootstrap) => run.capabilityHost.disposeSession(PRODUCTION_OLD_PC_INSTANCE_ID),
    () => new RuntimeBootstrap({ pack: runPack, seed: state.seed }),
  );
  if (!replacement.replaced) {
    cleanupState = `Could not dispose ${PRODUCTION_OLD_PC_INSTANCE_ID}; the active run remains retained and replacement is blocked: ${replacement.error instanceof Error ? replacement.error.message : String(replacement.error)}.`;
    return;
  }
  if (activeRun) cleanupState = `Disposed ${PRODUCTION_OLD_PC_INSTANCE_ID}; subscriptions, jobs and presentation handles are terminal.`;
  activeRun = replacement.activeRun as RuntimeBootstrap;
  runSequence += 1;
  state.runId = `lab-run-${runSequence}`;
  state.eventLog = [`${state.runId}: compiler output ${runPack.campaign.id} created with ${state.swap} swap; inspection module=${selection.moduleId}, quest=${selection.questId}, scene=${selection.sceneId}; capability preset=${selection.preset}`];
  sessionStorage.setItem(`${STORAGE_NAMESPACE}:last-run`, JSON.stringify({ campaignId: runPack.campaign.id, runId: state.runId, seed: state.seed, swap: state.swap, selection }));
  cleanupState = `Active ${state.runId}; isolated namespace ${STORAGE_NAMESPACE}.`;
}

function disposeRun(): boolean {
  if (!activeRun) return true;
  const replacement = replaceActiveLabRun(
    activeRun,
    (run: RuntimeBootstrap) => run.capabilityHost.disposeSession(PRODUCTION_OLD_PC_INSTANCE_ID),
    () => null,
  );
  if (!replacement.replaced) {
    cleanupState = `Could not dispose ${PRODUCTION_OLD_PC_INSTANCE_ID}; the active run remains retained: ${replacement.error instanceof Error ? replacement.error.message : String(replacement.error)}.`;
    return false;
  }
  activeRun = null;
  cleanupState = `Disposed ${PRODUCTION_OLD_PC_INSTANCE_ID}; subscriptions, jobs and presentation handles are terminal.`;
  return true;
}

function invokeSampleCapability(): void {
  if (!activeRun) {
    cleanupState = 'Start an isolated run before invoking its active capability.';
    return;
  }
  try {
    const runtimePack = activeRun.pack as LabPack;
    const provider = runtimePack.registries.capabilities.find((entry) => entry.protocolId === 'urman.oldpc:capability/archive-hub');
    if (!provider) throw new Error('The active compiled pack has no old-PC capability manifest.');
    const query = state.preset === 'accessible' ? `[accessible] ${state.seed}` : state.seed;
    const result = activeRun.oldPcPresentation().handle({ type: 'search', query }) as { readonly status?: string; readonly actionOccurrenceId?: string; readonly command?: { readonly payload?: { readonly query?: string } } };
    const outcomeId = provider.accessibility?.equivalentOutcomeId;
    const appliedQuery = result.command?.payload?.query ?? query;
    state.eventLog = [...state.eventLog, `${state.runId}: provider handle preset=${state.preset} query=${appliedQuery} ${result.status ?? 'accepted'} ${result.actionOccurrenceId ?? ''} -> ${outcomeId}`];
    cleanupState = `Active provider received ${state.preset} input and returned ${result.status ?? 'accepted'} under outcome contract ${outcomeId}.`;
  } catch (error) {
    cleanupState = error instanceof Error ? error.message : String(error);
  }
}

function render(): void {
  const labPack = inspectionPack();
  syncInspectionSelection(labPack);
  const modules = labPack.moduleFingerprints.map(({ moduleId }) => moduleId);
  const quests = (labPack.registries.quests ?? []).map(({ id }) => id);
  const scenes = (labPack.registries.scenes ?? []).map(({ id }) => id);
  const roles = Object.entries(labPack.campaign.roleBindings).map(([role, id]) => `<li><code>${escape(role)}</code> → <code>${escape(id)}</code></li>`).join('');
  const capabilities = labPack.campaign.capabilityRequirements.map(({ protocolId, exactVersion }) => `<li><code>${escape(protocolId)}@${escape(exactVersion)}</code></li>`).join('');
  const edges = labPack.dependencyGraph.edges.map(({ from, to }) => `<li><code>${escape(from)}</code> → <code>${escape(to)}</code></li>`).join('') || '<li>No module dependency edges.</li>';
  const selectedVariant = selectedCompiledRunPack();
  const diagnostics = `No compilation diagnostics: ${labCatalog.packs.length} base campaign(s) and ${labCatalog.variants.length} separately compiled swap variant(s) are in the virtual catalog.${selectedVariant ? ` Selected SHA-256: ${selectedVariant.campaignFingerprint}.` : ' Selected base has no launchable swap variant.'}`;
  const runtimeState = activeRun ? JSON.stringify(activeRun.runtimeContext.select((value) => value), null, 2) : 'No active run.';
  const campaignOptions = labCatalog.packs.map((candidate) => option(candidate.campaign.id, state.campaignId)).join('');

  app.innerHTML = `
    <main data-content-lab="true" aria-labelledby="lab-title">
      <style>
        :root { color-scheme: dark; font-family: system-ui, sans-serif; }
        main { max-width: 72rem; margin: 0 auto; padding: 1.5rem; background: #15130f; color: #f5edda; min-height: 100vh; }
        fieldset, section { border: 1px solid #6b604d; margin: 1rem 0; padding: 1rem; }
        label { display: grid; gap: .35rem; margin: .65rem 0; } select, input, button { font: inherit; padding: .45rem; } button { cursor: pointer; } code { overflow-wrap: anywhere; } .grid { display:grid; gap:1rem; grid-template-columns: repeat(auto-fit,minmax(16rem,1fr)); } output { display:block; white-space:pre-wrap; } [data-lab-status] { min-height:1.5rem; }
      </style>
      <h1 id="lab-title">УРМАН: Content Lab</h1>
      <p id="lab-storage-namespace">Isolated storage namespace: <code>${STORAGE_NAMESPACE}</code></p>
      <p data-lab-status aria-live="polite">${escape(cleanupState)}</p>
      <form data-lab-form aria-label="Параметры тестового запуска">
        <fieldset><legend>Запуск: campaign, swap, preset и seed</legend><div class="grid">
          <label>Campaign<select name="campaign" data-lab-campaign="true">${campaignOptions}</select></label>
          <label>Data-only swap<select name="swap">${selectOptions(['character', 'dialogue', 'asset', 'quest-order'], state.swap)}</select></label>
          <label>Capability input preset<select name="preset">${selectOptions(['accessible', 'standard'], state.preset)}</select></label>
          <label>Seed<input name="seed" value="${escape(state.seed)}" autocomplete="off"></label>
        </div></fieldset>
        <fieldset><legend>Inspection selection (does not mutate the frozen run pack)</legend><div class="grid">
          <label>Module<select name="module">${selectOptions(modules, state.moduleId)}</select></label>
          <label>Quest<select name="quest">${selectOptions(quests, state.questId)}</select></label>
          <label>Scene<select name="scene">${selectOptions(scenes, state.sceneId)}</select></label>
        </div></fieldset>
        <button type="button" data-lab-action="new-run">Запустить новый изолированный run</button>
        <button type="button" data-lab-action="sample-capability">Проверить доступный outcome</button>
        <button type="button" data-lab-action="cleanup">Очистить capability run</button>
      </form>
      <section aria-labelledby="lab-run-heading"><h2 id="lab-run-heading">Run</h2><output id="lab-run-id">${escape(state.runId || 'not-started')}</output><output id="lab-run-fingerprint">${escape(activeRun?.campaignFingerprint ?? 'not-started')}</output></section>
      <section aria-labelledby="lab-dag-heading"><h2 id="lab-dag-heading">Dependency DAG</h2><p>Nodes: ${labPack.dependencyGraph.nodes.map(escape).join(', ')}</p><ul>${edges}</ul></section>
      <section aria-labelledby="lab-selection-heading"><h2 id="lab-selection-heading">Inspection selection</h2><output id="lab-inspection-selection">Pack=${escape(labPack.campaign.id)}; module=${escape(state.moduleId)}; quest=${escape(state.questId)}; scene=${escape(state.sceneId)}.</output></section>
      <section aria-labelledby="lab-role-heading"><h2 id="lab-role-heading">Role bindings</h2><ul>${roles}</ul></section>
      <section aria-labelledby="lab-capability-heading"><h2 id="lab-capability-heading">Required capabilities</h2><ul>${capabilities}</ul></section>
      <section aria-labelledby="lab-diagnostics-heading"><h2 id="lab-diagnostics-heading">Diagnostics</h2><p>${diagnostics}</p></section>
      <section aria-labelledby="lab-events-heading"><h2 id="lab-events-heading">State, events and cleanup</h2><output id="lab-event-log">${escape(state.eventLog.join('\n') || 'No events.')}</output><output id="lab-state-log">${escape(runtimeState)}</output><output id="lab-cleanup">${escape(cleanupState)}</output></section>
    </main>`;

  const form = app.querySelector<HTMLFormElement>('[data-lab-form]');
  form?.addEventListener('change', () => {
    const values = new FormData(form);
    const campaignId = String(values.get('campaign') ?? state.campaignId);
    if (campaignId !== state.campaignId) {
      selectCampaign(campaignId);
      render();
      return;
    }
    state.moduleId = String(values.get('module') ?? state.moduleId);
    state.questId = String(values.get('quest') ?? state.questId);
    state.sceneId = String(values.get('scene') ?? state.sceneId);
    state.preset = String(values.get('preset') ?? state.preset);
    state.seed = String(values.get('seed') ?? state.seed);
    state.swap = String(values.get('swap') ?? state.swap) as SwapKind;
    syncInspectionSelection(inspectionPack());
    render();
  });
  form?.addEventListener('input', () => {
    const values = new FormData(form);
    state.seed = String(values.get('seed') ?? state.seed);
  });
  app.querySelector<HTMLButtonElement>('[data-lab-action="new-run"]')?.addEventListener('click', () => { startIsolatedRun(); render(); });
  app.querySelector<HTMLButtonElement>('[data-lab-action="sample-capability"]')?.addEventListener('click', () => { invokeSampleCapability(); render(); });
  app.querySelector<HTMLButtonElement>('[data-lab-action="cleanup"]')?.addEventListener('click', () => { disposeRun(); render(); });
}

render();
