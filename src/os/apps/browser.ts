import webContent from '../data/web_content.json';

type BrowserPage =
  | 'home'
  | 'search'
  | 'tatarwiki'
  | 'tukay'
  | 'news'
  | 'forum'
  | 'thread'
  | 'maps'
  | 'poem';

type TukayPoemMeta = {
  id: string;
  title: string;
  year: string;
  url: string;
};

type BrowserState = {
  history: Array<{ page: BrowserPage; payload?: any; address?: string }>;
  historyIndex: number;
  current: { page: BrowserPage; payload?: any; address?: string };
  tabs: Array<{ id: string; title: string; page: BrowserPage; payload?: any; address?: string }>;
  activeTabId: string;
  poems: TukayPoemMeta[];
};

const DEFAULT_POEMS: TukayPoemMeta[] = [
  {
    id: 'tugan-tel',
    title: 'Туган тел',
    year: '1909',
    url: 'https://gabdullatukay.ru/works/poem/1909/tugan-tel/',
  },
  {
    id: 'shurele',
    title: 'Шүрәле',
    year: '1907',
    url: 'https://gabdullatukay.ru/works/poem/1907/shurele/',
  },
  {
    id: 'shurale-ru',
    title: 'Шурале (пер. Р. Бухараева)',
    year: '1907',
    url: 'https://gabdullatukay.ru/rus/works/poems/1907-god/shurale-per-r-buharaeva/',
  },
];

const TATARWIKI_ARTICLES = [
  {
    slug: 'shurale',
    title: 'Шүрәле',
    subtitle: 'Персонаж татарской мифологии',
    body: `
      <p><b>Шүрәле</b> — один из самых узнаваемых персонажей татарского фольклора. 
      Обычно описывается как лесное существо, связанное с чащей, дорогой, заблудившимися путниками и нарушением границ между миром человека и природой.</p>
      <p>В литературной традиции образ Шүрәле получил особую известность благодаря поэме Габдуллы Тукая.</p>
      <p><b>Связанные статьи:</b> Урман, Габдулла Тукай, Су анасы</p>
    `,
  },
  {
    slug: 'su-anasy',
    title: 'Су анасы',
    subtitle: 'Образ водяной хозяйки в татарской традиции',
    body: `
      <p><b>Су анасы</b> — мифологический образ, связанный с водой, рекой, озером, памятью о пропаже, страхом перед глубиной и темой нарушенного запрета.</p>
      <p>В современном культурном переосмыслении может трактоваться не только как фольклорный персонаж, но и как символ утраты, вины и зовущего прошлого.</p>
      <p><b>Связанные статьи:</b> Шүрәле, Урман, Догалар</p>
    `,
  },
  {
    slug: 'gabdulla-tukay',
    title: 'Габдулла Тукай',
    subtitle: 'Татарский поэт, публицист, литературный критик',
    body: `
      <p><b>Габдулла Тукай</b> — ключевая фигура татарской литературы начала XX века. 
      Его стихи и поэмы, включая «Туган тел» и «Шүрәле», стали частью культурного канона.</p>
      <p>В этой сборке браузера раздел со стихами использует загрузку текстов с реального сайта, посвященного Тукаю.</p>
      <p><b>Связанные статьи:</b> Туган тел, Шүрәле, Татарская поэзия</p>
    `,
  },
  {
    slug: 'urman',
    title: 'Урман',
    subtitle: 'Лес как культурный и символический образ',
    body: `
      <p><b>Урман</b> в татарской словесности и фольклоре часто выступает как граница между понятным и неизвестным.</p>
      <p>В игровом контексте это удобный символ пространства, где бытовое, историческое и мифологическое наслаиваются друг на друга.</p>
      <p><b>Связанные статьи:</b> Шүрәле, Су анасы, Деревня</p>
    `,
  },
];

