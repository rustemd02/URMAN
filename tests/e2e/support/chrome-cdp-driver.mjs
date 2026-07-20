import { spawn } from 'node:child_process';
import { constants as fsConstants } from 'node:fs';
import { access } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { mkdtemp, rm } from 'node:fs/promises';

const DEFAULT_TIMEOUT_MS = 15_000;
const DEFAULT_POLL_INTERVAL_MS = 50;

function delay(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

function distinct(values) {
  return [...new Set(values.filter(Boolean))];
}

async function executable(pathname, platform) {
  try {
    await access(pathname, platform === 'win32' ? fsConstants.F_OK : fsConstants.X_OK);
    return true;
  } catch {
    return false;
  }
}

export function chromeCandidates({ env = process.env, platform = process.platform } = {}) {
  const candidates = [];
  if (env.CHROME_PATH) candidates.push(env.CHROME_PATH);
  if (platform === 'darwin') {
    candidates.push(
      '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
      '/Applications/Chromium.app/Contents/MacOS/Chromium',
    );
  } else if (platform === 'linux') {
    candidates.push(
      '/usr/bin/google-chrome', '/usr/bin/google-chrome-stable', '/usr/bin/chromium', '/usr/bin/chromium-browser',
      '/usr/local/bin/google-chrome', '/usr/local/bin/google-chrome-stable', '/usr/local/bin/chromium', '/usr/local/bin/chromium-browser',
    );
  } else if (platform === 'win32') {
    const roots = distinct([env.PROGRAMFILES, env['PROGRAMFILES(X86)'], env.LOCALAPPDATA]);
    for (const root of roots) {
      candidates.push(
        path.win32.join(root, 'Google', 'Chrome', 'Application', 'chrome.exe'),
        path.win32.join(root, 'Chromium', 'Application', 'chrome.exe'),
      );
    }
  }
  return Object.freeze(distinct(candidates));
}

function chromeUnavailableError({ platform, candidates, reason = undefined }) {
  return new Error([
    `Chrome/CDP is unavailable on ${platform}.`,
    reason ? `Reason: ${reason}` : '',
    `Checked executable paths:\n${candidates.map((candidate) => `- ${candidate}`).join('\n') || '- (none)'}`,
    'Install Google Chrome/Chromium or set CHROME_PATH to a browser executable.',
  ].filter(Boolean).join('\n'));
}

export async function findChromeExecutable(options = {}) {
  const platform = options.platform ?? process.platform;
  const candidates = options.candidates ?? chromeCandidates({ ...options, platform });
  for (const candidate of candidates) {
    if (await executable(candidate, platform)) return candidate;
  }
  throw chromeUnavailableError({ platform, candidates });
}

function waitForDevToolsEndpoint(processHandle, timeoutMs) {
  return new Promise((resolve, reject) => {
    let output = '';
    let settled = false;
    const finish = (callback, value) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      callback(value);
    };
    const capture = (chunk) => {
      output += chunk.toString();
      const match = /DevTools listening on (ws:\/\/[^\s]+)/.exec(output);
      if (match) finish(resolve, match[1]);
    };
    const timer = setTimeout(() => finish(reject, new Error(`Chrome did not expose a DevTools endpoint within ${timeoutMs}ms. Output:\n${output}`)), timeoutMs);
    processHandle.stdout?.on('data', capture);
    processHandle.stderr?.on('data', capture);
    processHandle.once('error', (error) => finish(reject, error));
    processHandle.once('exit', (code, signal) => finish(reject, new Error(`Chrome exited before CDP connected (code ${code}, signal ${signal}). Output:\n${output}`)));
  });
}

function waitForSocketOpen(socket, timeoutMs) {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error(`CDP WebSocket did not open within ${timeoutMs}ms.`)), timeoutMs);
    socket.addEventListener('open', () => { clearTimeout(timer); resolve(); }, { once: true });
    socket.addEventListener('error', () => { clearTimeout(timer); reject(new Error('CDP WebSocket connection failed.')); }, { once: true });
  });
}

class CdpConnection {
  #socket;
  #nextRequestId = 1;
  #pending = new Map();
  #events = [];
  #closed = false;

