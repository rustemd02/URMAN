import { normalizePresentationHost } from './active-provider-contracts.mjs';

/** Owns only the outward handoff, never a global scene switch. */
export class ExternalHandoffAdapter {
  #host;

  constructor(presentationHost) { this.#host = normalizePresentationHost(presentationHost); }
  handoff(fromSceneId, targetSceneId, entryAlreadyCommitted = false) {
    this.#host.handoff(Object.freeze({ fromSceneId, targetSceneId, entryAlreadyCommitted }));
  }
}