const NEWS_ITEMS = [
  {
    title: 'В Арском районе открыли выставку о Тукае и народной поэзии',
    text: 'Школьники, библиотекари и местные краеведы представили рукописные сборники, иллюстрации и редкие издания.',
  },
  {
    title: 'ТатарВики: цифровая энциклопедия локальной культуры получила новый дизайн',
    text: 'В тестовой версии появились страницы о фольклоре, поэтах и деревенской истории.',
  },
  {
    title: 'В сельских библиотеках обсуждают перевод классических текстов на современный интерфейс',
    text: 'Библиотекари считают, что молодежь лучше вовлекается через экранные форматы и интерактивное чтение.',
  },
];

const FORUM_THREADS = [
  {
    author: 'Aidar_16',
    title: 'Кто-нибудь знает норм сайт со стихами Тукая?',
    replies: 12,
  },
  {
    author: 'alsu_k',
    title: 'Почему в старых стихах столько слов, которые без словаря не понять',
    replies: 7,
  },
  {
    author: 'Babay77',
    title: 'Нужен архив карт деревни до трассы',
    replies: 3,
  },
];

function escapeHtml(text: string): string {
  return text
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}

function stripTags(html: string): string {
  return html
    .replace(/<script[\s\S]*?<\/script>/gi, ' ')
    .replace(/<style[\s\S]*?<\/style>/gi, ' ')
    .replace(/<br\s*\/?>/gi, '\n')
    .replace(/<\/p>/gi, '\n\n')
    .replace(/<\/div>/gi, '\n')
    .replace(/<\/li>/gi, '\n')
    .replace(/<[^>]+>/g, ' ')
    .replace(/&nbsp;/g, ' ')
    .replace(/&laquo;/g, '«')
    .replace(/&raquo;/g, '»')
    .replace(/&quot;/g, '"')
    .replace(/&#039;/g, "'")
    .replace(/&amp;/g, '&')
    .replace(/[ \t]+\n/g, '\n')
    .replace(/\n{3,}/g, '\n\n')
    .replace(/[ \t]{2,}/g, ' ')
    .trim();
}

function poemLinesFromHtml(html: string): string[] {
  const selectors = [
    /<div[^>]*class="[^"]*entry-content[^"]*"[^>]*>([\s\S]*?)<\/div>/i,
    /<article[\s\S]*?<div[^>]*class="[^"]*content[^"]*"[^>]*>([\s\S]*?)<\/div>[\s\S]*?<\/article>/i,
    /<div[^>]*class="[^"]*post-content[^"]*"[^>]*>([\s\S]*?)<\/div>/i,
    /<main[\s\S]*?>([\s\S]*?)<\/main>/i,
  ];

  let bodyHtml = '';
  for (const re of selectors) {
    const match = html.match(re);
    if (match?.[1]) {
      bodyHtml = match[1];
      break;
    }
  }

  if (!bodyHtml) {
    bodyHtml = html;
  }

  const text = stripTags(bodyHtml);
  const lines = text
    .split('\n')
    .map((s) => s.trim())
    .filter(Boolean);

  const filtered = lines.filter((line) => {
    if (line.length < 2) return false;
    if (line.length > 120) return false;
    if (/^(меню|поиск|главная|биография|works|рус|eng|комментарии|источник|академик басма)/i.test(line)) return false;
    return true;
  });

  return filtered.slice(0, 120);
}

async function fetchExternalHtml(url: string): Promise<string> {
  const proxyUrl = `/api/browser-proxy?url=${encodeURIComponent(url)}`;
  const res = await fetch(proxyUrl, { method: 'GET' });
  if (!res.ok) {
    throw new Error(`Proxy error: ${res.status}`);
  }
  return await res.text();
}

async function loadPoemFromSource(meta: TukayPoemMeta): Promise<{ title: string; year: string; lines: string[]; sourceUrl: string }> {
  const html = await fetchExternalHtml(meta.url);
  const lines = poemLinesFromHtml(html);
  return {
    title: meta.title,
    year: meta.year,
    lines,
    sourceUrl: meta.url,
  };
}

function winButton(label: string, cls = ''): string {
  return `
    <button class="win-btn ${cls}" style="
      height:18px;
      min-width:32px;
      border:1px outset #fff;
      background:#c0c0c0;
      font-size:9px;
      cursor:pointer;
      padding:0 4px;
      display:flex;
      align-items:center;
      justify-content:center;
    ">${label}</button>
  `;
}

function renderHomePage(): string {
  return `
   <div style="background: #ffcc00; height: 60px; border-bottom: 2px solid #000; display: flex; align-items: center; padding: 0 15px; gap: 15px; overflow: visible;">
    <img src="/assets/yangir.png" 
         style="height: 200%; width: auto; padding: 5px 0; object-fit: contain;" 
         alt="Yangir">
    
    <span style="font-size: 13px; color: black;">Барысы да явачак</span>
</div>
    <div style="padding:16px;">
      <div style="display:flex; align-items:baseline; gap:10px;">
        <div style="font-size:30px; color:#0b45b5; font-weight:bold; letter-spacing:-1px;">TatarNet</div>
      </div>

      <div style="margin-top:16px; display:grid; grid-template-columns:repeat(2, minmax(220px,1fr)); gap:12px;">
        <div class="browser-link-card" data-open-page="tatarwiki" style="border:2px outset #fff; background:#efefef; padding:12px; cursor:pointer;">
          <div style="font-weight:bold; color:#003399;">ТатарВики</div>
        </div>

        <div class="browser-link-card" data-open-page="tukay" style="border:2px outset #fff; background:#efefef; padding:12px; cursor:pointer;">
          <div style="font-weight:bold; color:#003399;">Стихи Тукая</div>
        </div>

        <div class="browser-link-card" data-open-page="news" style="border:2px outset #fff; background:#efefef; padding:12px; cursor:pointer;">
          <div style="font-weight:bold; color:#003399;">Новости</div>
        </div>

        <div class="browser-link-card" data-open-page="forum" style="border:2px outset #fff; background:#efefef; padding:12px; cursor:pointer;">
          <div style="font-weight:bold; color:#003399;">Форум</div>
        </div>
      </div>
    </div>
  `;
}

function renderSearchPage(query: string): string {
  const q = query.trim().toLowerCase();

  const rows = [
    {
      title: 'ТатарВики — Габдулла Тукай',
      desc: 'Биография, произведения, культурный контекст.',
      page: 'tatarwiki',
      payload: { slug: 'gabdulla-tukay' },
      url: 'tatarwiki://gabdulla-tukay',
    },
    {
      title: 'Стихи Тукая — Туган тел',
      desc: 'Текст стихотворения.',
      page: 'poem',
      payload: { poemId: 'tugan-tel' },
      url: 'tukay://tugan-tel',
    },
    {
      title: 'Стихи Тукая — Шүрәле',
      desc: 'Поэма и связанный фольклорный контекст.',
      page: 'poem',
      payload: { poemId: 'shurele' },
      url: 'tukay://shurele',
    },
    {
      title: 'ТатарВики — Шүрәле',
      desc: 'Энциклопедическая статья о персонаже.',
      page: 'tatarwiki',
      payload: { slug: 'shurale' },
      url: 'tatarwiki://shurale',
    },
    {
      title: 'Форум — Кто знает сайт со стихами Тукая?',
      desc: 'Обсуждение удобных источников.',
      page: 'forum',
      payload: {},
      url: 'forum://threads',
    },
  ].filter((row) => {
    if (!q) return true;
    return `${row.title} ${row.desc}`.toLowerCase().includes(q);
  });

  return `
    <div style="background: #ffcc00; padding: 10px; border-bottom: 2px solid #000; display: flex; align-items: center; gap: 15px;">
        <img src="/assets/yangir.png" style="height: 30px; width: auto; max-width: 100%; object-fit: contain;" alt="Yangir">
        <span style="font-size: 11px; color: black; font-weight: normal;">Найдется всё</span>
    </div>
    <div style="padding:16px;">
      <div style="font-size:12px; color:#666; margin-bottom:16px;">
        Результаты поиска для: <b>${escapeHtml(query)}</b><br>
        Найдено: <b>${rows.length}</b>
      </div>

      ${rows
        .map(
          (row) => `
        <div class="browser-search-result" data-open-page="${row.page}" data-payload='${escapeHtml(JSON.stringify(row.payload))}' data-address="${escapeHtml(row.url)}" style="margin-bottom:18px;">
          <div style="color:#0000ee; text-decoration:underline; cursor:pointer; font-size:18px;"><b>${row.title}</b></div>
          <div style="font-size:13px; color:#000; margin-top:4px;">${row.desc}</div>
          <div style="font-size:12px; color:#008000; margin-top:2px;">${row.url}</div>
        </div>
      `
        )
        .join('')}
    </div>
  `;
}

function renderTatarWiki(slug?: string): string {
  const article = TATARWIKI_ARTICLES.find((x) => x.slug === slug) ?? TATARWIKI_ARTICLES[0];

  return `
    <div style="display:flex; min-height:100%;">
      <aside style="width:180px; border-right:1px solid #ccc; background:#f3f3f3; padding:12px; flex-shrink:0;">
        <div style="font-weight:bold; color:#333; margin-bottom:10px; font-size:14px;">ТатарВики</div>
        ${TATARWIKI_ARTICLES.map(
          (item) => `
            <div class="tw-nav-item" data-open-page="tatarwiki" data-payload='${escapeHtml(JSON.stringify({ slug: item.slug }))}' style="
              padding:4px 6px;
              margin-bottom:4px;
              cursor:pointer;
              font-size:12px;
              background:${item.slug === article.slug ? '#d9e7ff' : 'transparent'};
              border:1px solid ${item.slug === article.slug ? '#8ab0ff' : 'transparent'};
            ">
              ${item.title}
            </div>
          `
        ).join('')}
      </aside>

      <main style="flex:1; padding:18px;">
        <div style="font-size:24px; font-weight:bold; margin-bottom:4px;">${article.title}</div>
        <div style="font-size:11px; color:#666; margin-bottom:16px;">${article.subtitle}</div>

        <div style="font-size:13px; line-height:1.5; max-width:100%;">
          ${article.body}
        </div>
      </main>
    </div>
  `;
}

function renderTukayIndex(poems: TukayPoemMeta[]): string {
  return `
    <div style="padding:16px;">
      <div style="font-size:24px; font-weight:bold;">Стихи Тукая</div>
      <div style="font-size:11px; color:#666; margin-top:4px;">
        Локальный каталог.
      </div>

      <div style="margin-top:18px; display:flex; flex-direction:column; gap:8px;">
        ${poems
          .map(
            (poem) => `
          <div class="browser-poem-link" data-open-page="poem" data-payload='${escapeHtml(
            JSON.stringify({ poemId: poem.id })
          )}' style="border:1px solid #ccc; padding:10px; cursor:pointer; background:#fafafa;">
            <div style="color:#003399; text-decoration:underline; font-weight:bold; font-size:14px;">${poem.title}</div>
            <div style="font-size:11px; color:#666; margin-top:2px;">Год: ${poem.year}</div>
          </div>
        `
          )
          .join('')}
      </div>
    </div>
  `;
}

