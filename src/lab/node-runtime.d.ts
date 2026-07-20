/** Minimal Node declarations keep the Vite-serve-only plugin outside the browser type surface. */
declare module 'node:fs/promises' {
  export function readFile(path: string, encoding: string): Promise<string>;
  export function writeFile(path: string, value: string): Promise<void>;
  export function cp(source: string, destination: string, options: { readonly recursive: boolean }): Promise<void>;
  export function mkdtemp(prefix: string): Promise<string>;
  export function rm(path: string, options: { readonly recursive: boolean; readonly force: boolean }): Promise<void>;
}

declare module 'node:path' {
  const path: {
    relative(from: string, to: string): string;
    join(...parts: string[]): string;
    dirname(path: string): string;
    basename(path: string): string;
  };
  export default path;
}

declare module 'node:os' {
  const os: { tmpdir(): string };
  export default os;
}
