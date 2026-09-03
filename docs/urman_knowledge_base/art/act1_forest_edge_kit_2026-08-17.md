# Act 1 Kara-Urman forest-edge kit — 2026-08-17

Status: **PARTIAL / production-candidate authored geometry.** This is not an
art lock, not demo acceptance and not a runtime integration.

## Intent and boundary

This kit addresses the observed Kara-Urman defect: a straight corridor of
repeated `PineA` cones, an empty horizon and log/proxy silhouettes that do not
give the forest edge a believable side mass or bend. It is authored geometry,
not a new texture mask.

The kit is reusable at the existing Act 1 scale. Its neutral preview board
leaves a central path in the roughly 6–8 m road-width range when the banks are
placed as a pair; the components themselves have ground-anchor origins and can
be reoriented by a later composition task. Every mesh is presentation-only:
there are no `-col` meshes, `StaticBody3D` nodes or `CollisionShape3D` nodes.
The host scene must retain ownership of walkability, path relief and gameplay
collision.

No scene, C# script, RuntimeBridge, route/state/collision owner, existing
modular kit or raster texture was changed. `assets/asset_registry.json` was
also intentionally not changed because it is outside this asset-authoring
slice's ownership; a registry entry is a follow-up before production use.

## Authored component contract

Each row is an independent mesh node in the GLB. Both exact LOD nodes are
present; the composition task may choose its own Godot visibility ranges.

| Family | Exact nodes | Enables | Does not solve |
|---|---|---|---|
| Left bank | `ForestBank_Left_LOD0`, `ForestBank_Left_LOD1` | Broken left road edge, low occluding mass, exposed roots and moss pockets | Walkability, natural terrain collision, final wet-earth material |
| Right bank | `ForestBank_Right_LOD0`, `ForestBank_Right_LOD1` | Asymmetric right edge and a controllable passage/bend counterpart | Collision, full road composition or final level-art placement |
| Left occluder | `ForestOccluder_LeftCluster_LOD0`, `ForestOccluder_LeftCluster_LOD1` | Mixed trunks, angled boughs, conifer/broadleaf side silhouette | Wind animation, full forest coverage, botanical/cultural review |
| Right occluder | `ForestOccluder_RightCluster_LOD0`, `ForestOccluder_RightCluster_LOD1` | A different side mass and value plane from the left cluster | Replacing all repeated PineA instances or proving traversal readability |
| Pine mass A | `PineMass_A_LOD0`, `PineMass_A_LOD1` | Three-trunk, branch-supported irregular pine mass; not a single cone | Final pine family, billboard/LOD acceptance or collision |
| Pine mass B | `PineMass_B_LOD0`, `PineMass_B_LOD1` | Bent-trunk, one-sided crown variation for breaking the straight corridor | Full vegetation set or target-host performance |
| Birch edge mass | `BirchEdgeMass_LOD0`, `BirchEdgeMass_LOD1` | Slender birch-like trunks and muted broadleaf edge cue, widening the woodland vocabulary | A culturally reviewed birch material/texture or animation |
| Distant horizon A | `DistantForestMass_A_LOD0`, `DistantForestMass_A_LOD1` | Low faceted horizon closure and far value plane | Sky/fog/light balance and 20–30 m in-engine repetition proof |
| Distant horizon B | `DistantForestMass_B_LOD0`, `DistantForestMass_B_LOD1` | A taller, lopsided far silhouette so the horizon is not a repeated row | Full horizon/skybox treatment or level composition |
| Left root wall | `UnderstoryRootWall_Left_LOD0`, `UnderstoryRootWall_Left_LOD1` | Layered root/understory wall that closes the near-left side without a flat fence | Ground collision, foliage animation or final undergrowth density |
| Right root wall | `UnderstoryRootWall_Right_LOD0`, `UnderstoryRootWall_Right_LOD1` | Matching but independently placeable right-side undergrowth mass | Gameplay blocking or final traversal acceptance |
| Crooked stump | `CrookedStump_LOD0`, `CrookedStump_LOD1` | Ground landmark with broken branch and cut surface; restrained near-field wrongness | A creature reveal, interaction target or stump collision |
| Branch silhouette | `BranchSilhouette_Hook_LOD0`, `BranchSilhouette_Hook_LOD1` | Crooked overhead/side silhouette for the Level 3 forest-edge language | Character/creature mesh, animation or scare beat |
| Fallen logs | `FallenLogCluster_LOD0`, `FallenLogCluster_LOD1` | Ground breakup with three angled logs and cut ends, replacing proxy-only log reads | Traversable obstacle collision or final debris family |
| Mossy boulders | `MossyBoulderCluster_LOD0`, `MossyBoulderCluster_LOD1` | Low natural ground anchors and scale cues beside the route | Collision, geology accuracy or wetness acceptance |
| Side gate landmark | `SideGate_WayfindingLandmark_LOD0`, `SideGate_WayfindingLandmark_LOD1` | Plain uneven posts, bent header, blank board and foot stones for a side threshold/bend landmark | Localized sign text, quest marker, religious motif or route comprehension proof |