function renderNewsPage(): string {
  return `
    <div style="padding:16px;">
      <div style="font-size:24px; font-weight:bold;">Новости</div>
      <div style="margin-top:18px;">
        ${NEWS_ITEMS.map(
          (item) => `
          <div style="padding:10px 0; border-bottom:1px solid #e5e5e5;">
            <div style="font-size:16px; color:#003399;"><b>${item.title}</b></div>
            <div style="font-size:12px; margin-top:4px; line-height:1.4;">${item.text}</div>
          </div>
        `
        ).join('')}
      </div>
    </div>
  `;
}

function renderForumPage(): string {
  const threads = (webContent as any).forum?.threads || [];
  return `
    <div style="padding:16px;">
      <div style="font-size:24px; font-weight:bold;">Форум</div>
      <div style="margin-top:18px;">
        ${threads.map(
          (thread: any) => `
          <div style="border:1px solid #ccc; background:#fafafa; padding:8px 10px; margin-bottom:6px; cursor:pointer;" class="forum-thread-link" data-thread-id="${thread.id}">
            <div style="font-size:14px; color:#003399; text-decoration:underline;">${thread.title}</div>
            <div style="font-size:11px; color:#666; margin-top:2px;">Автор: ${thread.author} • Ответов: ${thread.replies.length}</div>
          </div>
        `
        ).join('')}
      </div>
    </div>
  `;
}

