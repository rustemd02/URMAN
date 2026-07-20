import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import net from 'node:net';
import path from 'node:path';

function delay(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

async function availablePort() {
  const server = net.createServer();
  await new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', resolve);
  });
  const address = server.address();
  await new Promise((resolve) => server.close(resolve));
  if (!address || typeof address === 'string') throw new Error('Cannot reserve a local Vite test port.');
  return address.port;
}

async function waitForHttp(url, child, timeoutMs = 15_000) {
  const deadline = Date.now() + timeoutMs;
  let lastError = null;
  while (Date.now() <= deadline) {
    if (child.exitCode !== null || child.signalCode !== null) throw new Error(`Vite exited before serving ${url}.`);
    try {
      const response = await fetch(url);
      if (response.ok) return;
    } catch (error) {
      lastError = error;
    }
    await delay(50);
  }
  throw new Error(`Vite did not serve ${url} within ${timeoutMs}ms: ${lastError?.message ?? 'unknown error'}`);
}

async function stop(child) {
  if (child.exitCode !== null || child.signalCode !== null) return;
  child.kill('SIGTERM');
  if (await waitForExit(child, 3_000)) return;
  child.kill('SIGKILL');
  if (await waitForExit(child, 3_000)) return;
  throw new Error('Vite did not exit after SIGKILL.');
}

async function waitForExit(child, timeoutMs) {
  if (child.exitCode !== null || child.signalCode !== null) return true;
  return new Promise((resolve) => {
    const onExit = () => {
      clearTimeout(timer);
      resolve(true);
    };
    const timer = setTimeout(() => {
      child.removeListener('exit', onExit);
      resolve(false);
    }, timeoutMs);
    child.once('exit', onExit);
  });
}

export async function startViteTestServer({ preview = false, e2e = false } = {}) {
  const viteBin = path.resolve('node_modules/vite/bin/vite.js');
  if (!existsSync(viteBin)) throw new Error(`Vite executable is missing: ${viteBin}`);
  const port = await availablePort();
  const child = spawn(process.execPath, [viteBin, preview ? 'preview' : '', '--host', '127.0.0.1', '--port', String(port), '--strictPort'].filter(Boolean), {
    env: { ...process.env, ...(e2e ? { URMAN_E2E: '1' } : {}) },
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  const url = `http://127.0.0.1:${port}`;
  try {
    await waitForHttp(url, child);
    return Object.freeze({ url, close: () => stop(child) });
  } catch (error) {
    await stop(child);
    throw error;
  }
}
