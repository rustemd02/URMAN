export const renderMinesweeper = () => `
  <div class="t-app" style="display:flex;align-items:center;justify-content:center;height:100%;background:#c0c0c0;font-family:Tahoma,sans-serif;overflow:hidden;padding:10px;">
    <div class="t-container" style="border:2px outset #fff; background:#c0c0c0; padding:4px; box-shadow:2px 2px 0 #808080; transform-origin: center center; display: flex; flex-direction: column; align-items: center;">
      <div style="display:flex;justify-content:space-between;align-items:center;gap:4px;margin-bottom:4px;border:2px inset #fff;padding:4px;background:#c0c0c0;width:100%;box-sizing:border-box;">
        <div style="background:#000;color:#f00;font-family:Courier New,monospace;font-weight:bold;padding:2px 4px;min-width:50px;text-align:right;font-size:14px;" id="t-score">0000</div>
        <button id="t-reset" style="border:2px outset #fff;background:#c0c0c0;padding:1px 6px;cursor:pointer;font-size:14px;">🙂</button>
        <div style="background:#000;color:#f00;font-family:Courier New,monospace;font-weight:bold;padding:2px 4px;min-width:50px;text-align:right;font-size:14px;" id="t-lines">000</div>
      </div>

      <div style="display:flex;gap:4px;justify-content:center;">
        <div style="border:2px inset #fff;background:#9e9e9e;padding:2px;">
          <canvas id="t-board" width="160" height="320" style="display:block;background:#bdbdbd;image-rendering:pixelated;"></canvas>
        </div>

        <div style="display:flex;flex-direction:column;gap:4px;width:70px;">
          <div style="border:2px inset #fff;padding:4px;background:#c0c0c0;">
            <div style="font-size:9px;margin-bottom:2px;">NEXT</div>
            <canvas id="t-next" width="60" height="60" style="display:block;background:#bdbdbd;image-rendering:pixelated;"></canvas>
          </div>

          <div style="border:2px inset #fff;padding:4px;background:#c0c0c0;font-size:9px;line-height:1.2;">
            ← → move<br>
            ↑ rotate<br>
            ↓ drop<br>
            space fall
          </div>
        </div>
      </div>
    </div>
  </div>
`;

interface Piece {
  type: string;
  matrix: number[][];
  x: number;
  y: number;
}