function renderThreadPage(threadId: string): string {
  const thread = (webContent as any).forum?.threads.find((t: any) => t.id === threadId);
  if (!thread) return 'Thread not found';
  
  return `
    <div style="padding:16px;">
      <div style="font-size:18px; font-weight:bold; color:#003399; margin-bottom:15px;">${thread.title}</div>
      <div style="display:flex; flex-direction:column; gap:10px;">
        ${thread.replies.map((r: any) => `
          <div style="border:1px solid #ddd; padding:8px; background:#fff;">
            <div style="font-weight:bold; font-size:12px; color:#555; margin-bottom:4px;">${r.author}</div>
            <div style="font-size:13px;">${r.text}</div>
          </div>
        `).join('')}
      </div>
      <button class="win98-btn" data-open-page="forum" style="margin-top:15px;">Назад к списку</button>
    </div>
  `;
}

function renderMapsPage(): string {
  return `
    <div style="padding:16px;">
      <div style="font-size:24px; font-weight:bold;">Карты</div>
      <div style="margin-top:18px; border:2px inset #fff; background:#e8e8e8; height:240px; position:relative;">
        <div style="position:absolute; left:20px; top:20px; width:100px; height:60px; border:1px solid #999; background:#d5e8c7;"></div>
        <div style="position:absolute; left:150px; top:80px; width:120px; height:70px; border:1px solid #999; background:#d8d0f0;"></div>
        <div style="position:absolute; left:80px; top:160px; width:150px; height:30px; border:1px solid #999; background:#d9edf7;"></div>
        <div style="position:absolute; left:30px; top:40px; font-size:10px;">Старые дома</div>
        <div style="position:absolute; left:180px; top:110px; font-size:10px;">Поле</div>
        <div style="position:absolute; left:130px; top:168px; font-size:10px;">Река</div>
      </div>
    </div>
  `;
}

