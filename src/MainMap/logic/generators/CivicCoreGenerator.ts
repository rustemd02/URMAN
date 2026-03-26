import { AUTHORED_LAYOUT } from '../../config';
import { MapEntity, ReservedZone, Point } from '../../types';

/**
 * generateCivicCore - Places Mosque, Selsoviet, Club, and Clinic at hand-authored coordinates.
 */
export function generateCivicCore(anchors: {
    mosque: Point;
    club: Point;
    council: Point;
    clinic: Point;
}): {
    entities: MapEntity[];
    zones: ReservedZone[];
} {
    const entities: MapEntity[] = [];
    const zones: ReservedZone[] = [];

    const c = AUTHORED_LAYOUT.civic;

    // 1. Mosque
    entities.push({
        id: 'mosque', type: 'building', subType: 'mosque', name: 'Мечеть',
        gx: c.mosque.x, gy: c.mosque.y, gw: c.mosque.w, gh: c.mosque.h,
        style: { wallColor: '#f0f5f0', roofColor: '#1a5a1a', height: 45 },
        actions: [{ label: 'Войти', sceneTarget: 'mosque' }]
    });

    // 2. Selsoviet
    entities.push({
        id: 'council', type: 'building', subType: 'council', name: 'Сельсовет',
        gx: c.selsovet.x, gy: c.selsovet.y, gw: c.selsovet.w, gh: c.selsovet.h,
        style: { wallColor: '#d9c0a0', roofColor: '#5a2a1a', height: 28 }
    });

    // 3. Club
    entities.push({
        id: 'club', type: 'building', subType: 'club', name: 'Клуб',
        gx: c.club.x, gy: c.club.y, gw: c.club.w, gh: c.club.h,
        style: { wallColor: '#b0d0c0', roofColor: '#2a4a3a', height: 25 }
    });

    // 4. Clinic (MedPoint)
    entities.push({
        id: 'clinic', type: 'building', subType: 'clinic', name: 'Медпункт',
        gx: c.clinic.x, gy: c.clinic.y, gw: c.clinic.w, gh: c.clinic.h,
        style: { wallColor: '#f5f5f5', roofColor: '#a02020', height: 20 }
    });

    // Add zones
    Object.values(c).forEach((spot, i) => {
        zones.push({
            gx: spot.x - 1, gy: spot.y - 1, gw: spot.w + 2, gh: spot.h + 2,
            purpose: 'mosque', priority: 100 - i * 10
        });
    });

    return { entities, zones };
}