  constructor(socket) {
    this.#socket = socket;
    socket.addEventListener('message', (event) => this.#receive(event));
    socket.addEventListener('close', () => this.#failPending(new Error('CDP WebSocket closed.')));
    socket.addEventListener('error', () => this.#failPending(new Error('CDP WebSocket failed.')));
  }

  async request(method, params = {}) {
    if (this.#closed) throw new Error('CDP connection is closed.');
    const id = this.#nextRequestId;
    this.#nextRequestId += 1;
    return new Promise((resolve, reject) => {
      this.#pending.set(id, { resolve, reject });
      this.#socket.send(JSON.stringify({ id, method, params }));
    });
  }

  events() {
    return Object.freeze([...this.#events]);
  }

  close() {
    if (this.#closed) return;
    this.#closed = true;
    this.#failPending(new Error('CDP connection was closed.'));
    this.#socket.close();
  }

  #receive(event) {
    const message = JSON.parse(String(event.data));
    if (typeof message.id === 'number') {
      const pending = this.#pending.get(message.id);
      if (!pending) return;
      this.#pending.delete(message.id);
      if (message.error) pending.reject(new Error(`CDP ${message.error.code}: ${message.error.message}`));
      else pending.resolve(message.result ?? {});
      return;
    }
    this.#events.push(Object.freeze({ sequence: this.#events.length + 1, method: message.method, params: message.params ?? {} }));
  }

  #failPending(error) {
    for (const { reject } of this.#pending.values()) reject(error);
    this.#pending.clear();
  }
}

async function pageSocketEndpoint(browserEndpoint) {
  const endpoint = new URL(browserEndpoint);
  const protocol = endpoint.protocol === 'wss:' ? 'https:' : 'http:';
  const response = await fetch(`${protocol}//${endpoint.host}/json/list`);
  if (!response.ok) throw new Error(`Chrome CDP target discovery failed with HTTP ${response.status}.`);
  const targets = await response.json();
  const target = targets.find((candidate) => candidate.type === 'page' && candidate.webSocketDebuggerUrl);
  if (!target) throw new Error('Chrome CDP target discovery returned no page target.');
  return target.webSocketDebuggerUrl;
}

function evaluateFailure(result, expression) {
  if (!result.exceptionDetails) return null;
  const details = result.exceptionDetails;
  return new Error(`Page evaluation failed: ${details.text ?? 'unknown exception'}\n${expression}`);
}

async function waitForExit(processHandle, timeoutMs) {
  if (processHandle.exitCode !== null || processHandle.signalCode !== null) return true;
  return new Promise((resolve) => {
    const onExit = () => {
      clearTimeout(timer);
      resolve(true);
    };
    const timer = setTimeout(() => {
      processHandle.removeListener('exit', onExit);
      resolve(false);
    }, timeoutMs);
    processHandle.once('exit', onExit);
  });
}

async function terminateProcess(processHandle, label) {
  if (processHandle.exitCode !== null || processHandle.signalCode !== null) return;
  processHandle.kill('SIGTERM');
  if (await waitForExit(processHandle, 3_000)) return;
  processHandle.kill('SIGKILL');
  if (await waitForExit(processHandle, 3_000)) return;
  throw new Error(`${label} did not exit after SIGKILL.`);
}

class ChromePage {
  #process;
  #profileDirectory;
  #connection;
  #closed = false;

  constructor({ processHandle, profileDirectory, connection }) {
    this.#process = processHandle;
    this.#profileDirectory = profileDirectory;
    this.#connection = connection;
  }

  async goto(url) {
    await this.#connection.request('Page.navigate', { url });
    await this.#waitForReady(new URL(url).href);
  }

  async evaluate(expression) {
    const result = await this.#connection.request('Runtime.evaluate', {
      expression,
      awaitPromise: true,
      returnByValue: true,
      userGesture: true,
    });
    const failure = evaluateFailure(result, expression);
    if (failure) throw failure;
    return result.result?.value;
  }

  async click(selector) {
    const quoted = JSON.stringify(selector);
    await this.evaluate(`(() => { const element = document.querySelector(${quoted}); if (!element) throw new Error('Missing selector: ' + ${quoted}); element.click(); return true; })()`);
  }

  async waitFor(selector, { timeoutMs = DEFAULT_TIMEOUT_MS, intervalMs = DEFAULT_POLL_INTERVAL_MS } = {}) {
    const deadline = Date.now() + timeoutMs;
    const quoted = JSON.stringify(selector);
    while (Date.now() <= deadline) {
      if (await this.evaluate(`Boolean(document.querySelector(${quoted}))`)) return;
      await delay(intervalMs);
    }
    throw new Error(`Timed out after ${timeoutMs}ms waiting for selector: ${selector}`);
  }

  async text(selector) {
    const quoted = JSON.stringify(selector);
    return this.evaluate(`(() => { const element = document.querySelector(${quoted}); if (!element) throw new Error('Missing selector: ' + ${quoted}); return element.textContent ?? ''; })()`);
  }

  async focus(selector) {
    const quoted = JSON.stringify(selector);
    const focused = await this.evaluate(`(() => { const element = document.querySelector(${quoted}); if (!element) throw new Error('Missing selector: ' + ${quoted}); element.focus(); return document.activeElement === element; })()`);
    if (focused !== true) throw new Error(`Browser could not focus selector: ${selector}`);
  }

  async screenshot() {
    const result = await this.#connection.request('Page.captureScreenshot', { format: 'png' });
    return Buffer.from(result.data, 'base64');
  }

  events() {
    return this.#connection.events();
  }

  async close() {
    if (this.#closed) return;
    this.#closed = true;
    this.#connection.close();
    await terminateProcess(this.#process, 'Chrome');
    await rm(this.#profileDirectory, { recursive: true, force: true });
  }

  async #waitForReady(expectedUrl) {
    const deadline = Date.now() + DEFAULT_TIMEOUT_MS;
    while (Date.now() <= deadline) {
      if (await this.evaluate(`location.href === ${JSON.stringify(expectedUrl)} && document.readyState !== 'loading'`)) return;
      await delay(DEFAULT_POLL_INTERVAL_MS);
    }
    throw new Error(`Page did not reach ${expectedUrl} within ${DEFAULT_TIMEOUT_MS}ms.`);
  }
}

export async function launch({ timeoutMs = DEFAULT_TIMEOUT_MS, chromePath = undefined } = {}) {
  const platform = process.platform;
  const candidates = chromePath ? Object.freeze([chromePath]) : chromeCandidates({ platform });
  let profileDirectory;
  let processHandle;
  try {
    const executable = await findChromeExecutable({ platform, candidates });
    profileDirectory = await mkdtemp(path.join(os.tmpdir(), 'urman-cdp-profile-'));
    processHandle = spawn(executable, [
      '--headless=new', '--remote-debugging-port=0', `--user-data-dir=${profileDirectory}`,
      '--no-first-run', '--no-default-browser-check', '--remote-allow-origins=*', '--disable-gpu', 'about:blank',
    ], { stdio: ['ignore', 'pipe', 'pipe'] });
    const browserEndpoint = await waitForDevToolsEndpoint(processHandle, timeoutMs);
    const pageEndpoint = await pageSocketEndpoint(browserEndpoint);
    const socket = new WebSocket(pageEndpoint);
    await waitForSocketOpen(socket, timeoutMs);
    const connection = new CdpConnection(socket);
    await connection.request('Page.enable');
    await connection.request('Runtime.enable');
    return new ChromePage({ processHandle, profileDirectory, connection });
  } catch (error) {
    try {
      if (processHandle) await terminateProcess(processHandle, 'Chrome');
      if (profileDirectory) await rm(profileDirectory, { recursive: true, force: true });
    } catch (cleanupError) {
      const cleanupMessage = cleanupError instanceof Error ? cleanupError.message : String(cleanupError);
      const failureMessage = error instanceof Error ? error.message : String(error);
      throw chromeUnavailableError({ platform, candidates, reason: `${failureMessage}; cleanup failed: ${cleanupMessage}` });
    }
    if (error instanceof Error && error.message.startsWith('Chrome/CDP is unavailable on ')) throw error;
    const failureMessage = error instanceof Error ? error.message : String(error);
    throw chromeUnavailableError({ platform, candidates, reason: failureMessage });
  }
}

export const goto = (page, url) => page.goto(url);
export const evaluate = (page, expression) => page.evaluate(expression);
export const click = (page, selector) => page.click(selector);
export const waitFor = (page, selector, options) => page.waitFor(selector, options);
export const text = (page, selector) => page.text(selector);
export const focus = (page, selector) => page.focus(selector);
export const screenshot = (page) => page.screenshot();
export const close = (page) => page.close();