function renderPoemShell(): string {
  return `
    <div style="padding:16px;">
      <div id="browser-poem-state" style="font-size:13px; color:#444;">Загрузка стихотворения...</div>
    </div>
  `;
}

function makeTabTitle(page: BrowserPage, payload?: any): string {
  if (page === 'home') return 'Главная';
  if (page === 'search') return 'Поиск';
  if (page === 'tatarwiki') return 'ТатарВики';
  if (page === 'tukay') return 'Тукай';
  if (page === 'news') return 'Новости';
  if (page === 'forum') return 'Форум';
  if (page === 'maps') return 'Карты';
  if (page === 'poem') return 'Стих';
  return 'Вкладка';
}

function renderTabs(state: BrowserState): string {
  return state.tabs
    .map((tab) => {
      const active = tab.id === state.activeTabId;
      return `
        <div class="browser-tab ${active ? 'active' : ''}" data-tab-id="${tab.id}" style="
          display:flex;
          align-items:center;
          gap:4px;
          max-width:120px;
          padding:2px 6px;
          border:1px solid #808080;
          border-bottom:${active ? '1px solid #c0c0c0' : '1px solid #808080'};
          background:${active ? '#c0c0c0' : '#d8d8d8'};
          cursor:pointer;
          white-space:nowrap;
          font-size:11px;
        ">
          <span style="overflow:hidden; text-overflow:ellipsis; flex:1;">${tab.title}</span>
          <span class="browser-tab-close" data-close-tab-id="${tab.id}" style="font-weight:bold; color:#333; cursor:pointer; margin-left:4px;">×</span>
        </div>
      `;
    })
    .join('');
}

function renderAddress(address?: string): string {
  return escapeHtml(address || 'about:home');
}

