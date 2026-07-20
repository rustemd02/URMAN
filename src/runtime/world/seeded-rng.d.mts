export interface SeededRngSnapshot { readonly seed: number; readonly state: number; readonly position: number }
export class SeededRngStream {
  constructor(options: { readonly seed: number | string; readonly state?: number; readonly position?: number });
  readonly position: number;
  nextUint32(): number;
  nextFloat(): number;
  exportSnapshot(): SeededRngSnapshot;
}
export interface OwnerRngStreamsSnapshot {
  readonly masterSeed: number;
  readonly streams: readonly ({ readonly ownerId: string } & SeededRngSnapshot)[];
}
export class OwnerRngStreams {
  constructor(options?: { readonly seed?: number | string });
  stream(ownerId: string): SeededRngStream;
  exportSnapshot(): OwnerRngStreamsSnapshot;
  static fromSnapshot(snapshot: OwnerRngStreamsSnapshot): OwnerRngStreams;
}
