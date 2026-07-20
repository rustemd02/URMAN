export type FileType = 'app' | 'folder' | 'doc' | 'image';

export interface SystemItem {
    id: string;
    appId: string;
    name: string;
    type: FileType;
    icon: string;
    parentId: string | null;
    content?: string | string[];
    launchSection?: string;
}

const ARCHIVE_APP_ID = 'urman.oldpc:app/archive_search';

/** Launcher data is declarative; DedOS resolves `appId` through exact descriptors. */
export const SYSTEM_REGISTRY: readonly SystemItem[] = [
    { id: 'urman.oldpc:launcher/archive-search', appId: ARCHIVE_APP_ID, name: 'Архивный поиск', type: 'app', icon: '🔎', parentId: null, launchSection: 'archive_search' },
    { id: 'urman.oldpc:launcher/documents-marat', appId: ARCHIVE_APP_ID, name: 'Документы Марата', type: 'app', icon: '📁', parentId: null, launchSection: 'documents_marat' },
    { id: 'urman.oldpc:launcher/archive', appId: ARCHIVE_APP_ID, name: 'Кырлай архив', type: 'app', icon: 'К', parentId: null, launchSection: 'archive_search' },
    { id: 'urman.oldpc:launcher/tatarwiki', appId: ARCHIVE_APP_ID, name: 'Татарвики', type: 'app', icon: '📚', parentId: null, launchSection: 'tatarwiki' },
    { id: 'urman.oldpc:launcher/saved-messages', appId: ARCHIVE_APP_ID, name: 'Сохранённые сообщения', type: 'app', icon: '💬', parentId: null, launchSection: 'saved_messages' },
    { id: 'urman.oldpc:launcher/internal-accounting', appId: ARCHIVE_APP_ID, name: 'Учёт Кырлая', type: 'app', icon: '📇', parentId: null, launchSection: 'internal_accounting' },
    { id: 'urman.oldpc:launcher/damaged-hidden', appId: ARCHIVE_APP_ID, name: 'Повреждённые файлы', type: 'app', icon: '▣', parentId: null, launchSection: 'damaged_hidden' },
    { id: 'urman.oldpc:launcher/notepad', appId: 'urman.oldpc:app/notepad', name: 'Заметки Айдара', type: 'app', icon: '📝', parentId: null },
    { id: 'urman.oldpc:launcher/player', appId: 'urman.oldpc:app/player', name: 'Проигрыватель', type: 'app', icon: '📻', parentId: null },
    { id: 'urman.oldpc:launcher/terminal', appId: 'urman.oldpc:app/terminal', name: 'Командная строка', type: 'app', icon: '💻', parentId: null },
    { id: 'urman.oldpc:launcher/calendar', appId: 'urman.oldpc:app/calendar', name: 'Календарь', type: 'app', icon: '⏳', parentId: null },
    { id: 'urman.oldpc:launcher/task-manager', appId: 'urman.oldpc:app/task-manager', name: 'Состояние', type: 'app', icon: '📋', parentId: null },
    { id: 'urman.oldpc:launcher/exit', appId: 'urman.oldpc:app/exit', name: 'Отойти от компьютера', type: 'app', icon: '🚪', parentId: null },
    { id: 'urman.oldpc:launcher/registry-folder', appId: ARCHIVE_APP_ID, name: 'Реестр домов', type: 'app', icon: '📁', parentId: null, launchSection: 'household_registry' },
    { id: 'urman.oldpc:launcher/compensation-folder', appId: ARCHIVE_APP_ID, name: 'Компенсации', type: 'app', icon: '📁', parentId: null, launchSection: 'violations_compensation' },
    { id: 'urman.oldpc:launcher/kara-urman-folder', appId: ARCHIVE_APP_ID, name: 'Кара-Урман', type: 'app', icon: '📁', parentId: null, launchSection: 'kara_urman' },
    { id: 'urman.oldpc:launcher/household-folder', appId: ARCHIVE_APP_ID, name: 'Хозяйство', type: 'app', icon: '📁', parentId: null, launchSection: 'household_misc' },
    { id: 'urman.oldpc:launcher/trash', appId: 'urman.oldpc:app/trash', name: 'Чүплек', type: 'app', icon: '🗑️', parentId: null },
];
