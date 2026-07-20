import { AUTHORED_LAYOUT } from '../../config';
import { MainMapLandmarkBinding, MapEntity, ReservedZone, Point } from '../../types';

/**
 * generateCivicCore - Places Mosque, Selsoviet, Club, and Clinic at hand-authored coordinates.
 */
export function generateCivicCore(_anchors: {
    mosque: Point;
    club: Point;
    council: Point;
    clinic: Point;
}, landmarkBindings: readonly MainMapLandmarkBinding[]): {
    entities: MapEntity[];
    zones: ReservedZone[];
} {
    const entities: MapEntity[] = [];
    const zones: ReservedZone[] = [];

    const c = AUTHORED_LAYOUT.civic;
    const landmarks = new Map(landmarkBindings.map((binding) => [binding.slot, binding]));
    const required = (slot: 'mosque' | 'council' | 'club' | 'clinic') => {
        const binding = landmarks.get(slot);
        if (!binding) throw new Error(`MainMap dev config is missing ${slot} landmark binding.`);
        return binding;
    };
    const mosque = required('mosque');
    const council = required('council');
    const club = required('club');
    const clinic = required('clinic');

    // 1. Mosque
    entities.push({
        id: mosque.entityId, type: 'building', subType: 'mosque', name: mosque.label,
        gx: c.mosque.x, gy: c.mosque.y, gw: c.mosque.w, gh: c.mosque.h,
        style: { wallColor: '#f0f5f0', roofColor: '#1a5a1a', height: 45 },
        actions: mosque.actions
    });

    // 2. Selsoviet
    entities.push({
        id: council.entityId, type: 'building', subType: 'council', name: council.label,
        gx: c.selsovet.x, gy: c.selsovet.y, gw: c.selsovet.w, gh: c.selsovet.h,
        style: { wallColor: '#d9c0a0', roofColor: '#5a2a1a', height: 28 },
        actions: council.actions
    });

    // 3. Club
    entities.push({
        id: club.entityId, type: 'building', subType: 'club', name: club.label,
        gx: c.club.x, gy: c.club.y, gw: c.club.w, gh: c.club.h,
        style: { wallColor: '#b0d0c0', roofColor: '#2a4a3a', height: 25 },
        actions: club.actions
    });

    // 4. Clinic (MedPoint)
    entities.push({
        id: clinic.entityId, type: 'building', subType: 'clinic', name: clinic.label,
        gx: c.clinic.x, gy: c.clinic.y, gw: c.clinic.w, gh: c.clinic.h,
        style: { wallColor: '#f5f5f5', roofColor: '#a02020', height: 20 },
        actions: clinic.actions
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
