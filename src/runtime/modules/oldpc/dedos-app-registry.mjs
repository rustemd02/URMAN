import { OldPcErrorCode, oldPcError } from './oldpc-errors.mjs';

function fields(descriptor) {
  if (!descriptor || typeof descriptor !== 'object' || Array.isArray(descriptor)) return null;
  const appId = Object.getOwnPropertyDescriptor(descriptor, 'appId');
  const create = Object.getOwnPropertyDescriptor(descriptor, 'create');
  if (!appId || !Object.hasOwn(appId, 'value') || !create || !Object.hasOwn(create, 'value')) return null;
  return { appId: appId.value, create: create.value };
}

/** Exact descriptor registry for DedOS apps. The shell never switches on app IDs. */
export class DedOsAppRegistry {
  #descriptors = new Map();

  constructor(descriptors = []) {
    if (!Array.isArray(descriptors)) throw oldPcError(OldPcErrorCode.InvalidContext, 'DedOS app descriptors must be an array.');
    for (const descriptor of descriptors) this.register(descriptor);
  }

  register(descriptor) {
    const normalized = fields(descriptor);
    if (!normalized || typeof normalized.appId !== 'string' || !normalized.appId || typeof normalized.create !== 'function') {
      throw oldPcError(OldPcErrorCode.InvalidContext, 'DedOS app descriptor requires own appId and create fields.');
    }
    if (this.#descriptors.has(normalized.appId)) {
      throw oldPcError(OldPcErrorCode.InvalidContext, `DedOS app ${normalized.appId} is registered twice.`, { appId: normalized.appId });
    }
    this.#descriptors.set(normalized.appId, Object.freeze(normalized));
    return this;
  }

  ids() { return Object.freeze([...this.#descriptors.keys()].sort()); }

  create(appId, context) {
    const descriptor = this.#descriptors.get(appId);
    if (!descriptor) throw oldPcError(OldPcErrorCode.InvalidContext, `DedOS app ${appId} is not registered.`, { appId });
    const app = descriptor.create(context);
    if (!app || typeof app !== 'object' || Array.isArray(app)
      || typeof app.render !== 'function' || (app.init !== undefined && typeof app.init !== 'function')) {
      throw oldPcError(OldPcErrorCode.InvalidContext, `DedOS app ${appId} must provide render() and optional init().`, { appId });
    }
    return Object.freeze(app);
  }
}
