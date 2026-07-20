import { RegistryErrorCode } from '../contracts/runtime-contracts.mjs';

const EXACT_VERSION = /^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$/;

export class RegistryError extends Error {
  constructor(code, message, details = {}) {
    super(message);
    this.name = 'RegistryError';
    this.code = code;
    this.details = Object.freeze({ ...details });
  }
}

function duplicate(id) {
  return new RegistryError(RegistryErrorCode.DuplicateRegistration, `Duplicate registration: ${id}.`, { id });
}

function invalid(id, reason) {
  return new RegistryError(RegistryErrorCode.InvalidRegistration, `Invalid registration ${id}: ${reason}.`, { id, reason });
}

function ownDescriptorFields(descriptor, fieldNames) {
  if (!descriptor || typeof descriptor !== 'object' || Array.isArray(descriptor)) return null;
  const fields = {};
  for (const fieldName of fieldNames) {
    const field = Object.getOwnPropertyDescriptor(descriptor, fieldName);
    if (!field || !Object.hasOwn(field, 'value')) return null;
    fields[fieldName] = field.value;
  }
  return fields;
}

function invalidDescriptorId(fields, fieldName, fallback) {
  return typeof fields?.[fieldName] === 'string' && fields[fieldName] ? fields[fieldName] : fallback;
}

function requiredPack(pack) {
  if (!pack?.registries || !pack?.campaign) {
    throw new RegistryError(RegistryErrorCode.MissingRegistration, 'CompiledContentPack is required to build runtime registries.', { id: 'CompiledContentPack' });
  }
  return pack;
}

function collectConditionOps(condition, target) {
  target.add(condition.op);
  if (condition.op === 'all' || condition.op === 'any') {
    for (const nested of condition.conditions) collectConditionOps(nested, target);
  } else if (condition.op === 'not') {
    collectConditionOps(condition.condition, target);
  }
}

function collectPackOpcodes(pack) {
  const conditions = new Set();
  const effects = new Set();
  const addConditions = (entries = []) => entries.forEach((entry) => collectConditionOps(entry, conditions));
  const addEffects = (entries = []) => entries.forEach((entry) => effects.add(entry.op));
  for (const scene of pack.registries.scenes) {
    addConditions(scene.entryConditions);
    addEffects(scene.onEnter);
    addEffects(scene.onExit);
    for (const interaction of scene.interactions) {
      addConditions(interaction.conditions);
      addEffects(interaction.effects);
    }
  }
  for (const dialogue of pack.registries.dialogues) {
    for (const node of dialogue.nodes) {
      addConditions(node.conditions);
      addEffects(node.effects);
      for (const choice of node.choices) {
        addConditions(choice.conditions);
        addEffects(choice.effects);
      }
    }
  }
  for (const portableRecord of pack.registries.documents) {
    addConditions(portableRecord.accessConditions);
    addEffects(portableRecord.openEffects);
  }
  for (const quest of pack.registries.quests) {
    addEffects(quest.cancelPolicy.effects);
    addEffects(quest.outcomes.success);
    addEffects(quest.outcomes.failure);
    addEffects(quest.outcomes.optional);
    for (const stage of quest.stages) {
      for (const objective of stage.objectives) {
        addConditions(objective.startConditions);
        addConditions(objective.completionConditions);
        addEffects(objective.completionEffects);
        addEffects(objective.failureEffects);
      }
    }
  }
  return { conditions, effects };
}

class ExactRegistry {
  #entries = new Map();

