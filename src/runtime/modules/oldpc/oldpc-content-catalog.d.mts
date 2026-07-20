import type { DocumentDefinition } from '../../../content/generated/content-types.js';

export interface OldPcDocument extends Pick<DocumentDefinition, 'id' | 'bodyMarkdown' | 'sourceFile' | 'accessConditions' | 'openEffects' | 'knowledgeRefs'> {
  readonly title: string;
  readonly presentationIds: readonly string[];
  readonly oldPc: {
    readonly type: string;
    readonly pcSection: string;
    readonly canonStatus: string;
    readonly reliability: string;
    readonly searchTerms: readonly string[];
    readonly suggestedTerms: readonly string[];
  };
}

export class OldPcContentCatalog {
  constructor(options: { readonly contentRegistry: { category(name: 'documents'): readonly DocumentDefinition[] }; readonly moduleId: string });
  ids(): readonly string[];
  get(documentId: string): OldPcDocument;
  sections(): readonly string[];
  search(query?: string, options?: { readonly section?: string; readonly isAccessible?: (document: OldPcDocument) => boolean }): readonly OldPcDocument[];
  suggestedTerms(options?: { readonly isAccessible?: (document: OldPcDocument) => boolean }): readonly string[];
}
export function createOldPcContentCatalog(options: ConstructorParameters<typeof OldPcContentCatalog>[0]): OldPcContentCatalog;
export function normalizeSearchTerm(value: string): string;
