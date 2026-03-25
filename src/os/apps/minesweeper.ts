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

  if (!board || !nextCanvas || !scoreEl || !linesEl || !resetBtn || !gameContainer) return () => {};

  const resizeObserver = new ResizeObserver(entries => {
    for (let entry of entries) {
      const { width, height } = entry.contentRect;
      const scale = Math.min(width / 300, (height - 20) / 460, 1);
      gameContainer.style.transform = `scale(${scale})`;
    }
  });
  resizeObserver.observe(appContainer);

  const ctx = board.getContext('2d');
  const nextCtx = nextCanvas.getContext('2d');

  if (!ctx || !nextCtx) return () => {};

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
    I: '#00aaaa', O: '#aaaa00', T: '#aa00aa', S: '#00aa00',
    Z: '#aa0000', J: '#0000aa', L: '#aa5500', X: '#666666'
  };

  // Easter Egg piece values related to history/lore
  const pieceValues: Record<string, number> = {
    I: 70, O: 50, T: 30, S: 20, Z: 20, J: 40, L: 40
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

  const poemText = `Кабер ташлары миңа нидер сөйли
Ләкин берни аңламыйм
Җеп өзелгән, тел югалган
Укып була тик бер генә сан, бер генә сан`;

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
    // Show score in hex as a hint to 1552 (0x610)
    if (scoreEl) scoreEl.textContent = '0x' + score.toString(16).toUpperCase().padStart(3, '0');
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
    }
    cx.globalAlpha = 1;
  };

  const drawBoardBg = () => {
    if (!ctx) return;
    ctx.fillStyle = '#bdbdbd';
    ctx.fillRect(0, 0, board.width, board.height);
    ctx.strokeStyle = '#a2a2a2';
    for (let x = 0; x <= COLS; x++) {
      ctx.beginPath(); ctx.moveTo(x * SIZE, 0); ctx.lineTo(x * SIZE, board.height); ctx.stroke();
    }
    for (let y = 0; y <= ROWS; y++) {
      ctx.beginPath(); ctx.moveTo(0, y * SIZE); ctx.lineTo(board.width, y * SIZE); ctx.stroke();
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
    while (!collide(active, 0, ghostY - active.y + 1)) ghostY++;
    for (let y = 0; y < active.matrix.length; y++) {
      for (let x = 0; x < active.matrix[y].length; x++) {
        if (active.matrix[y][x]) drawCell(ctx, active.x + x, ghostY + y, cellColor(active.type), 0.3);
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
    const h = mat.length; const w = mat[0].length;
    const out = Array.from({ length: w }, () => Array(h).fill(0));
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) out[x][h - 1 - y] = mat[y][x];
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
    if (!active) return;
    const base = pieceValues[active.type] || 10;
    score += base * linesCleared;
    
    // Cap at 0x610 (1552) if approaching it to trigger easter egg exactly
    if (score > 0x5D0 && score < 0x610) {
        // give a nudge if very close
    }
    
    // Easter Egg: 0x610 = 1552 (Year of Fall of Kazan)
    if (score > 0x610 && score < 0x640) score = 0x610;
  };

  const clearRows = () => {
    let removed = 0;
    for (let y = ROWS - 1; y >= 0; y--) {
      if (grid[y].every(cell => !!cell)) {
        grid.splice(y, 1); grid.unshift(Array(COLS).fill(''));
        removed++; y++;
      }
    }
    if (removed > 0) {
      lines += removed; addScore(removed);
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
    merge(); clearRows(); updateHud();
    if (score === 0x610 && !shown) {
      shown = true; paused = true;
      showOverlay(); return;
    }
    spawn();
  };

  const move = (dir: number) => {
    if (over || paused || !active) return;
    if (!collide(active, dir, 0)) active.x += dir;
  };

  const drop = () => {
    if (over || paused || !active) return;
    if (!collide(active, 0, 1)) active.y++;
    else lockAndNext();
    dropCounter = 0;
  };

  const hardDrop = () => {
    if (over || paused || !active) return;
    while (!collide(active, 0, 1)) active.y++;
    lockAndNext(); dropCounter = 0;
  };

  const turn = () => {
    if (over || paused || !active) return;
    const r = rotate(active.matrix);
    if (!collide(active, 0, 0, r)) active.matrix = r;
    else if (!collide(active, -1, 0, r)) { active.x--; active.matrix = r; }
    else if (!collide(active, 1, 0, r)) { active.x++; active.matrix = r; }
  };

  const draw = () => {
    if (!ctx) return;
    drawBoardBg(); drawGrid(); drawGhost(); drawPiece(active);
    if (over) {
      ctx.fillStyle = 'rgba(192,192,192,0.92)'; ctx.fillRect(20, 120, 120, 60);
      ctx.strokeStyle = '#fff'; ctx.strokeRect(20, 120, 120, 60);
      ctx.fillStyle = '#000'; ctx.font = 'bold 14px Tahoma'; ctx.textAlign = 'center';
      ctx.fillText('GAME OVER', 80, 155);
    }
  };

  const frame = (t = 0) => {
    const dt = t - lastTime; lastTime = t;
    if (!over && !paused) {
      dropCounter += dt;
      if (dropCounter >= speed) drop();
    }
    draw(); requestAnimationFrame(frame);
  };

  const showOverlay = () => {
    const box = document.createElement('div');
    box.style.cssText = 'position:absolute;inset:0;display:flex;align-items:center;justify-content:center;background:rgba(0,0,0,0.25);z-index:99999;';

    const win = document.createElement('div');
    win.style.cssText = 'width:480px;max-width:95%;background:#c0c0c0;border:2px outset #fff;box-shadow:2px 2px 0 #808080;padding:2px;';

    const bar = document.createElement('div');
    bar.style.cssText = 'background:#000080;color:#fff;padding:3px 6px;font:bold(12px) Tahoma;';
    bar.textContent = 'Message';

    const body = document.createElement('div');
    body.style.cssText = 'margin-top:2px;border:2px inset #fff;background:#fff;padding:12px;white-space:pre-line;font:14px Tahoma;line-height:1.55;color:#000;';
    body.textContent = poemText;

    const row = document.createElement('div');
    row.style.cssText = 'display:flex;justify-content:center;padding:8px;';

    const btn = document.createElement('button');
    btn.textContent = 'OK';
    btn.style.cssText = 'border:2px outset #fff;background:#c0c0c0;padding:4px 18px;cursor:pointer;font:12px Tahoma;';

    btn.addEventListener('click', () => { box.remove(); paused = false; spawn(); });

    row.appendChild(btn); win.appendChild(bar); win.appendChild(body); win.appendChild(row); box.appendChild(win);
    const app = root.querySelector('.t-app') as HTMLElement;
    if (app) { app.style.position = 'relative'; app.appendChild(box); }
  };

  const reset = () => {
    grid = emptyGrid(); score = 0; lines = 0; over = false; paused = false; shown = false;
    dropCounter = 0; lastTime = 0; speed = 550;
    if (resetBtn) resetBtn.textContent = '🙂';
    updateHud(); spawn(); draw();
  };

  const onKey = (e: KeyboardEvent) => {
    if (['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', ' '].includes(e.key)) e.preventDefault();
    if (e.key === 'ArrowLeft') move(-1);
    else if (e.key === 'ArrowRight') move(1);
    else if (e.key === 'ArrowUp') turn();
    else if (e.key === 'ArrowDown') drop();
    else if (e.key === ' ') hardDrop();
  };

  if (resetBtn) resetBtn.addEventListener('click', reset);
  window.addEventListener('keydown', onKey);
  reset();
  const animId = requestAnimationFrame(frame);

  return () => {
    window.removeEventListener('keydown', onKey);
    cancelAnimationFrame(animId);
    resizeObserver.disconnect();
  };
};