import { MapEntity } from './types';

/**
 * Продвинутая изометрическая сортировка.
 * Решает проблему перекрытия (Z-sorting) для объектов разного размера.
 */
export function sortIsometricEntities(entities: MapEntity[]): MapEntity[] {
    return [...entities].sort((a, b) => {
        // 1. Проверка по "дальней" кромке объекта (Y + Глубина)
        // Это база: кто дальше вглубь экрана, тот рисуется первым.
        const aFarY = a.gy + (a.gd || 1);
        const bFarY = b.gy + (b.gd || 1);

        if (aFarY !== bFarY) {
            return aFarY - bFarY;
        }

        // 2. Проверка по "дальней" кромке X (Лево/Право)
        const aFarX = a.gx + (a.gw || 1);
        const bFarX = b.gx + (b.gw || 1);

        if (aFarX !== bFarX) {
            return aFarX - bFarX;
        }

        // 3. Учет вертикального уровня (Z-index/gz)
        // Объекты на втором этаже всегда выше тех, что на земле.
        const aZ = a.gz || 0;
        const bZ = b.gz || 0;

        if (aZ !== bZ) {
            return aZ - bZ;
        }

        // 4. Финальный штрих: высота самого спрайта (gh)
        // Если все остальное совпало, более высокий объект рисуем позже.
        return (a.gh || 0) - (b.gh || 0);
    });
}