  register(id, value) {
    if (this.#entries.has(id)) throw duplicate(id);
    this.#entries.set(id, value);
    return this;
  }

  get(id) {
    if (!this.#entries.has(id)) throw new RegistryError(RegistryErrorCode.MissingRegistration, `Missing registration: ${id}.`, { id });
    return this.#entries.get(id);
  }

  has(id) {
    return this.#entries.has(id);
  }

  ids() {
    return Object.freeze([...this.#entries.keys()].sort());
  }
}

export class ContentRegistry {
  #entries = new ExactRegistry();
  #categories = new Map();

  static fromPack(pack) {
    requiredPack(pack);
    const registry = new ContentRegistry();
    for (const category of Object.keys(pack.registries).sort()) {
      const categoryEntries = new ExactRegistry();
      for (const definition of pack.registries[category]) {
        categoryEntries.register(definition.id, definition);
        registry.#entries.register(definition.id, definition);
      }
      registry.#categories.set(category, categoryEntries);
    }
    return registry;
  }

  get(id) { return this.#entries.get(id); }
  has(id) { return this.#entries.has(id); }
  ids() { return this.#entries.ids(); }
  category(name) {
    const category = this.#categories.get(name);
    if (!category) throw new RegistryError(RegistryErrorCode.MissingRegistration, `Missing content category: ${name}.`, { name });
    return Object.freeze(category.ids().map((id) => category.get(id)));
  }
}

export class SceneRegistry {
  #scenes = new ExactRegistry();
  #descriptors = new ExactRegistry();

  constructor(pack, descriptors = []) {
    requiredPack(pack);
    for (const scene of pack.registries.scenes) this.#scenes.register(scene.id, scene);
    for (const descriptor of descriptors) this.register(descriptor);
    for (const sceneType of new Set(pack.registries.scenes.map((scene) => scene.sceneType))) this.#descriptors.get(sceneType);
  }

  register(descriptor) {
    const fields = ownDescriptorFields(descriptor, ['sceneType', 'create']);
    if (!fields || typeof fields.sceneType !== 'string' || !fields.sceneType || typeof fields.create !== 'function') {
      throw invalid(invalidDescriptorId(fields, 'sceneType', 'scene-descriptor'), 'own data sceneType and create function are required');
    }
    this.#descriptors.register(fields.sceneType, Object.freeze(fields));
    return this;
  }

  definition(sceneId) { return this.#scenes.get(sceneId); }
  create(sceneId, context) {
    const definition = this.definition(sceneId);
    return this.#descriptors.get(definition.sceneType).create(definition, context);
  }
}

class OpcodeRegistry {
  #descriptors = new ExactRegistry();
  #handlerName;
  constructor(descriptors = [], handlerName) {
    this.#handlerName = handlerName;
    for (const descriptor of descriptors) this.register(descriptor);
  }
  register(descriptor) {
    const fields = ownDescriptorFields(descriptor, ['op', this.#handlerName]);
    if (!fields || typeof fields.op !== 'string' || !fields.op || typeof fields[this.#handlerName] !== 'function') {
      throw invalid(invalidDescriptorId(fields, 'op', 'opcode-descriptor'), `own data op and ${this.#handlerName} function are required`);
    }
    this.#descriptors.register(fields.op, Object.freeze(fields));
    return this;
  }
  require(op) { return this.#descriptors.get(op); }
  ids() { return this.#descriptors.ids(); }
}

export class ConditionRegistry extends OpcodeRegistry {
  constructor(pack, descriptors = []) {
    super(descriptors, 'evaluate');
    for (const op of collectPackOpcodes(requiredPack(pack)).conditions) this.require(op);
  }
  evaluate(condition, context) { return this.require(condition.op).evaluate(condition, context); }
}

export class EffectRegistry extends OpcodeRegistry {
  constructor(pack, descriptors = []) {
    super(descriptors, 'plan');
    for (const op of collectPackOpcodes(requiredPack(pack)).effects) this.require(op);
  }
  plan(effect, context) { return this.require(effect.op).plan(effect, context); }
}

export class CapabilityRegistry {
  #providers = new ExactRegistry();

  constructor(pack, descriptors = []) {
    requiredPack(pack);
    for (const descriptor of descriptors) this.register(descriptor);
    for (const requirement of pack.campaign.capabilityRequirements) this.require(requirement.protocolId, requirement.exactVersion);
  }

  register(descriptor) {
    const fields = ownDescriptorFields(descriptor, ['protocolId', 'exactVersion', 'create']);
    if (!fields
      || typeof fields.protocolId !== 'string'
      || !fields.protocolId
      || typeof fields.exactVersion !== 'string'
      || !EXACT_VERSION.test(fields.exactVersion)
      || typeof fields.create !== 'function') {
      throw invalid(invalidDescriptorId(fields, 'protocolId', 'capability-descriptor'), 'own data protocolId, exactVersion and create function are required');
    }
    this.#providers.register(fields.protocolId, Object.freeze(fields));
    return this;
  }

  require(protocolId, exactVersion) {
    const descriptor = this.#providers.get(protocolId);
    if (descriptor.exactVersion !== exactVersion) {
      throw new RegistryError(
        RegistryErrorCode.IncompatibleExactVersion,
        `Capability ${protocolId} requires ${exactVersion}, registered ${descriptor.exactVersion}.`,
        { protocolId, required: exactVersion, registered: descriptor.exactVersion },
      );
    }
    return descriptor;
  }
}
