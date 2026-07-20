import type {
  CompiledContentPack,
  ConditionEffectCondition,
  ConditionEffectEffect,
  SceneDefinition,
} from '../../content/generated/content-types.js';

export class RegistryError extends Error {
  readonly code: string;
  readonly details: Readonly<Record<string, unknown>>;
}

export class ContentRegistry {
  static fromPack(pack: CompiledContentPack): ContentRegistry;
  get(id: string): unknown;
  has(id: string): boolean;
  ids(): readonly string[];
  category(name: keyof CompiledContentPack['registries']): readonly unknown[];
}

export interface SceneDescriptor<T = unknown> {
  readonly sceneType: SceneDefinition['sceneType'];
  create(definition: SceneDefinition, context: unknown): T;
}
export class SceneRegistry {
  constructor(pack: CompiledContentPack, descriptors?: readonly SceneDescriptor[]);
  register(descriptor: SceneDescriptor): this;
  definition(sceneId: string): SceneDefinition;
  create(sceneId: string, context: unknown): unknown;
}

export interface ConditionDescriptor {
  readonly op: ConditionEffectCondition['op'];
  evaluate(condition: ConditionEffectCondition, context: unknown): boolean;
}
export class ConditionRegistry {
  constructor(pack: CompiledContentPack, descriptors?: readonly ConditionDescriptor[]);
  register(descriptor: ConditionDescriptor): this;
  require(op: string): ConditionDescriptor;
  evaluate(condition: ConditionEffectCondition, context: unknown): boolean;
  ids(): readonly string[];
}

export interface EffectDescriptor {
  readonly op: ConditionEffectEffect['op'];
  plan(effect: ConditionEffectEffect, context: unknown): unknown;
}
export class EffectRegistry {
  constructor(pack: CompiledContentPack, descriptors?: readonly EffectDescriptor[]);
  register(descriptor: EffectDescriptor): this;
  require(op: string): EffectDescriptor;
  plan(effect: ConditionEffectEffect, context: unknown): unknown;
  ids(): readonly string[];
}

export interface CapabilityDescriptor {
  readonly protocolId: string;
  readonly exactVersion: string;
  create(config: unknown, context: unknown): unknown;
  readonly [key: string]: unknown;
}
export class CapabilityRegistry {
  constructor(pack: CompiledContentPack, descriptors?: readonly CapabilityDescriptor[]);
  register(descriptor: CapabilityDescriptor): this;
  require(protocolId: string, exactVersion: string): CapabilityDescriptor;
}
