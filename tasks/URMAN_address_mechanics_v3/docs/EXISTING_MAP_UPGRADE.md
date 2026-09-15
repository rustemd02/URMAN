# Existing Map Upgrade Pipeline

## Goal

Сделать текущую карту логичнее **без потери авторской работы**.

## Phase A — ingest

Поддерживаемый абстрактный input:
- roads: polyline + optional street_id/class
- buildings: position/footprint + optional entrance + optional existing address
- parcels: polygon optional
- constraints: forest/water/steep/no-build
- poi

Если движок хранит это иначе, Astra пишет adapter.

## Phase B — normalization

1. road intersections -> graph nodes;
2. близкие endpoints snap только логически;
3. геометрию пока не двигать;
4. building -> nearest viable access road;
5. existing addresses copied verbatim into `legacy_address`.

## Phase C — audit

Ошибки severity HIGH:
- жилой building unreachable from main road graph;
- duplicate committed address;
- address references nonexistent street;
- parcel has no access and building is marked residential;
- disconnected residential road component with no external connection.

MEDIUM:
- absurd driveway distance;
- unnamed road serving many houses;
- street changes name in the middle without node;
- unnecessary near-parallel duplicate road;
- forest spur collides with protected/no-build zone.

LOW:
- numbering gaps;
- dead ends;
- irregular parcel shapes.

Не «исправлять» LOW только ради красоты.

## Phase D — repair proposals

Каждое предложение содержит:
- `proposal_id`
- причина;
- severity;
- affected entities;
- exact suggested change;
- estimated gameplay gain;
- destructive flag.

Astra показывает/логирует предложения и в режиме preserve не применяет их автоматически.

## Phase E — enrichment

После topology:
- stable IDs;
- parcel ownership;
- addresses;
- quarter IDs;
- nav access nodes;
- minimap data;
- signs/mount metadata.

## Phase F — commit

После первого production commit:
- `building_id` immutable;
- `parcel_id` immutable;
- address changes create `address_history`;
- old address can remain alias for quest/search migration.
