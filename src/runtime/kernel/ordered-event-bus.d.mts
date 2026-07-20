import type { DisposableSubscription, DomainEvent } from '../contracts/runtime-contracts.mjs';

export class OrderedEventBus {
  subscribe(eventType: string, handler: (event: DomainEvent) => void): DisposableSubscription;
  publish(event: DomainEvent): readonly string[];
}
