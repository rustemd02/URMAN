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
    { id: 'notepad', name: 'Блокнот', type: 'app', icon: '📝', parentId: null },
    { id: 'player', name: 'Winamp', type: 'app', icon: '📻', parentId: null },
    { id: 'terminal', name: 'MS-DOS', type: 'app', icon: '💻', parentId: null },
    
    // Folders
    { id: 'my_docs', name: 'Документлар', type: 'folder', icon: '📁', parentId: null, content: ['pact_1999', 'missing_report'] },
    { id: 'photos', name: 'Фото_Архив', type: 'folder', icon: '📁', parentId: null, content: ['photo_forest', 'photo_old_man'] },
    
    // Files
    { id: 'pact_1999', name: 'ПАКТ_1999.txt', type: 'doc', icon: '📄', parentId: 'my_docs', content: 'Мы, жители деревни, обязуемся не заходить в северный квадрат после заката. Взамен Урман дает нам воду и грибы. Нарушение пакта карается забвением.' },
    { id: 'missing_report', name: 'ОТЧЕТ_ПРОПАВШИЕ.txt', type: 'doc', icon: '📄', parentId: 'my_docs', content: '2024-05-12: Пропал Айдар. Последний раз видели у старого моста. Следов борьбы нет. Только запах хвои и старой бумаги.' },
    { id: 'photo_forest', name: 'лес_ночь.jpg', type: 'image', icon: '🖼️', parentId: 'photos', content: '/assets/photo_forest.jpg' },
    { id: 'photo_old_man', name: 'дед_гаяз.jpg', type: 'image', icon: '🖼️', parentId: 'photos', content: '/assets/photo_old_man.jpg' },
    
    { id: 'trash', name: 'Чүплек', type: 'app', icon: '🗑️', parentId: null }
];
