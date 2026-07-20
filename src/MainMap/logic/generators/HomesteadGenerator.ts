import { AUTHORED_LAYOUT } from '../../config';
import { MapCell, MapEntity, ReservedZone, Point, MainMapPresentationLabels, MainMapResidentBinding } from '../../types';

/**
 * generateHomesteads - Places residential lots for identified characters.
 */
export function generateHomesteads(
    _map: MapCell[][], 
    _roadSpine: readonly Point[],
    _activeZones: ReservedZone[],
    residents: readonly MainMapResidentBinding[],
    labels: MainMapPresentationLabels,
): {
    entities: MapEntity[];
    houseZones: ReservedZone[];
} {
    const entities: MapEntity[] = [];
    const houseZones: ReservedZone[] = [];

    // Property dimensions (Lot area)
    const lw = 6;
    const lh = 8;

    const residentByLot = new Map(residents.map((resident) => [resident.lotIndex, resident]));

    AUTHORED_LAYOUT.lots.forEach((anchor, i) => {
        const [gx, gy] = anchor;
        const variant = (gx + gy * 7) % 4;
        const resident = residentByLot.get(i);
        const isPrimaryResidence = resident?.isPrimaryResidence === true;
        const houseName = resident?.label ?? `${labels.unnamedResidencePrefix} ${i + 1}`;

        // 1. The Main House (Smaller: 2x2 or 2x3)
        const houseId = resident?.entityId ?? `greybox-house-${i}`;
        const curGw = isPrimaryResidence ? 4 : ((variant % 2 === 0) ? 2 : 3);
        const curGh = isPrimaryResidence ? 3 : 2;

        entities.push({
            id: houseId,
            type: 'building',
            subType: 'village_house',
            name: houseName,
            gx, gy, gw: curGw, gh: curGh,
            style: {
                wallColor: isPrimaryResidence ? '#5a4a2a' : (['#c9a050', '#8eb8b0', '#a07040', '#9c9c9c'][variant]),
                roofColor: isPrimaryResidence ? '#7a3a3a' : (['#3a6b3a', '#7a3a3a', '#1a3a1a', '#4a4a4a'][variant]),
                height: isPrimaryResidence ? 30 : (20 + (variant * 2))
            },
            actions: resident?.actions ?? []
        });

        // 2. Secondary Buildings - back of the lot
        entities.push({
            id: `bath_${i}`,
            type: 'building',
            subType: 'bathhouse',
            name: labels.bathhouse,
            gx: gx, gy: gy + lh - 3, gw: 1.5, gh: 1.5,
            style: { wallColor: '#5a4a35', roofColor: '#3a3a3a', height: 10 }
        });

        entities.push({
            id: `shed_${i}`,
            type: 'building',
            subType: 'shed',
            name: labels.shed,
            gx: gx + 2, gy: gy + lh - 3, gw: 1.5, gh: 2,
            style: { wallColor: '#4a3a25', roofColor: '#2a2a2a', height: 12 }
        });

        // 3. Fences (Property Lot boundary)
        entities.push({
            id: `fence_${i}`,
            type: 'prop',
            subType: 'fence',
            gx: gx - 1, gy: gy - 1, gw: lw, gh: lh,
            style: { wallColor: '#5a4a35' }
        });

        houseZones.push({
            gx: gx - 1, gy: gy - 1, 
            gw: lw, gh: lh,
            purpose: 'house',
            priority: 50
        });
    });

    return { entities, houseZones };
}
