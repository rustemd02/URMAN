import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const { chromium } = require('/Users/unterlantas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');

const baseUrl = process.argv[2] ?? 'http://127.0.0.1:5174/';
const chromePath = process.env.CHROME_PATH ?? '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome';

const result = {
    baseUrl,
    steps: [],
    console: [],
    snapshot: {},
    ok: false,
};

const note = (name, data = {}) => result.steps.push({ name, ...data });

const browser = await chromium.launch({
    headless: true,
    executablePath: chromePath,
    args: ['--no-sandbox', '--disable-crash-reporter', '--disable-breakpad'],
});

try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    page.on('console', (message) => result.console.push(`${message.type()}: ${message.text()}`));
    page.on('pageerror', (error) => result.console.push(`pageerror: ${error.message}`));
    page.on('requestfailed', (request) => result.console.push(`requestfailed: ${request.url()} ${request.failure()?.errorText ?? ''}`));
    page.on('response', (response) => {
        if (response.status() >= 400) result.console.push(`http ${response.status()}: ${response.url()}`);
    });

    const clickText = async (text, timeout = 10000) => {
        await page.getByText(text, { exact: false }).first().click({ timeout });
    };
    const waitSceneText = async (text) => {
        await page.getByText(text, { exact: false }).first().waitFor({ timeout: 15000 });
    };
    const clickRoute = async (label) => {
        await page.locator('.route-control', { hasText: label }).first().click({ timeout: 12000 });
        await page.waitForTimeout(650);
    };
    const inspectAndClick = async (label) => {
        await clickRoute('Осмотреть');
        await clickText(label);
        await page.waitForTimeout(650);
    };

    await page.goto(baseUrl, { waitUntil: 'networkidle' });
    await page.evaluate(() => localStorage.clear());
    await page.reload({ waitUntil: 'networkidle' });
    note('menu loaded', { hasTitle: await page.getByText('URMAN').first().isVisible() });

    await clickText('Начать игру');
    await page.locator('#buy-btn').click({ timeout: 15000 });
    await waitSceneText('Дорога в Кырлай');
    note('menu -> intro -> route');

    await clickRoute('Вперёд');
    await clickRoute('Направо');
    await clickRoute('Вперёд');
    await clickText('Войти в дом');
    await waitSceneText('Дом Мансура');
    note('route -> house');

    await clickText('Сесть за компьютер');
    await page.locator('.desktop-icon[data-id="archive_search"]').dblclick({ timeout: 12000 });
    await page.locator('.oldpc-result[data-item-id="doc_marat_official_death_notice"]').click({ timeout: 12000 });
    await page.locator('[data-save-clue="doc_marat_official_death_notice"]').click({ timeout: 12000 });
    note('old PC official death clue saved');

    await page.locator('.oldpc-search-input').fill('реестр');
    await page.locator('.oldpc-search-btn').click();
    await page.locator('.oldpc-result[data-item-id="rec_marat_case_register_conflict"]').click({ timeout: 12000 });
    await page.locator('[data-save-clue="rec_marat_case_register_conflict"]').click({ timeout: 12000 });
    note('old PC contradiction saved');

    await page.locator('.oldpc-search-input').fill('Шүрәле');
    await page.locator('.oldpc-search-btn').click();
    await page.locator('.oldpc-result[data-item-id="tw_shurale_urman_boundary"]').click({ timeout: 12000 });
    await page.locator('[data-save-clue="tw_shurale_urman_boundary"]').click({ timeout: 12000 });
    note('vocabulary/re-read source opened');

    await page.locator('#win-archive_search .close-btn').click({ timeout: 12000 });
    await page.locator('.desktop-icon[data-id="village"]').dblclick({ timeout: 12000 });
    await waitSceneText('Поворот к дому Мансура');
    note('PC -> route');

    await page.locator('#inventory-toggle').click({ timeout: 12000 });
    await page.locator('.inventory-item[title*="Блокнот"]').click({ timeout: 12000 });
    await waitSceneText('Журнал Айдара');
    const journalText = await page.locator('#notebook-ui .journal-shell').innerText();
    note('journal visible', {
        hasOfficial: journalText.includes('Справка о смерти') || journalText.includes('официаль'),
        hasContradiction: journalText.includes('противореч') || journalText.includes('граница'),
        hasVocabulary: journalText.includes('урман') || journalText.includes('Шүрәле'),
    });
    await page.locator('.journal-close').click();

    await clickRoute('Осмотреть');
    await clickText('Спросить про “граница / ответил”');
    await waitSceneText('Где ты это видел');
    note('Rinat reacts to contradiction');
    await clickText('Вернуться к дороге');

    await clickRoute('Назад');
    await inspectAndClick('Оглянуться на окна');
    await clickRoute('Вперёд');
    await clickRoute('Вперёд');
    await inspectAndClick('Сверить дату');
    await clickRoute('Вперёд');
    await clickRoute('Вперёд');
    await inspectAndClick('Не отвечать сразу');
    await inspectAndClick('Остаться у леса');
    await clickText('Остаться у леса');
    await waitSceneText('Лес сначала звучит');
    await clickText('Прислушаться');
    await waitSceneText('Айдар');
    await clickText('Сделать шаг к голосу');
    await waitSceneText('Ответ почти готов');
    await clickText('Ответить');
    await waitSceneText('Не отвечай');
    await waitSceneText('Конец MVP-среза');
    note('route -> Kara-Urman cliffhanger');

    const stateBeforeReload = await page.evaluate(() => ({
        scene: window.URMAN.state.currentScene,
        routeCurrentNodeId: window.URMAN.state.routeCurrentNodeId,
        knownKeys: window.URMAN.state.knownKeys,
        savedEvidenceIds: window.URMAN.state.savedEvidenceIds,
        vocabularyStates: window.URMAN.state.vocabularyStates,
        pressureLevel: window.URMAN.state.pressureLevel,
        pressureFlags: window.URMAN.state.pressureFlags,
        completedBeats: window.URMAN.state.completedBeats,
        npcStates: window.URMAN.state.npcStates,
    }));
    await page.evaluate(() => window.URMAN.saveSystem.save());
    await page.reload({ waitUntil: 'networkidle' });
    await page.waitForTimeout(1000);
    if (await page.getByText('Пропустить вступление', { exact: false }).first().isVisible().catch(() => false)) {
        await clickText('Пропустить вступление');
        await page.waitForTimeout(1000);
    }
    const stateAfterReload = await page.evaluate(() => ({
        routeCurrentNodeId: window.URMAN.state.routeCurrentNodeId,
        knownKeys: window.URMAN.state.knownKeys,
        savedEvidenceIds: window.URMAN.state.savedEvidenceIds,
        vocabularyStates: window.URMAN.state.vocabularyStates,
        pressureLevel: window.URMAN.state.pressureLevel,
        pressureFlags: window.URMAN.state.pressureFlags,
        completedBeats: window.URMAN.state.completedBeats,
        npcStates: window.URMAN.state.npcStates,
    }));
    note('save/load checked');

    await page.screenshot({ path: '/private/tmp/urman-mvp-smoke.png', fullPage: false });
    result.snapshot = { stateBeforeReload, stateAfterReload, screenshot: '/private/tmp/urman-mvp-smoke.png' };
    result.ok = true;
} finally {
    await browser.close();
}

console.log(JSON.stringify(result, null, 2));
