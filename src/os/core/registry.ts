export type FileType = 'app' | 'folder' | 'doc' | 'image';

export interface SystemItem {
    id: string;
    name: string;
    type: FileType;
    icon: string;
    parentId: string | null;
    content?: string | string[]; // For docs (text) or folders (list of IDs)
}

export const SYSTEM_REGISTRY: SystemItem[] = [
    // Apps
    { id: 'chat', name: 'ICQ (Сельсовет)', type: 'app', icon: '💬', parentId: null },
    { id: 'browser', name: 'Яндэк 98', type: 'app', icon: '🌐', parentId: null },
    { id: 'minesweeper', name: 'Тетрис (Урман)', type: 'app', icon: '🧩', parentId: null },
    { id: 'notepad', name: 'Заметки Айдара', type: 'app', icon: '📝', parentId: null },
    { id: 'player', name: 'Winamp', type: 'app', icon: '📻', parentId: null },
    { id: 'terminal', name: 'MS-DOS', type: 'app', icon: '💻', parentId: null },
    
    // Folders
    { id: 'my_docs', name: 'Документлар', type: 'folder', icon: '📁', parentId: null, content: ['pact_1999', 'forest_plan', 'missing_report'] },
    { id: 'photos', name: 'Фото_Архив', type: 'folder', icon: '📁', parentId: null, content: ['photo_forest', 'photo_old_man'] },
    
    // Files
    { id: 'pact_1999', name: 'ПАКТ_1999.doc', type: 'doc', icon: '📄', parentId: 'my_docs', content: 'ДОГОВОР (ПАКТ) ОТ 1999 ГОДА\n\nМы, нижеподписавшиеся, подтверждаем границы Урмана. \nЛес не заходит в деревню, люди не заходят вглубь после заката.\n\nНарушение Пакта ведет к пробуждению Хозяев.\n\n(Текст обрывается, видны следы когтей)' },
    { id: 'forest_plan', name: 'ПЛАН_ВЫРУБКИ.doc', type: 'doc', icon: '📄', parentId: 'my_docs', content: 'ОБЪЕКТ: Участок 44-Б (Старый Лес)\nСТАТУС: Одобрено к вырубке.\nИНВЕСТОР: ООО "Казань-Строй-Инвест"\nПРИМЕЧАНИЕ: Гаяз абый подтвердил, что "проблем с местными не будет". Начать работы в июле.' },
    { id: 'missing_report', name: 'ОТЧЕТ_ПРОПАВШИЕ.txt', type: 'doc', icon: '📄', parentId: 'my_docs', content: '2024-05-12: Пропал Айдар (другой). Последний раз видели у старого моста. Следов борьбы нет. Только запах хвои и старой бумаги.' },
    { id: 'photo_forest', name: 'лес_ночь.jpg', type: 'image', icon: '🖼️', parentId: 'photos', content: '/assets/photo_forest.jpg' },
    { id: 'photo_old_man', name: 'дед_гаяз.jpg', type: 'image', icon: '🖼️', parentId: 'photos', content: '/assets/photo_old_man.jpg' },
    
    { id: 'trash', name: 'Чүплек', type: 'app', icon: '🗑️', parentId: null }
];
