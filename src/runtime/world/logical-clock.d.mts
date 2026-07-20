export interface LogicalClockSnapshot { readonly tick: number }
export class LogicalClock {
  constructor(options?: { readonly tick?: number });
  now(): number;
  advance(delta?: number): number;
  exportSnapshot(): LogicalClockSnapshot;
  static fromSnapshot(snapshot: LogicalClockSnapshot): LogicalClock;
}
