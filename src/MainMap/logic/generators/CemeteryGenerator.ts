import { AUTHORED_LAYOUT } from '../../config';
import { MapEntity, ReservedZone } from '../../types';

/**
 * generateCemetery - Places the 'Zirat' (village cemetery) atauthored coordinates.
 * Moved to the village side (south of the river) as per new authored layout.
 */
export function generateCemetery(): {
    entity: MapEntity;
    zone: ReservedZone;
} {
    const { x, y, w, h } = AUTHORED_LAYOUT.zirat;

    const entity: MapEntity = {
        id: 'zirat',
        type: 'special',
        subType: 'zirat',
        name: 'Зират',
        desc: 'Старое сельское кладбище. Тихий шелест травы и запах полыни.',
        gx: x, gy: y, gw: w, gh: h,
        actions: [{ label: 'Зайти', sceneTarget: 'zirat' }]
    };

    const zone: ReservedZone = {
        gx: x - 1,
        gy: y - 1,
        gw: w + 2,
        gh: h + 2,
        purpose: 'cemetery',
        priority: 90
    };

    return { entity, zone };
}
