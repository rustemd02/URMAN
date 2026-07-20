import { immutableModel, requireResolver } from './active-provider-contracts.mjs';

/** Resolver-only route view model renderer. */
export class RoutePresentationRenderer {
  #assets;
  #texts;
  #locale;

  constructor({ assetResolver, textResolver, locale = undefined } = {}) {
    this.#assets = requireResolver(assetResolver, 'resolve', 'Asset resolver');
    this.#texts = requireResolver(textResolver, 'resolve', 'Text resolver');
    this.#locale = locale;
  }

  render(scene) {
    const texts = scene.textRefs.map((textRef) => this.#texts.resolve(textRef, this.#locale));
    return immutableModel({
      kind: 'route',
      sceneId: scene.id,
      title: texts[0] ?? null,
      texts,
      assets: scene.assetRefs.map((assetRef) => this.#assets.resolve(assetRef)),
      actions: scene.interactions.map((interaction) => Object.freeze({
        id: interaction.id,
        label: this.#texts.resolve(interaction.labelTextId, this.#locale),
      })),
    });
  }
}
