"""Agent B Act I Kit 3: authored foliage variants.

Silhouette library so the compositor can scatter non-repeating vegetation:
birches, crooked pines, young spruces, shrubs, ferns, sedge tufts, grass
tufts, fallen branches, moss stones, dead stumps. Each variant is authored
distinctly; nothing is a uniform cone. Variants are laid on a 12 m grid
(Batman space) and re-placed in Godot by name.
"""

from __future__ import annotations

import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import agent_b_common as ab  # noqa: E402


def _faceted_canopy(name, centre, radius, squish, material, seed,
                    subdivisions=2):
    blob = ab.make_icosphere(name, centre, radius, subdivisions=subdivisions)
    blob.scale = (1.0, 1.0, squish)
    ab.displace_object(blob, radius * 0.28, 0.9, seed_offset=seed)
    ab.assign_material(blob, material)
    return blob


def birch_variant(index, height, lean):
    prefix = f"Birch_{index}"
    objs = []
    trunk = ab.make_cylinder(f"{prefix}_Trunk", (0, 0, 0), 0.09, 0.05,
                             height, segments=7)
    ab.rotate_around(trunk, lean, "x", (0.0, 0.0, 0.0))
    ab.rotate_around(trunk, lean * 0.6, "y", (0.0, 0.0, 0.0))
    ab.assign_material(trunk, "AB_bark_birch")
    objs.append(trunk)
    top = height
    for k, (r, z_off, sq) in enumerate((
        (0.85, 0.0, 0.62), (0.6, 0.7, 0.55), (0.5, -0.5, 0.5))):
        blob = _faceted_canopy(f"{prefix}_Canopy{k}",
                               (lean * height * 0.3 + (k - 1) * 0.3,
                                (k % 2) * 0.4 - 0.2,
                                top - 0.6 + z_off), r * height / 6.0, sq,
                               "AB_foliage_birch", seed=index * 3.1 + k)
        objs.append(blob)
    return objs


def pine_variant(index, height, crook):
    """Pine with irregular tiers, not a clean cone."""
    prefix = f"Pine_{index}"
    objs = []
    trunk = ab.make_cylinder(f"{prefix}_Trunk", (0, 0, 0), 0.14, 0.06,
                             height, segments=7)
    ab.rotate_around(trunk, crook * 0.16, "x", (0.0, 0.0, 0.0))
    ab.rotate_around(trunk, -crook * 0.1, "y", (0.0, 0.0, 0.0))
    ab.assign_material(trunk, "AB_bark_dark")
    objs.append(trunk)
    tiers = ((0.42, 0.30, 0.42), (0.55, 0.26, 0.52), (0.68, 0.22, 0.44),
             (0.8, 0.18, 0.5), (0.92, 0.12, 0.58))
    for k, (z_rel, r_rel, sq) in enumerate(tiers):
        r = r_rel * height * (0.85 + (k % 2) * 0.3)
        blob = _faceted_canopy(f"{prefix}_Tier{k}",
                               (crook * z_rel * height * 0.5 + (k % 2) * 0.14 - 0.07,
                                ((k * 37) % 3) * 0.1 - 0.1,
                                z_rel * height - 0.4),
                               r, sq,
                               "AB_foliage_pine", seed=index * 7.7 + k * 1.3)
        objs.append(blob)
    return objs


def spruce_variant(index, height):
    """Young spruce: narrow but softened, slightly irregular."""
    prefix = f"Spruce_{index}"
    objs = []
    trunk = ab.make_cylinder(f"{prefix}_Trunk", (0, 0, 0), 0.07, 0.03,
                             height, segments=6)
    ab.assign_material(trunk, "AB_bark_dark")
    objs.append(trunk)
    for k, (z_rel, r_rel) in enumerate(((0.2, 0.34), (0.45, 0.27),
                                        (0.68, 0.2), (0.88, 0.12))):
        r = r_rel * height
        blob = _faceted_canopy(f"{prefix}_Tier{k}", (0, 0, z_rel * height),
                               r, 0.75, "AB_foliage_spruce",
                               seed=index * 5.3 + k)
        objs.append(blob)
    return objs


