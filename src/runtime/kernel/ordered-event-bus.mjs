function deepFreeze(value) {
  if (!value || typeof value !== 'object' || Object.isFrozen(value)) return value;
  for (const child of Object.values(value)) deepFreeze(child);
  return Object.freeze(value);
}

export class OrderedEventBus {
  #nextSubscription = 1;
  #subscriptions = [];

  subscribe(eventType, handler) {
    if (typeof eventType !== 'string' || !eventType) throw new TypeError('eventType must be non-empty.');
    if (typeof handler !== 'function') throw new TypeError('event handler must be a function.');
    const record = { id: this.#nextSubscription++, eventType, handler, disposed: false };
    this.#subscriptions.push(record);
    return Object.freeze({
      get disposed() { return record.disposed; },
      dispose() { record.disposed = true; },
    });
  }

  publish(event) {
    const immutable = deepFreeze(structuredClone(event));
    const errors = [];
    const deliverySnapshot = [...this.#subscriptions];
    for (const subscription of deliverySnapshot) {
      if (subscription.disposed || (subscription.eventType !== '*' && subscription.eventType !== immutable.type)) continue;
      try {
        const outcome = subscription.handler(immutable);
        if (outcome && typeof outcome.then === 'function') {
          errors.push(`${subscription.id}:async subscribers are not supported`);
          Promise.resolve(outcome).catch(() => undefined);
        }
      } catch (error) {
        errors.push(`${subscription.id}:${error instanceof Error ? error.message : String(error)}`);
      }
    }
    this.#subscriptions = this.#subscriptions.filter(({ disposed }) => !disposed);
    return Object.freeze(errors);
  }
}
