import { AUTHORED_LAYOUT } from '../../config';
import { MainMapLandmarkBinding, MapEntity, ReservedZone } from '../../types';

/**
 * generateCemetery - Places the 'Zirat' (village cemetery) atauthored coordinates.
 * Moved to the village side (south of the river) as per new authored layout.
 */
export function generateCemetery(landmarkBindings: readonly MainMapLandmarkBinding[]): {
    entity: MapEntity;
    zone: ReservedZone;
} {
    const { x, y, w, h } = AUTHORED_LAYOUT.zirat;
    const binding = landmarkBindings.find((candidate) => candidate.slot === 'zirat');
    if (!binding) throw new Error('MainMap dev config is missing zirat landmark binding.');

    const entity: MapEntity = {
        id: binding.entityId,
        type: 'special',
        subType: 'zirat',
        name: binding.label,
        desc: binding.description,
        gx: x, gy: y, gw: w, gh: h,
        actions: binding.actions
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