def shrub_variant(index, radius):
    prefix = f"Shrub_{index}"
    objs = []
    for k, (ox, oy, oz, rr) in enumerate((
        (0, 0, radius * 0.7, radius),
        (radius * 0.6, 0.1, radius * 0.5, radius * 0.6),
        (-radius * 0.5, -0.15, radius * 0.45, radius * 0.55))):
        blob = _faceted_canopy(f"{prefix}_Lobe{k}", (ox, oy, oz), rr, 0.72,
                               "AB_foliage_shrub", seed=index * 11.0 + k)
        objs.append(blob)
    return objs


def _blade(prefix, index_k, size_x, size_y, size_z, material):
    """Blade mesh extending along +y with its pivot at the origin."""
    return ab.make_box(prefix, -size_x / 2, size_x / 2, 0.0, size_y,
                       0.0, size_z)


def fern_variant(index):
    prefix = f"Fern_{index}"
    objs = []
    fronds = 6 if index % 2 == 0 else 7
    for k in range(fronds):
        angle = (k / fronds) * math.tau + index * 0.35
        blade = _blade(f"{prefix}_Frond{k}", k, 0.04, 0.64, 0.05, "AB_fern")
        ab.rotate_around(blade, 0.62, "x", (0.0, 0.0, 0.0))
        ab.rotate_around(blade, angle, "z", (0.0, 0.0, 0.0))
        ab.translate_vertices(blade, math.cos(angle) * 0.26,
                              math.sin(angle) * 0.26, 0.28)
        ab.assign_material(blade, "AB_fern")
        objs.append(blade)
    return objs


def sedge_variant(index):
    prefix = f"Sedge_{index}"
    objs = []
    blades = 8
    for k in range(blades):
        angle = (k / blades) * math.tau + index
        blade = _blade(f"{prefix}_Blade{k}", k, 0.024, 0.6, 0.03, "AB_sedge")
        ab.rotate_around(blade, 0.5 + (k % 2) * 0.18, "x", (0.0, 0.0, 0.0))
        ab.rotate_around(blade, angle, "z", (0.0, 0.0, 0.0))
        ab.translate_vertices(blade, math.cos(angle) * 0.12,
                              math.sin(angle) * 0.12, 0.3)
        ab.assign_material(blade, "AB_sedge")
        objs.append(blade)
    return objs


def grass_tuft(index):
    prefix = f"GrassTuft_{index}"
    objs = []
    for k in range(5):
        angle = (k / 5) * math.tau + index * 0.8
        blade = _blade(f"{prefix}_B{k}", k, 0.04, 0.36, 0.024, "AB_grass")
        ab.rotate_around(blade, 0.4, "x", (0.0, 0.0, 0.0))
        ab.rotate_around(blade, angle, "z", (0.0, 0.0, 0.0))
        ab.translate_vertices(blade, math.cos(angle) * 0.08,
                              math.sin(angle) * 0.08, 0.16)
        ab.assign_material(blade, "AB_grass" if index % 3 else "AB_grass_dry")
        objs.append(blade)
    return objs


def fallen_branch(index, length):
    prefix = f"FallenBranch_{index}"
    objs = []
    branch = ab.make_cylinder(f"{prefix}_Main", (0, 0, 0.06), 0.035, 0.015,
                              length, segments=6, axis="x")
    ab.assign_material(branch, "AB_bark")
    objs.append(branch)
    twig = ab.make_cylinder(f"{prefix}_Twig", (length * 0.4, 0, 0.08), 0.015,
                            0.005, 0.45, segments=5)
    ab.rotate_around(twig, 0.45, "x", (length * 0.4, 0, 0.08))
    ab.assign_material(twig, "AB_bark")
    objs.append(twig)
    return objs