The GLB also contains the root node `ForestEdgeKit_Root`. The 32 mesh nodes
above are the published instancing contract; there are no physics descendants.

## Material policy

The source uses only project-original, flat authoring materials whose names can
be routed later through the existing `PainterlyMaterialLibrary`: `DampEarth`,
`MossyStone`, `PineBark`, `WeatheredWood`, `PineFoliage`, plus muted foliage,
birch and plain wayfinding variants. No raster texture was generated or
downloaded. The kit does not silently activate v2–v6 texture candidates and
does not create a new shader/material owner.

The side landmark is deliberately plain: the board has no text, religious
mark, emblem or ornament. It is a composition/wayfinding silhouette only.

## Verification evidence

Source and derived artifact:

- `assets/source/blender/urman_forest_edge_kit.blend` — 1,365,721 bytes;
  SHA-256 `75de1f8ab43f3d29736bf44dcba05da6d0bcf51649a043f6aac8d4369aa3f3d3`.
- `game/assets/generated/urman_forest_edge_kit.glb` — 544,480 bytes;
  SHA-256 `7cc13472f0024861d5b91ae6710b3c0ac18e7fbefb87ae9dc73110604a9791ba`.
- Neutral Blender preview — `/private/tmp/urman_forest_edge_kit_preview.png`,
  1,709,781 bytes, 1600×900 RGBA;
  SHA-256 `cba56eae110e37d48d3f1cd2b88c1f51b2b26bc57f9a7970b00eaabb42600ed4`.

Successful export command:

```text
/Users/unterlantas/Documents/GitHub/URMAN/.tools/blender/Blender.app/Contents/MacOS/Blender --background --python /tmp/urman_forest_edge_kit_generator.py -- --root /Users/unterlantas/Documents/GitHub/URMAN --preview /tmp/urman_forest_edge_kit_preview.png
```

The export reported 16 component families, 32 mesh nodes, 3,768 LOD0
triangles and 1,678 LOD1 triangles. Blender re-import returned `FINISHED` and
found every published LOD pair. Godot import was run with the pinned
4.7.1 .NET binary:

```text
GODOT=/Users/unterlantas/Documents/GitHub/URMAN/.tools/godot/Godot_mono.app/Contents/MacOS/Godot
DOTNET_ROOT=/Users/unterlantas/Documents/GitHub/URMAN/.tools/dotnet DOTNET_ROOT_ARM64=/Users/unterlantas/Documents/GitHub/URMAN/.tools/dotnet DOTNET_CLI_HOME=/tmp/urman-dotnet-home NUGET_PACKAGES=/tmp/urman-nuget PATH=/Users/unterlantas/Documents/GitHub/URMAN/.tools/dotnet:/usr/bin:/bin:/usr/sbin:/sbin "$GODOT" --headless --path game --import --quit-after 2
```

The Godot load probe returned:

```text
godot-glb-load: PASS
godot-glb-mesh-count: 32
godot-glb-collision-count: 0
```

The full exact name list is the table above. The Blender exporter emitted
warnings that some decimated LOD1 mesh blocks “may be exported wrongly”; a
Blender re-import and the Godot load probe both found non-empty geometry for
all 32 published mesh nodes. This is evidence of importability, not a reason
to close the LOD/visual acceptance gate.

## Neutral preview grade

Preview inspected at `/private/tmp/urman_forest_edge_kit_preview.png`:

- Banks/root walls: **3/5** — faceted, reusable side masses are present, but
  the overview can still read as long low-poly slabs until placed against
  uneven ground in Godot.
- Occluder clusters: **4/5** — mixed trunks, branch directions and foliage
  lobes break the single-cone family.
- `PineMass_A/B`: **4/5** — multi-trunk and bent/one-sided crowns visibly vary
  the silhouette; they are not PineA duplicates.
- `BirchEdgeMass`: **3.5/5** — slender-trunk/broadleaf contrast is readable,
  but needs first-person distance review.
- Distant masses: **4/5** — the empty horizon is closed with two different
  faceted profiles.
- Stump/branch/log/boulder details: **3/5** — authored and non-proxy, but
  small/partly occluded in the neutral overview.
- Side gate landmark: **2/5** in this overview — the exact mesh exists and
  imports, but it is not yet a strong first-person read; the later composition
  task must give it a clear bend/wayfinding shot.

Overall visual grade: **3.4/5, partial**. The kit materially improves the
geometry vocabulary and horizon coverage, but the preview is a neutral Blender
board, not an in-engine traversal or art acceptance frame.

## Open gates / next owner

The next composition task should instance these exact names around the existing
Act 1 Kara-Urman road, test a slight bend with the side gate, map materials to
existing semantic owners and capture near/mid/far first-person views. It must
also check screen-space wayfinding, repetition over 20–30 m, fog/light value
separation, frame time on target hardware and cultural/level-art review.

This slice does not prove that the forest is complete, that the route is
understandable to a first-time player, that the new meshes have gameplay
collision, or that the Painterly Low-Poly art lock is accepted.
