import { compileContent } from './compile-content.mjs';

export const VIRTUAL_CONTENT_ID = 'virtual:urman-content-catalog';
export const RESOLVED_VIRTUAL_CONTENT_ID = `\0${VIRTUAL_CONTENT_ID}`;

export function virtualCatalogSource(pack) {
  return [
    'const deepFreeze = (value) => {',
    "  if (!value || typeof value !== 'object' || Object.isFrozen(value)) return value;",
    '  for (const child of Object.values(value)) deepFreeze(child);',
    '  return Object.freeze(value);',
    '};',
    `const pack = deepFreeze(${JSON.stringify(pack)});`,
    'export { pack };',
    'export default pack;',
    '',
  ].join('\n');
}

export function createViteContentPlugin(options) {
  let source = null;
  return {
    name: 'urman-content-catalog',
    enforce: 'pre',
    async buildStart() {
      const result = await compileContent(options);
      if (!result.ok) {
        const details = result.diagnostics
          .map((entry) => `${entry.code} ${entry.sourcePath}${entry.jsonPointer}: ${entry.message}`)
          .join('\n');
        throw new Error(`URMAN content compilation failed:\n${details}`);
      }
      source = virtualCatalogSource(result.pack);
    },
    resolveId(id) {
      return id === VIRTUAL_CONTENT_ID ? RESOLVED_VIRTUAL_CONTENT_ID : null;
    },
    load(id) {
      if (id !== RESOLVED_VIRTUAL_CONTENT_ID) return null;
      if (source === null) throw new Error('URMAN content catalog was requested before buildStart compilation.');
      return source;
    },
  };
}