def moss_stone(index, radius):
    prefix = f"MossStone_{index}"
    stone = ab.make_icosphere(f"{prefix}_Stone", (0, 0, radius * 0.4), radius,
                              subdivisions=1)
    ab.displace_object(stone, radius * 0.3, 1.4, seed_offset=index * 3.3)
    ab.assign_material(stone, "AB_stone")
    moss = ab.make_icosphere(f"{prefix}_Moss", (0, 0, radius * 0.55),
                             radius * 0.66, subdivisions=1)
    moss.scale = (1.05, 1.05, 0.5)
    ab.displace_object(moss, radius * 0.2, 1.6, seed_offset=index * 6.1)
    ab.assign_material(moss, "AB_moss")
    return [stone, moss]


def dead_stump(index, height):
    prefix = f"Stump_{index}"
    stump = ab.make_cylinder(f"{prefix}_Stump", (0, 0, 0), 0.22, 0.18,
                             height, segments=7)
    ab.assign_material(stump, "AB_bark_dark")
    root = ab.make_box(f"{prefix}_Root", -0.4, 0.4, -0.1, 0.1, 0.0, 0.14,
                       jitter=0.03)
    root.rotation_euler.z = index * 0.7
    ab.assign_material(root, "AB_bark")
    return [stump, root]


def main() -> None:
    ab.setup_scene()
    objects: list = []
    variants: list[tuple[str, object]] = []

    for spec in ((1, 6.4, 0.03), (2, 7.6, -0.02), (3, 5.6, 0.05)):
        objects.extend(birch_variant(*spec))
        variants.append((f"Birch_{spec[0]}", None))
    for spec in ((1, 8.0, 0.5), (2, 9.2, -0.7), (3, 6.8, 0.9)):
        objects.extend(pine_variant(*spec))
        variants.append((f"Pine_{spec[0]}", None))
    for spec in ((1, 2.6), (2, 3.2), (3, 2.1)):
        objects.extend(spruce_variant(*spec))
        variants.append((f"Spruce_{spec[0]}", None))
    for spec in ((1, 0.7), (2, 0.55), (3, 0.9)):
        objects.extend(shrub_variant(*spec))
        variants.append((f"Shrub_{spec[0]}", None))
    for i in range(3):
        objects.extend(fern_variant(i))
        variants.append((f"Fern_{i}", None))
    for i in range(3):
        objects.extend(sedge_variant(i))
        variants.append((f"Sedge_{i}", None))
    for i in range(3):
        objects.extend(grass_tuft(i))
        variants.append((f"GrassTuft_{i}", None))
    for i in range(2):
        objects.extend(fallen_branch(i, 1.4 + i * 0.5))
        variants.append((f"FallenBranch_{i}", None))
    objects.extend(moss_stone(0, 0.5))
    variants.append(("MossStone_0", None))
    objects.extend(dead_stump(0, 0.55))
    variants.append(("Stump_0", None))

    # Lay variants on a 12 m grid; Godot composer locates each by name.
    offsets: list[str] = []
    for idx, (name, _) in enumerate(variants):
        ox = (idx % 8) * 12.0
        oy = (idx // 8) * 12.0
        offsets.append(f"{name}:{ox:.1f},{oy:.1f}")
        for obj in objects:
            if obj.name == name or obj.name.startswith(name + "_"):
                ab.translate_vertices(obj, ox, oy, 0.0)

    root = os.environ.get("AGENTB_OUT", HERE)
    glb_path = os.path.join(root, "game/assets/models/agent_b_act1",
                            "agentb_foliage_kit.glb")
    blend_path = os.path.join(root, "assets/source/blender/agent_b_act1",
                              "agentb_foliage_kit.blend")
    ab.export_glb(objects, glb_path, "URMAN_AgentB_FoliageKit")
    ab.save_blend(blend_path)
    ab.report_kit("agentb_foliage_kit", objects, glb_path)
    print("AGENTB_VARIANTS " + ";".join(offsets))


if __name__ == "__main__":
    main()