export const renderBrowser = () => `
  <div class="browser-container" style="
    display:flex;
    flex-direction:column;
    height:100%;
    background:#c0c0c0;
    font-family:Tahoma, sans-serif;
    color:#000;
    overflow:hidden;
  ">
    <div id="browser-tabs-bar" style="
      display:flex;
      gap:2px;
      align-items:end;
      padding:2px 2px 0 2px;
      background:#c0c0c0;
      border-bottom:1px solid #808080;
      min-height:24px;
      overflow-x:auto;
    "></div>

    <div class="browser-toolbar" style="
      display:flex;
      align-items:center;
      gap:2px;
      padding:2px;
      border-bottom:1px solid #808080;
      box-shadow:inset 1px 1px #fff;
      background:#c0c0c0;
    ">
      ${winButton('⬅', 'browser-back')}
      ${winButton('➡', 'browser-forward')}
      ${winButton('🏠', 'browser-home')}

      <div style="
        flex:1;
        display:flex;
        align-items:center;
        background:#fff;
        border:1px solid;
        border-color:#808080 #fff #fff #808080;
        padding:0 2px;
        height:18px;
      ">
        <input id="browser-address" type="text" value="about:home" style="flex:1; border:none; font-size:9px; outline:none; height:14px; background:transparent;">
      </div>

      ${winButton('Go', 'browser-go')}
    </div>

    <div style="
      display:flex;
      align-items:center;
      gap:2px;
      padding:2px;
      background:#d6d6d6;
      border-bottom:1px solid #9b9b9b;
    ">
      <input id="browser-search-input" type="text" placeholder="Поиск в Яндэк..." style="
        flex:1;
        border:1px solid;
        border-color:#808080 #fff #fff #808080;
        padding:1px 3px;
        font-size:9px;
        background:#fff;
        height:18px;
      ">
      ${winButton('Искать', 'browser-search-btn')}
    </div>

    <div id="browser-content" class="browser-content" style="
      flex:1;
      background:#fff;
      border:2px solid;
      border-color:#808080 #fff #fff #808080;
      overflow:auto;
      padding:0;
    "></div>

    <div style="
      height:18px;
      background:#c0c0c0;
      border-top:1px solid #808080;
      display:flex;
      align-items:center;
      padding:0 4px;
      font-size:9px;
      gap:8px;
    ">
      <div id="browser-status" style="flex:1; border-right:1px solid #808080; padding-right:4px;">Готово</div>
      <div style="width:60px; border-right:1px solid #808080; display:flex; align-items:center; gap:2px;">
        🌐 <span style="font-weight:bold;">Online</span>
      </div>
      <div style="width:60px; text-align:center;">TatarNet</div>
    </div>
  </div>
`;

