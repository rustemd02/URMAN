
export interface ChatMessage {
    chatId: string;
    user: string;
    text: string;
    time: string;
    color?: string;
    unlocked: boolean;
    id?: string;
    trans?: {
        word: string;
        mean: string;
    };
}

export interface Contact {
    id: string;
    name: string;
    sub: string;
    icon: string;
}

export const CHAT_CONTACTS: Contact[] = [
    { id: 'selsovet', name: 'Сельсовет', sub: 'Общий чат (24 участника)', icon: '📁' },
    { id: 'alsu', name: 'Алсу', sub: 'Online', icon: '👤' },
    { id: 'babay', name: 'Бабай (Мансур)', sub: 'Online', icon: '👴' },
    { id: 'abi', name: 'Әби (Гөлсинә)', sub: 'Last seen yesterday', icon: '👵' },
    { id: 'family', name: 'Семья Шакировых', sub: 'Группа', icon: '🏘️' },
    { id: 'fanis', name: 'Фанис (Механик)', sub: 'Online', icon: '👨‍🔧' },
    { id: 'sushi', name: 'Суши 🍣 Казань', sub: 'Бот-доставка', icon: '🍱' },
    { id: 'unknown', name: '+7 (9xx) xxx-xx-77', sub: 'Away', icon: '❓' }
];

export const CHAT_MESSAGES: ChatMessage[] = [
    // --- СЕЛЬСОВЕТ (Юмор и мемы) ---
    {
        chatId: 'selsovet',
        user: 'IldarPole',
        text: 'Кем басуга трактор калдырган?! Чүп үләннәре арасында күренми дә!',
        time: '08:20',
        unlocked: true,
        trans: { word: 'басуга', mean: 'в поле' }
    },
    {
        chatId: 'selsovet',
        user: 'Babay77',
        text: 'Ильдар, ул трактор түгел, ул минем заборның бер өлеше. Кисәк кенә очып китте.',
        time: '09:12',
        unlocked: true
    },
    {
        chatId: 'selsovet',
        user: 'Fanis_VAZ',
        text: 'Забор очса — димәк Шүрәле белән кәрт уйнагансыз))',
        time: '10:05',
        unlocked: true,
        trans: { word: 'кәрт', mean: 'карты' }
    },
    {
        chatId: 'selsovet',
        user: 'Zarifa_Tatar',
        text: 'Фанис, «кәрт» түгел, «карта» дип әйтергә кирәк. Һәм тыныш билгеләрен онытмагыз!',
        time: '10:30',
        unlocked: true
    },
    {
        chatId: 'selsovet',
        user: 'Salakh_Guard',
        text: 'Кемнең сыеры минем будка янында йоклый?! Карат өрә-өрә тавышы калмады инде.',
        time: '11:15',
        unlocked: true,
        trans: { word: 'сыеры', mean: 'корова' }
    },
    {
        chatId: 'selsovet',
        user: 'RashidGarage',
        text: 'Салах, ул сыер түгел, ул Фанисның яңа «Нива»сы, просто төсе ошаган.',
        time: '11:20',
        unlocked: true
    },
    {
        chatId: 'selsovet',
        user: 'Fanis_VAZ',
        text: 'Рашид, шаяртырга Казанга бар! Минем машина — легенда!',
        time: '11:25',
        unlocked: true
    },

    // --- БАБАЙ (Пицца и тех-фейл) ---
    {
        chatId: 'babay',
        user: 'Babay77',
        text: 'Айдар, улым, бу компьютердан ничек пицца заказ бирергә? Төймәгә басам — экран сүнә.',
        time: '14:20',
        unlocked: true,
        trans: { word: 'төймәгә', mean: 'на кнопку' }
    },
    {
        chatId: 'babay',
        user: 'Babay77',
        text: 'Казанда «Додо» дигәннәр, алар Кырлайга китерәләрме? Бабайның ашыйсы килә.',
        time: '14:22',
        unlocked: true
    },
    {
        chatId: 'babay',
        user: 'Babay77',
        text: 'Әбинең эчпочмаклары арып китте инде. Яңалык кирәк.',
        time: '14:25',
        unlocked: true
    },

    // --- АЛСУ (Романтика и мистика) ---
    {
        chatId: 'alsu',
        user: 'alsu_k',
        text: 'Айдар, син бүген зират янында булдыңмы? Күләгәңне күрдем кебек...',
        time: '23:05',
        unlocked: true,
        trans: { word: 'зират', mean: 'кладбище' }
    },
    {
        chatId: 'alsu',
        user: 'alsu_k',
        text: 'Урман сине күзәтә. Сак бул. Төшләреңдә мине күрсәң — ышанма.',
        time: '23:10',
        unlocked: true,
        trans: { word: 'күзәтә', mean: 'наблюдает' }
    },
    {
        chatId: 'alsu',
        user: 'alsu_k',
        text: 'Документларда ялгыш даталар була. Барысына да ышанма.',
        time: '01:15',
        unlocked: false,
        id: 'alsu_false_date_warning'
    },

    // --- СЕМЬЯ (Хаос) ---
    {
        chatId: 'family',
        user: 'Gulsina_Abi',
        text: '[Голосовое сообщение 0:45]: «Айдар, бәбкәм, кайттыгызмы? Чәй кайнады, бабай пицца көтеп утыра, жинни...»',
        time: '18:00',
        unlocked: true,
        trans: { word: 'бәбкәм', mean: 'детка/гусеночек (ласк.)' }
    },
    {
        chatId: 'family',
        user: 'Babay77',
        text: 'Гөлсинә, монда язма, мин пиццаны интернеттан үзем алам!',
        time: '18:05',
        unlocked: true
    },

    // --- ФАНИС (Советы по ВАЗ) ---
    {
        chatId: 'fanis',
        user: 'Fanis_VAZ',
        text: 'Карбюраторны чистартырга кирәк булса — кил. Тик бензин исе белән кайтсаң, әби тиркәр.',
        time: '11:00',
        unlocked: true,
        trans: { word: 'тиркәр', mean: 'будет ругать' }
    },

    // --- СУШИ (Бот) ---
    {
        chatId: 'sushi',
        user: 'SushiBot',
        text: 'Заказ успешно принят! Доставка в Кырлай... ОШИБКА: расстояние более 150 км. Попробуйте наш филиал в Арске.',
        time: '14:40',
        unlocked: true
    },

    {
        chatId: 'unknown',
        user: 'Unknown',
        text: 'Урман эчендә елаган тавыш ишеттеңме? Бу җил түгел.',
        time: '03:33',
        unlocked: false,
        id: 'forest_cry_event',
        trans: { word: 'елаган', mean: 'плачущий' }
    },
    {
        chatId: 'family',
        user: 'Babay77',
        text: 'Айдар, бәрәңге бакчасында нәрсәдер кыштырдый. Эткә охшамаган. Чыгып кара әле.',
        time: '22:15',
        unlocked: true,
        trans: { word: 'кыштырдый', mean: 'шорошит' }
    },
    {
        chatId: 'selsovet',
        user: 'Rushania_Med',
        text: 'Дискуссияне туктатыгыз! Кем эчпочмакка рецепт сораган иде? Личкага языгыз.',
        time: '12:00',
        unlocked: true
    }
];
