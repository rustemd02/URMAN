import { AUTHORED_LAYOUT } from '../../config';
import { MapCell, MapEntity, ReservedZone, Point } from '../../types';

/**
 * generateHomesteads - Places 15 authored residential lots.
 * Each lot includes a house, fence, gate, bathhouse, and shed.
 */
export function generateHomesteads(
    _map: MapCell[][], 
    _roadSpine: readonly Point[],
    _activeZones: ReservedZone[]
): {
    entities: MapEntity[];
    houseZones: ReservedZone[];
} {
    const entities: MapEntity[] = [];
    const houseZones: ReservedZone[] = [];

    AUTHORED_LAYOUT.lots.forEach((anchor, i) => {
        const [gx, gy] = anchor;
        const variant = (gx + gy * 7) % 4;

        // 1. The Main House
        const houseId = `house_${i}`;
        const curGw = (variant % 2 === 0) ? 4 : 3;
        const curGh = (variant % 2 === 0) ? 3 : 4;

        entities.push({
            id: houseId,
            type: 'building',
            subType: 'village_house',
            name: `Дом #${i + 1}`,
            gx, gy, gw: curGw, gh: curGh,
            style: {
                wallColor: ['#c9a050', '#8eb8b0', '#a07040', '#9c9c9c'][variant],
                roofColor: ['#3a6b3a', '#7a3a3a', '#1a3a1a', '#4a4a4a'][variant],
                height: 20 + (variant * 2)
            },
            actions: [{ label: 'Войти', sceneTarget: 'computer' }]
        });

        // 2. Secondary Buildings (Dressing)
        // Bathhouse (Banya) - typically smaller, in the back
        entities.push({
            id: `bath_${i}`,
            type: 'building',
            subType: 'bathhouse',
            name: 'Баня',
            gx: gx + 1, gy: gy + curGh + 1, gw: 2, gh: 2,
            style: { wallColor: '#5a4a35', roofColor: '#3a3a3a', height: 12 }
        });

        // Shed (Saray)
        entities.push({
            id: `shed_${i}`,
            type: 'building',
            subType: 'shed',
            name: 'Сарай',
            gx: gx + curGw + 1, gy: gy, gw: 2, gh: 3,
            style: { wallColor: '#4a3a25', roofColor: '#2a2a2a', height: 14 }
        });

        // 3. Fence & Gate (Placeholder props)
        // We'll just define the zone for now, as props need specific renderer support
        houseZones.push({
            gx: gx - 2, gy: gy - 2, 
            gw: curGw + 6, gh: curGh + 6,
            purpose: 'house',
            priority: 50
        });
    });

    return { entities, houseZones };
}