export const initBrowser = (root: ParentNode = document) => {
  const container = root.querySelector('.browser-container') as HTMLElement | null;
  if (!container) return;

  const contentEl = container.querySelector('#browser-content') as HTMLElement;
  const addressEl = container.querySelector('#browser-address') as HTMLInputElement;
  const searchInputEl = container.querySelector('#browser-search-input') as HTMLInputElement;
  const statusEl = container.querySelector('#browser-status') as HTMLElement;
  const tabsBarEl = container.querySelector('#browser-tabs-bar') as HTMLElement;

  const state: BrowserState = {
    history: [{ page: 'home', address: 'about:home' }],
    historyIndex: 0,
    current: { page: 'home', address: 'about:home' },
    tabs: [{ id: 'tab-home', title: 'Главная', page: 'home', address: 'about:home' }],
    activeTabId: 'tab-home',
    poems: DEFAULT_POEMS,
  };

  function syncActiveTab() {
    const tab = state.tabs.find((t) => t.id === state.activeTabId);
    if (!tab) return;
    state.current = {
      page: tab.page,
      payload: tab.payload,
      address: tab.address,
    };
  }

  function setStatus(text: string) {
    statusEl.textContent = text;
  }

  function setCurrentToActiveTab(page: BrowserPage, payload?: any, address?: string) {
    const tab = state.tabs.find((t) => t.id === state.activeTabId);
    if (!tab) return;
    tab.page = page;
    tab.payload = payload;
    tab.address = address;
    tab.title = makeTabTitle(page, payload);
    state.current = { page, payload, address };
  }

  function pushHistory(page: BrowserPage, payload?: any, address?: string) {
    state.history = state.history.slice(0, state.historyIndex + 1);
    state.history.push({ page, payload, address });
    state.historyIndex = state.history.length - 1;
  }

  async function renderCurrentPage() {
    const { page, payload } = state.current;
    addressEl.value = renderAddress(state.current.address);

    if (page === 'home') {
      contentEl.innerHTML = renderHomePage();
      setStatus('Главная страница открыта');
    } else if (page === 'search') {
      contentEl.innerHTML = renderSearchPage(payload?.query ?? '');
      setStatus('Результаты поиска загружены');
    } else if (page === 'tatarwiki') {
      const article = (webContent.tatarwiki as any)[payload?.slug] || (webContent.tatarwiki as any)['shurale'];
      contentEl.innerHTML = `
        <div style="display:flex; min-height:100%;">
          <aside style="width:150px; border-right:1px solid #ccc; background:#f3f3f3; padding:10px; flex-shrink:0;">
            <div style="font-weight:bold; font-size:12px; margin-bottom:8px;">ТатарВики</div>
            ${Object.keys(webContent.tatarwiki).map(key => `
              <div class="tw-nav-item" data-open-page="tatarwiki" data-payload='${JSON.stringify({slug: key})}' style="cursor:pointer; font-size:11px; padding:4px; ${key === payload?.slug ? 'background:#d9e7ff' : ''}">
                ${(webContent.tatarwiki as any)[key].title}
              </div>
            `).join('')}
          </aside>
          <main style="flex:1; padding:15px; font-size:13px;">
            <h2 style="margin:0 0 5px 0;">${article.title}</h2>
            <div style="color:#666; font-size:11px; margin-bottom:10px;">${article.subtitle}</div>
            <div>${article.body}</div>
          </main>
        </div>
      `;
      setStatus('ТатарВики открыта');
    } else if (page === 'tukay') {
      contentEl.innerHTML = renderTukayIndex(state.poems);
      setStatus('Каталог стихов открыт');
    } else if (page === 'news') {
      contentEl.innerHTML = renderNewsPage();
      setStatus('Новости открыты');
    } else if (page === 'forum') {
      contentEl.innerHTML = renderForumPage();
      setStatus('Форум открыт');
      
      // Биндим клики по тредам
      contentEl.querySelectorAll('.forum-thread-link').forEach(el => {
        el.addEventListener('click', () => {
          const tid = (el as HTMLElement).dataset.threadId;
          openPage('thread', { threadId: tid });
        });
      });
    } else if (page === 'thread') {
      contentEl.innerHTML = renderThreadPage(payload?.threadId);
      setStatus('Просмотр темы');
    } else if (page === 'maps') {
      contentEl.innerHTML = renderMapsPage();
      setStatus('Карты открыты');
    } else if (page === 'poem') {
      const poem = (webContent.poems as any)[payload?.poemId] || (webContent.poems as any)['tugan-tel'];
      contentEl.innerHTML = `
        <div style="padding:15px;">
          <h3 style="margin:0;">${poem.title}</h3>
          <div style="font-size:11px; color:#666; margin-bottom:10px;">Год: ${poem.year}</div>
          <div style="font-family:'Times New Roman', serif; font-size:15px; line-height:1.6; white-space:pre-line;">
            ${poem.lines.join('\n')}
          </div>
        </div>
      `;
      setStatus(`Загружено: ${poem.title}`);
    }

    renderTabsUi();
    bindDynamicLinks();
  }

  function renderTabsUi() {
    tabsBarEl.innerHTML = renderTabs(state);

    tabsBarEl.querySelectorAll('.browser-tab').forEach((el) => {
      el.addEventListener('click', (e) => {
        const target = e.currentTarget as HTMLElement;
        const tabId = target.dataset.tabId;
        if (!tabId) return;
        state.activeTabId = tabId;
        syncActiveTab();
        renderCurrentPage();
      });
    });

    tabsBarEl.querySelectorAll('.browser-tab-close').forEach((el) => {
      el.addEventListener('click', (e) => {
        e.stopPropagation();
        const closeId = (e.currentTarget as HTMLElement).dataset.closeTabId;
        if (!closeId) return;
        if (state.tabs.length === 1) return;

        const idx = state.tabs.findIndex((t) => t.id === closeId);
        if (idx === -1) return;

        const wasActive = state.activeTabId === closeId;
        state.tabs.splice(idx, 1);

        if (wasActive) {
          const nextTab = state.tabs[Math.max(0, idx - 1)] ?? state.tabs[0];
          state.activeTabId = nextTab.id;
          syncActiveTab();
          renderCurrentPage();
        } else {
          renderTabsUi();
        }
      });
    });
  }

  function bindDynamicLinks() {
    contentEl.querySelectorAll('[data-open-page]').forEach((el) => {
      el.addEventListener('click', () => {
        const target = el as HTMLElement;
        const page = target.dataset.openPage as BrowserPage;
        const payloadRaw = target.dataset.payload;
        const address = target.dataset.address;
        const payload = payloadRaw ? JSON.parse(payloadRaw) : undefined;
        openPage(page, payload, address);
      });
    });
  }

  function openPage(page: BrowserPage, payload?: any, address?: string) {
    setCurrentToActiveTab(page, payload, address ?? inferAddress(page, payload));
    pushHistory(page, payload, address ?? inferAddress(page, payload));
    renderCurrentPage();
  }

  function inferAddress(page: BrowserPage, payload?: any): string {
    if (page === 'home') return 'about:home';
    if (page === 'search') return `search://${encodeURIComponent(payload?.query ?? '')}`;
    if (page === 'tatarwiki') return `tatarwiki://${payload?.slug ?? 'index'}`;
    if (page === 'tukay') return 'tukay://index';
    if (page === 'poem') return `tukay://${payload?.poemId ?? ''}`;
    if (page === 'news') return 'localnews://front';
    if (page === 'forum') return 'forum://threads';
    if (page === 'maps') return 'maps://local';
    return 'about:blank';
  }

  function goBack() {
    if (state.historyIndex <= 0) return;
    state.historyIndex -= 1;
    const item = state.history[state.historyIndex];
    setCurrentToActiveTab(item.page, item.payload, item.address);
    renderCurrentPage();
  }

  function goForward() {
    if (state.historyIndex >= state.history.length - 1) return;
    state.historyIndex += 1;
    const item = state.history[state.historyIndex];
    setCurrentToActiveTab(item.page, item.payload, item.address);
    renderCurrentPage();
  }

  function openAddress(raw: string) {
    const value = raw.trim();

    if (!value || value === 'about:home') {
      openPage('home', undefined, 'about:home');
      return;
    }

    if (value.startsWith('tatarwiki://')) {
      const slug = value.replace('tatarwiki://', '').trim() || 'shurale';
      openPage('tatarwiki', { slug }, value);
      return;
    }

    if (value.startsWith('tukay://')) {
      const poemId = value.replace('tukay://', '').trim();
      if (!poemId || poemId === 'index') {
        openPage('tukay', undefined, 'tukay://index');
      } else {
        openPage('poem', { poemId }, value);
      }
      return;
    }

    if (value.startsWith('search://')) {
      const query = decodeURIComponent(value.replace('search://', ''));
      openPage('search', { query }, value);
      return;
    }

    openPage('search', { query: value }, `search://${encodeURIComponent(value)}`);
  }

  function openNewTab(page: BrowserPage = 'home', payload?: any, address?: string) {
    const id = `tab-${Date.now()}-${Math.floor(Math.random() * 1000)}`;
    state.tabs.push({
      id,
      title: makeTabTitle(page, payload),
      page,
      payload,
      address: address ?? inferAddress(page, payload),
    });
    state.activeTabId = id;
    syncActiveTab();
    renderCurrentPage();
  }

  container.querySelector('.browser-back')?.addEventListener('click', goBack);
  container.querySelector('.browser-forward')?.addEventListener('click', goForward);
  container.querySelector('.browser-home')?.addEventListener('click', () => openPage('home'));
  container.querySelector('.browser-go')?.addEventListener('click', () => openAddress(addressEl.value));
  container.querySelector('.browser-search-btn')?.addEventListener('click', () => {
    openPage('search', { query: searchInputEl.value }, `search://${encodeURIComponent(searchInputEl.value)}`);
  });

  addressEl.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') openAddress(addressEl.value);
  });

  searchInputEl.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') {
      openPage('search', { query: searchInputEl.value }, `search://${encodeURIComponent(searchInputEl.value)}`);
    }
  });

  renderCurrentPage();
};
