export const OldPcErrorCode: Readonly<Record<string, string>>;
export class OldPcError extends Error { readonly code: string; readonly details: Readonly<Record<string, unknown>>; }