export const initMinesweeper = (root: HTMLElement | Document = document) => {
  const appContainer = root instanceof HTMLElement ? root : root.querySelector('.t-app') as HTMLElement;
  const gameContainer = appContainer.querySelector('.t-container') as HTMLElement;
  const board = appContainer.querySelector('#t-board') as HTMLCanvasElement;
  const nextCanvas = appContainer.querySelector('#t-next') as HTMLCanvasElement;
  const scoreEl = appContainer.querySelector('#t-score');
  const linesEl = appContainer.querySelector('#t-lines');
  const resetBtn = appContainer.querySelector('#t-reset');

  if (!board || !nextCanvas || !scoreEl || !linesEl || !resetBtn || !gameContainer) return;

  // Адаптивность контента через масштабирование
  const resizeObserver = new ResizeObserver(entries => {
    for (let entry of entries) {
      const { width, height } = entry.contentRect;
      // Используем реальные размеры контейнера (примерно 250x380)
      const scale = Math.min(width / 250, height / 380, 1);
      gameContainer.style.transform = `scale(${scale})`;
    }
  });
  resizeObserver.observe(appContainer);

  const ctx = board.getContext('2d');
  const nextCtx = nextCanvas.getContext('2d');

  if (!ctx || !nextCtx) return;

  const COLS = 10;
  const ROWS = 20;
  const SIZE = 16; 

  const pieces: Record<string, number[][]> = {
    I: [[1, 1, 1, 1]],
    O: [[1, 1], [1, 1]],
    T: [[0, 1, 0], [1, 1, 1]],
    S: [[0, 1, 1], [1, 1, 0]],
    Z: [[1, 1, 0], [0, 1, 1]],
    J: [[1, 0, 0], [1, 1, 1]],
    L: [[0, 0, 1], [1, 1, 1]],
  };

  const palette: Record<string, string> = {
    I: '#00aaaa',
    O: '#aaaa00',
    T: '#aa00aa',
    S: '#00aa00',
    Z: '#aa0000',
    J: '#0000aa',
    L: '#aa5500',
    X: '#666666'
  };

  const order = Object.keys(pieces);

  let grid: string[][] = [];
  let active: Piece | null = null;
  let next: Piece | null = null;
  let score = 0;
  let lines = 0;
  let over = false;
  let paused = false;
  let dropCounter = 0;
  let lastTime = 0;
  let speed = 550;
  let shown = false;

  const msgData = [
    '0JrQsNCx0LXRgCDRgtCw0YjQu9Cw0YDRiyDQvNC40qPCwINC90LjQtNC10YAg0YHө0Lli',
    '0JvTmc60LjQvNC40YAg0LHQtdGA0L3QuCDQsNGj0LvQsNC80YvQvQ==',
    '0Җ0L/Qv9GB0LTRhdC80LDQvSwgdNC10Lsg0Y7Qs9Cw0LvQsNC9',
    '0KPQutGL0L8g0LHRg9C70LAg0YLQuNC6INCx0LXRgCDQs9C10L3Tmcgc0YHQsNC9LCDQsdC10YAg0LPQtdC90Y8gc0YHQsNC9',
    '0KLQuNC6INCx0LXRgCwg0LHRgNC90LAg0LPQtdC90Y8gc0YHQsNC90LCDQsdC10YAg0LPQtdC90Y8gc0YHQsNC9',
    '0KLQuNC6INCx0LXRgCwg0LHRgNC90LAg0LPQtdC90Y8gc0YHQsNC9LCDRgtC40Log0LHRgNC90LAg0LPQtdC90Y8gc0YHQsNC9',
    '0JHQtdGAINCz0LXQvdCwIOGB0LDQvSwg0LHRgNC90LAg0LPQtdC90Y8gc0YHQsNC9',
    '0JHQtdGAINCz0LXQvdCwIOGB0LDQvSwg0LHRgNC90LAg0LPQtdC90Y8gc0YHQsNC9LCAxNTUy',
    'MTU1MiwgMTU1Miwgw6fÓ™0YLQtdGA0LXQvNC0w50gMTU1Mg=='
  ];

  const decode = (s: string) => {
    try {
      return decodeURIComponent(atob(s).split('').map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2)).join(''));
    } catch (e) { return s; }
  };

  const getText = () => msgData.map(decode).join('\n');

  const emptyGrid = (): string[][] => Array.from({ length: ROWS }, () => Array(COLS).fill(''));

  const clone = (m: number[][]) => m.map(r => [...r]);

  const randPiece = (): Piece => {
    const type = order[(Math.random() * order.length) | 0];
    return {
      type,
      matrix: clone(pieces[type]),
      x: ((COLS / 2) | 0) - ((pieces[type][0].length / 2) | 0),
      y: 0
    };
  };

  const updateHud = () => {
    if (scoreEl) scoreEl.textContent = String(score).padStart(4, '0');
    if (linesEl) linesEl.textContent = String(lines).padStart(3, '0');
  };

  const cellColor = (v: string) => palette[v] || '#000';

  const drawCell = (cx: CanvasRenderingContext2D, x: number, y: number, color: string, alpha = 1) => {
    cx.globalAlpha = alpha;
    cx.fillStyle = color;
    cx.fillRect(x * SIZE, y * SIZE, SIZE, SIZE);
    
    if (alpha === 1) {
      cx.strokeStyle = '#e8e8e8';
      cx.lineWidth = 1;
      cx.beginPath();
      cx.moveTo(x * SIZE, y * SIZE + SIZE);
      cx.lineTo(x * SIZE, y * SIZE);
      cx.lineTo(x * SIZE + SIZE, y * SIZE);
      cx.stroke();
      cx.strokeStyle = '#555';
      cx.beginPath();
      cx.moveTo(x * SIZE + SIZE, y * SIZE);
      cx.lineTo(x * SIZE + SIZE, y * SIZE + SIZE);
      cx.lineTo(x * SIZE, y * SIZE + SIZE);
      cx.stroke();
    } else {
      cx.strokeStyle = color;
      cx.strokeRect(x * SIZE + 0.5, y * SIZE + 0.5, SIZE - 1, SIZE - 1);
    }
    cx.globalAlpha = 1;
  };

  const drawBoardBg = () => {
    if (!ctx) return;
    ctx.fillStyle = '#bdbdbd';
    ctx.fillRect(0, 0, board.width, board.height);
    ctx.strokeStyle = '#a2a2a2';
    for (let x = 0; x <= COLS; x++) {
      ctx.beginPath();
      ctx.moveTo(x * SIZE, 0);
      ctx.lineTo(x * SIZE, board.height);
      ctx.stroke();
    }
    for (let y = 0; y <= ROWS; y++) {
      ctx.beginPath();
      ctx.moveTo(0, y * SIZE);
      ctx.lineTo(board.width, y * SIZE);
      ctx.stroke();
    }
  };

  const drawGrid = () => {
    if (!ctx) return;
    for (let y = 0; y < ROWS; y++) {
      for (let x = 0; x < COLS; x++) {
        if (grid[y][x]) drawCell(ctx, x, y, cellColor(grid[y][x]));
      }
    }
  };

  const drawGhost = () => {
    if (!active || !ctx) return;
    let ghostY = active.y;
    while (!collide(active, 0, ghostY - active.y + 1)) {
      ghostY++;
    }
    for (let y = 0; y < active.matrix.length; y++) {
      for (let x = 0; x < active.matrix[y].length; x++) {
        if (active.matrix[y][x]) {
          drawCell(ctx, active.x + x, ghostY + y, cellColor(active.type), 0.3);
        }
      }
    }
  };

  const drawPiece = (p: Piece | null) => {
    if (!p || !ctx) return;
    for (let y = 0; y < p.matrix.length; y++) {
      for (let x = 0; x < p.matrix[y].length; x++) {
        if (p.matrix[y][x]) drawCell(ctx, p.x + x, p.y + y, cellColor(p.type));
      }
    }
  };

  const drawNext = () => {
    if (!nextCtx) return;
    nextCtx.clearRect(0, 0, nextCanvas.width, nextCanvas.height);
    nextCtx.fillStyle = '#bdbdbd';
    nextCtx.fillRect(0, 0, nextCanvas.width, nextCanvas.height);

    if (!next) return;

    const previewSize = 12; 
    const mw = next.matrix[0].length;
    const mh = next.matrix.length;
    const ox = ((nextCanvas.width - mw * previewSize) / 2) | 0;
    const oy = ((nextCanvas.height - mh * previewSize) / 2) | 0;

    for (let y = 0; y < mh; y++) {
      for (let x = 0; x < mw; x++) {
        if (!next.matrix[y][x]) continue;
        nextCtx.fillStyle = cellColor(next.type);
        nextCtx.fillRect(ox + x * previewSize, oy + y * previewSize, previewSize, previewSize);
        nextCtx.strokeStyle = '#e8e8e8';
        nextCtx.beginPath();
        nextCtx.moveTo(ox + x * previewSize, oy + y * previewSize + previewSize);
        nextCtx.lineTo(ox + x * previewSize, oy + y * previewSize);
        nextCtx.lineTo(ox + x * previewSize + previewSize, oy + y * previewSize);
        nextCtx.stroke();
        nextCtx.strokeStyle = '#555';
        nextCtx.beginPath();
        nextCtx.moveTo(ox + x * previewSize + previewSize, oy + y * previewSize);
        nextCtx.lineTo(ox + x * previewSize + previewSize, oy + y * previewSize + previewSize);
        nextCtx.lineTo(ox + x * previewSize, oy + y * previewSize + previewSize);
        nextCtx.stroke();
      }
    }
  };

  const collide = (p: Piece, dx = 0, dy = 0, mat = p.matrix) => {
    for (let y = 0; y < mat.length; y++) {
      for (let x = 0; x < mat[y].length; x++) {
        if (!mat[y][x]) continue;
        const nx = p.x + x + dx;
        const ny = p.y + y + dy;
        if (nx < 0 || nx >= COLS || ny >= ROWS) return true;
        if (ny >= 0 && grid[ny][nx]) return true;
      }
    }
    return false;
  };

  const rotate = (mat: number[][]) => {
    const h = mat.length;
    const w = mat[0].length;
    const out = Array.from({ length: w }, () => Array(h).fill(0));
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        out[x][h - 1 - y] = mat[y][x];
      }
    }
    return out;
  };

  const merge = () => {
    if (!active) return;
    for (let y = 0; y < active.matrix.length; y++) {
      for (let x = 0; x < active.matrix[y].length; x++) {
        if (active.matrix[y][x]) {
          const gy = active.y + y;
          const gx = active.x + x;
          if (gy >= 0) grid[gy][gx] = active.type;
        }
      }
    }
  };

  const addScore = (linesCleared: number) => {
    if (score < 0x578) {
      score += 70 * linesCleared;
    } else if (score < 0x609) {
      score += 10 * linesCleared;
    } else if (score < 0x610) {
      score += 1 * linesCleared;
    } else {
      score += 70 * linesCleared;
    }
    
    if (score > 0x610 && score < 0x640) score = 0x610;
  };

  const clearRows = () => {
    let removed = 0;
    for (let y = ROWS - 1; y >= 0; y--) {
      if (grid[y].every(cell => !!cell)) {
        grid.splice(y, 1);
        grid.unshift(Array(COLS).fill(''));
        removed++;
        y++;
      }
    }
    if (removed > 0) {
      lines += removed;
      addScore(removed);
      if (speed > 120) speed -= removed * 8;
    }
  };

  const spawn = () => {
    active = next || randPiece();
    active.x = ((COLS / 2) | 0) - ((active.matrix[0].length / 2) | 0);
    active.y = 0;
    next = randPiece();
    drawNext();
    if (collide(active, 0, 0)) {
      over = true;
      if (resetBtn) resetBtn.textContent = '☹';
    }
  };

  const lockAndNext = () => {
    merge();
    clearRows();
    updateHud();
    if (score === 0x610 && !shown) {
      shown = true;
      paused = true;
      showOverlay();
      return;
    }
    spawn();
  };

  const move = (dir: number) => {
    if (over || paused || !active) return;
    if (!collide(active, dir, 0)) active.x += dir;
  };

  const drop = () => {
    if (over || paused || !active) return;
    if (!collide(active, 0, 1)) {
      active.y++;
    } else {
      lockAndNext();
    }
    dropCounter = 0;
  };

  const hardDrop = () => {
    if (over || paused || !active) return;
    while (!collide(active, 0, 1)) active.y++;
    lockAndNext();
    dropCounter = 0;
  };

  const turn = () => {
    if (over || paused || !active) return;
    const r = rotate(active.matrix);
    if (!collide(active, 0, 0, r)) {
      active.matrix = r;
      return;
    }
    if (!collide(active, -1, 0, r)) {
      active.x--;
      active.matrix = r;
      return;
    }
    if (!collide(active, 1, 0, r)) {
      active.x++;
      active.matrix = r;
      return;
    }
  };

  const draw = () => {
    if (!ctx) return;
    drawBoardBg();
    drawGrid();
    drawGhost(); 
    drawPiece(active);

    if (over) {
      ctx.fillStyle = 'rgba(192,192,192,0.92)';
      ctx.fillRect(20, 120, 120, 60);
      ctx.strokeStyle = '#fff';
      ctx.strokeRect(20, 120, 120, 60);
      ctx.fillStyle = '#000';
      ctx.font = 'bold 14px Tahoma';
      ctx.textAlign = 'center';
      ctx.fillText('GAME OVER', 80, 155);
    }
  };

  const frame = (t = 0) => {
    const dt = t - lastTime;
    lastTime = t;

    if (!over && !paused) {
      dropCounter += dt;
      if (dropCounter >= speed) drop();
    }

    draw();
    requestAnimationFrame(frame);
  };

  const showOverlay = () => {
    const box = document.createElement('div');
    box.style.position = 'absolute';
    box.style.inset = '0';
    box.style.display = 'flex';
    box.style.alignItems = 'center';
    box.style.justifyContent = 'center';
    box.style.background = 'rgba(0,0,0,0.25)';
    box.style.zIndex = '99999';

    const win = document.createElement('div');
    win.style.width = '480px';
    win.style.maxWidth = '95%';
    win.style.background = '#c0c0c0';
    win.style.border = '2px outset #fff';
    win.style.boxShadow = '2px 2px 0 #808080';
    win.style.padding = '2px';

    const bar = document.createElement('div');
    bar.style.background = '#000080';
    bar.style.color = '#fff';
    bar.style.padding = '3px 6px';
    bar.style.font = 'bold 12px Tahoma';
    bar.textContent = 'Message';

    const body = document.createElement('div');
    body.style.marginTop = '2px';
    body.style.border = '2px inset #fff';
    body.style.background = '#fff';
    body.style.padding = '12px';
    body.style.whiteSpace = 'pre-line';
    body.style.font = '14px Tahoma';
    body.style.lineHeight = '1.55';
    body.style.color = '#000';
    body.textContent = getText();

    const row = document.createElement('div');
    row.style.display = 'flex';
    row.style.justifyContent = 'center';
    row.style.padding = '8px';

    const btn = document.createElement('button');
    btn.textContent = 'OK';
    btn.style.border = '2px outset #fff';
    btn.style.background = '#c0c0c0';
    btn.style.padding = '4px 18px';
    btn.style.cursor = 'pointer';
    btn.style.font = '12px Tahoma';

    btn.addEventListener('click', () => {
      box.remove();
      paused = false;
      spawn();
    });

    row.appendChild(btn);
    win.appendChild(bar);
    win.appendChild(body);
    win.appendChild(row);
    box.appendChild(win);

    const app = root.querySelector('.t-app') as HTMLElement;
    if (app) {
      app.style.position = 'relative';
      app.appendChild(box);
    }
  };

  const reset = () => {
    grid = emptyGrid();
    score = 0;
    lines = 0;
    over = false;
    paused = false;
    shown = false;
    dropCounter = 0;
    lastTime = 0;
    speed = 550;
    if (resetBtn) resetBtn.textContent = '🙂';
    updateHud();
    spawn();
    draw();
  };

  const onKey = (e: KeyboardEvent) => {
    if (['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', ' '].includes(e.key)) {
      e.preventDefault();
    }
    if (e.key === 'ArrowLeft') move(-1);
    else if (e.key === 'ArrowRight') move(1);
    else if (e.key === 'ArrowUp') turn();
    else if (e.key === 'ArrowDown') drop();
    else if (e.key === ' ') hardDrop();
  };

  if (resetBtn) resetBtn.addEventListener('click', reset);
  window.addEventListener('keydown', onKey);

  reset();
  requestAnimationFrame(frame);
};
