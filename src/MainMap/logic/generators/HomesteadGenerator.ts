import { AUTHORED_LAYOUT } from '../../config';
import { MapCell, MapEntity, ReservedZone, Point } from '../../types';
import { CHARACTERS } from '../../../data/characters';

/**
 * generateHomesteads - Places residential lots for identified characters.
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

    // Property dimensions (Lot area)
    const lw = 6;
    const lh = 8;

    // Ordered list of residents for the 15 lots
    const residents = [
        'babay', 'fanis', 'zarifa', 'ildar', 'gulnara', 
        'karat_guard', 'rashid', 'nail', 'rushania', 'razilya',
        'alsu', 'neighbor_1', 'neighbor_2', 'neighbor_3', 'neighbor_4'
    ];

    AUTHORED_LAYOUT.lots.forEach((anchor, i) => {
        const [gx, gy] = anchor;
        const variant = (gx + gy * 7) % 4;
        const residentId = residents[i];
        const character = CHARACTERS[residentId];
        
        const isMainHouse = residentId === 'babay';
        const houseName = isMainHouse ? 'Дом Әби и Бабая' : (character ? `Дом: ${character.fullName}` : `Дом #${i + 1}`);

        // 1. The Main House (Smaller: 2x2 or 2x3)
        const houseId = `house_${residentId || i}`;
        const curGw = isMainHouse ? 4 : ((variant % 2 === 0) ? 2 : 3);
        const curGh = isMainHouse ? 3 : 2;

        const actions: { label: string; sceneTarget?: string }[] = [];
        if (isMainHouse) {
            actions.push(
                { label: 'Поговорить с Бабаем', sceneTarget: 'dialogue_babay' },
                { label: 'Поговорить с Әби', sceneTarget: 'dialogue_abi' },
                { label: 'Компьютер Бабая', sceneTarget: 'computer' }
            );
        } else if (character) {
            actions.push({ label: `Поговорить с: ${character.fullName.split(' ')[0]}`, sceneTarget: `dialogue_${residentId}` });
        } else {
            actions.push({ label: 'Постучать', sceneTarget: 'knock_door' });
        }

        entities.push({
            id: houseId,
            type: 'building',
            subType: 'village_house',
            name: houseName,
            gx, gy, gw: curGw, gh: curGh,
            style: {
                wallColor: isMainHouse ? '#5a4a2a' : (['#c9a050', '#8eb8b0', '#a07040', '#9c9c9c'][variant]),
                roofColor: isMainHouse ? '#7a3a3a' : (['#3a6b3a', '#7a3a3a', '#1a3a1a', '#4a4a4a'][variant]),
                height: isMainHouse ? 30 : (20 + (variant * 2))
            },
            actions
        });

        // 2. Secondary Buildings - back of the lot
        entities.push({
            id: `bath_${i}`,
            type: 'building',
            subType: 'bathhouse',
            name: 'Баня',
            gx: gx, gy: gy + lh - 3, gw: 1.5, gh: 1.5,
            style: { wallColor: '#5a4a35', roofColor: '#3a3a3a', height: 10 }
        });

        entities.push({
            id: `shed_${i}`,
            type: 'building',
            subType: 'shed',
            name: 'Сарай',
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
