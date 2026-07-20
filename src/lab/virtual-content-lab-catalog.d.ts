declare module 'virtual:urman-content-lab-catalog' {
  import type { CompiledContentPack } from '../content/generated/content-types';

  export const catalog: { readonly packs: readonly CompiledContentPack[] };
  export default catalog;
}
