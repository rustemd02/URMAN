# Navigation, map and save-system contract

## One source of truth

2D map and navigation must consume the same world registry:
- road graph
- parcel polygons
- building positions
- entrances
- POI
- constraints

Do not maintain a second hand-authored minimap with different topology.

## Address search

Index:
`street_id -> normalized house number -> address_id -> target access node`

Search target:
1. gate/driveway if present;
2. entrance;
3. building centroid fallback.

## Routing

A* / Dijkstra on road+walk graph.

Edge metadata:
- class
- traversal mode
- cost
- seasonal state optional
- blocked state
- surface optional

## 2D projection

Use world XZ to map XY.

Map layers:
1. terrain boundaries;
2. forest/water edges;
3. road hierarchy;
4. parcels at close zoom;
5. buildings;
6. addresses at close zoom;
7. POI / landmarks;
8. current route.

## Save stability

Never save:
`quest_target = "ул. Лесная, 12"`

Save:
`quest_target_building_id = "BLD-..."`

Address registry may then change without invalidating the save.

## Address history

If address changes:
```json
{
  "current": "ADR-X",
  "history": [
    {"street_id":"old_street","number":"11","valid_to":"dev_revision_24"}
  ]
}
```

Optional geocoder may accept old alias during development migration.
