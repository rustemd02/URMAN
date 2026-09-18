# Fallback procedural village generation

Использовать только если готовой карты нет.

## Inputs

- terrain/slope
- forest mask
- water
- external road connection
- no-build zones
- desired house count
- existing POI constraints

## Algorithm

1. Place / preserve main external connection.
2. Grow `tukay` as main path through viable settlement area.
3. Seed candidate residential locations using:
   - road proximity
   - slope
   - dry land
   - sunlight/open space if available
   - distance from hazards
4. Add local roads to serve seeds.
5. Prefer reusing existing road before creating a new one.
6. Add a limited number of cycles where they meaningfully shorten routes.
7. Generate parcels with road frontage.
8. Place buildings inside parcels with plausible setback/orientation.
9. Create districts/quarters.
10. Run the same audit used for authored maps.
11. Assign addresses only after geometry/topology is frozen.

## Do not optimize for perfect symmetry

A village should tolerate:
- missing numbers;
- unequal parcel sizes;
- short dead ends;
- roads bending around terrain;
- old core + newer peripheral growth.

The system optimizes **coherence**, not suburban neatness.
