export interface DedOsApp { render(): string; init?(root: HTMLElement): void | (() => void); }
export interface DedOsAppDescriptor { readonly appId: string; create(context: unknown): DedOsApp; }
export class DedOsAppRegistry {
  constructor(descriptors?: readonly DedOsAppDescriptor[]);
  register(descriptor: DedOsAppDescriptor): this;
  ids(): readonly string[];
  create(appId: string, context: unknown): Readonly<DedOsApp>;
}
