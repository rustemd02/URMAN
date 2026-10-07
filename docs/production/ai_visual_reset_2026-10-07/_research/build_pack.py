#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Собирает курированный пак кадров игры УРМАН для внешнего art-ревью.

Копирует выбранные PNG из репозитория в docs/production/ai_visual_reset_2026-10-07/screenshots/,
переименовывает в NNN_slug.jpg (JPEG q88, оригинальное разрешение — кроме явных 4K)
и пишет JSON-манифест, из которого затем генерируется индекс.
"""
import json
import os
import shutil
import subprocess
import sys

ROOT = "/Users/unterlantas/Documents/GitHub/URMAN"
OUT = os.path.join(ROOT, "docs/production/ai_visual_reset_2026-10-07/screenshots")

P = "docs/production"
A = "docs/urman_knowledge_base/art"
C = ".codex-captures"

# (категория, slug, путь-от-корня, что видно / на что смотреть)
ITEMS = [
    # ============ 01 WORLD / EXTERIOR ============
    ("01_world_exterior", "route_12_frames_act1", f"{P}/urman_visual_review_pack/01_current_world_panoramas.png",
     "12 кадров полного маршрута Акта I (приезд, улицы, двор, ФАП, зират, ночная кромка). Общее впечатление от мира."),
    ("01_world_exterior", "annotated_problems", f"{P}/urman_visual_review_pack/02_problem_annotations.png",
     "Те же кадры с подписями проблем: дорога-полотно без колеи, плоский снег, «палочные» кроны, пустой горизонт."),
    ("01_world_exterior", "progress_baseline_to_winter", f"{P}/urman_visual_review_pack/05_progress_baseline_to_winter.png",
     "Стартовый летний вид против текущего зимнего: что уже сделано и что осталось сырым."),
    ("01_world_exterior", "progress_density_boundaries", f"{P}/urman_visual_review_pack/06_progress_density_and_boundaries.png",
     "Плотность растительности и границы деревни было/стало; невидимая стена вместо опушки."),
    ("01_world_exterior", "progress_snow_and_trees", f"{P}/urman_visual_review_pack/07_progress_snow_and_trees.png",
     "Снег и деревья v1→v2: «палки» вместо деревьев, снег как белое полотно."),
    ("01_world_exterior", "progress_road_snow_trees", f"{P}/urman_visual_review_pack/08_progress_road_snow_trees.png",
     "Последняя правка дороги, сугробов, веток. Деревья стояли на дороге."),
    ("01_world_exterior", "main_street_dolly_strip", f"{P}/urman_visual_review_pack/22_main_street_dolly_strip.png",
     "Проезд камеры по главной улице, 3 кадра. Оценивать ритм застройки и глубину улицы."),
    ("01_world_exterior", "village_life_cat_walk_strip", f"{P}/urman_visual_review_pack/23_village_life_cat_walk_strip.png",
     "Живое событие деревни (кот идёт через двор), 4 кадра. Единственный пример «жизни» мира."),
    ("01_world_exterior", "babai_street", f"{P}/village_relayout_2026-10-01/captures/babai_street.png", "Улица у дома бабая."),
    ("01_world_exterior", "babai_yard", f"{P}/village_relayout_2026-10-01/captures/babai_yard.png", "Двор бабая и әби."),
    ("01_world_exterior", "babai_air", f"{P}/village_relayout_2026-10-01/captures/babai_air.png", "Двор бабая сверху: композиция участка."),
    ("01_world_exterior", "farbank_aerial", f"{P}/village_relayout_2026-10-01/captures/farbank_aerial.png", "Дальний берег сверху: план деревни."),
    ("01_world_exterior", "farbank_street", f"{P}/village_relayout_2026-10-01/captures/farbank_street.png", "Улица дальнего берега."),
    ("01_world_exterior", "farbank_lane", f"{P}/village_relayout_2026-10-01/captures/farbank_lane.png", "Переулок дальнего берега."),
    ("01_world_exterior", "farbank_police", f"{P}/village_relayout_2026-10-01/captures/farbank_police.png", "Полицейский участок и окружение."),
    ("01_world_exterior", "gorge_bridge", f"{P}/village_relayout_2026-10-01/captures/gorge_bridge.png", "Мост через овраг."),
    ("01_world_exterior", "gorge_inside", f"{P}/village_relayout_2026-10-01/captures/gorge_inside.png", "Внутри оврага."),
    ("01_world_exterior", "ravine_bridge", f"{P}/village_relayout_2026-10-01/captures/ravine_bridge.png", "Мост через промоину."),
    ("01_world_exterior", "mosque_front", f"{P}/village_relayout_2026-10-01/captures/mosque_front.png", "Мечеть с улицы."),
    ("01_world_exterior", "mosque_air", f"{P}/village_relayout_2026-10-01/captures/mosque_air.png", "Мечеть сверху: минарет, двор, окружение."),
    ("01_world_exterior", "snow_culvert", f"{P}/village_relayout_2026-10-01/captures/snow_culvert.png", "Снежный надув у трубы."),
    ("01_world_exterior", "snow_far_street", f"{P}/village_relayout_2026-10-01/captures/snow_far_street.png", "Дальняя заснеженная улица."),
    ("01_world_exterior", "snow_street_south", f"{P}/village_relayout_2026-10-01/captures/snow_street_south.png", "Южная улица: снег, колея, обочины."),
    ("01_world_exterior", "layout_scheme", f"{P}/village_relayout_2026-10-01/scheme.png", "Схема перепланировки деревни: где что стоит."),
    ("01_world_exterior", "layout_audit", f"{P}/village_relayout_2026-10-01/audit/audit-2026-10-02.png", "Аудит планировки: топология улиц и участков."),
    ("01_world_exterior", "rejected_layout_paused", f"{P}/village_relayout_2026-10-02/evidence/rejected-paused-captures/main_street.png", "Отклонённый/остановленный вариант: главная улица."),
    ("01_world_exterior", "rejected_capka_east", f"{P}/village_relayout_2026-10-02/evidence/rejected-paused-captures/capka_east.png", "Отклонённый/остановленный вариант: восток деревни."),
    ("01_world_exterior", "life_day_high", f"{P}/village_life_2026-10-04/day_high.png", "Дневная жизнь деревни, высокий ракурс."),
    ("01_world_exterior", "life_day_low", f"{P}/village_life_2026-10-04/day_low.png", "Дневная жизнь деревни, низкий ракурс (как видит игрок)."),
    ("01_world_exterior", "life_night_high", f"{P}/village_life_2026-10-04/night_high.png", "Ночная деревня: свет в окнах, фонари, читаемость силуэтов."),
    ("01_world_exterior", "police_exterior", f"{P}/police_post_2026-10-04/licensed-v8/exterior.png", "Участковый пункт: последний лицензированный набор, общий вид."),
    ("01_world_exterior", "police_zhiguli", f"{P}/police_post_2026-10-04/licensed-v8/zhiguli.png", "Машина ВАЗ у участка (лицензированная модель)."),
    ("01_world_exterior", "police_zhiguli_front", f"{P}/police_post_2026-10-04/licensed-v8/zhiguli-front.png", "ВАЗ спереди: материалы, стёкла, шины."),
    ("01_world_exterior", "repair_day_yard_west", f"{P}/village_repair_2026-10-04/day-yard-west.png", "Двор днём после ремонта."),
    ("01_world_exterior", "repair_day_yard_east", f"{P}/village_repair_2026-10-04/day-yard-east.png", "Двор днём после ремонта, другой ракурс."),
    ("01_world_exterior", "repair_night_yard", f"{P}/village_repair_2026-10-04/night-yard.png", "Двор ночью: освещение и тени."),
    ("01_world_exterior", "repair_minaret_lantern", f"{P}/village_repair_2026-10-04/minaret-lantern.png", "Фонарь на минарете."),
    ("01_world_exterior", "repair_mosque_exterior", f"{P}/village_repair_2026-10-04/mosque-exterior.png", "Мечеть снаружи после ремонта."),
    ("01_world_exterior", "intro_flyover_village", f"{P}/mvp_tracker_2026-09-27/a02-intro-flyover/02_village_large.png", "Вступительный облёт: деревня целиком."),
    ("01_world_exterior", "intro_flyover_departure", f"{P}/mvp_tracker_2026-09-27/a02-intro-flyover/01_departure.png", "Вступление: отъезд из города."),
    ("01_world_exterior", "kara_forest_left", f"{P}/mvp_tracker_2026-09-27/a10-kara-grade/kara_forest_left.png", "Кромка леса у Кара-Урмана."),
    ("01_world_exterior", "mosque_edge_after", f"{P}/mvp_tracker_2026-09-27/a10-west-mosque-edge/after_contact.png", "Западная кромка у мечети, после правки."),
    ("01_world_exterior", "remote_arrival_forward", f"{C}/remote/45201e074d8d4f018634217c368165b7/frames/arrival_forward.png", "Кадр удалённого прогона: приезд, вид вперёд."),
    ("01_world_exterior", "remote_arrival_depth", f"{C}/remote/45201e074d8d4f018634217c368165b7/frames/arrival_depth.png", "Кадр удалённого прогона: приезд, глубина."),
    ("01_world_exterior", "remote_street_forward", f"{C}/remote/45201e074d8d4f018634217c368165b7/frames/street_forward.png", "Кадр удалённого прогона: улица вперёд."),
    ("01_world_exterior", "remote_street_back", f"{C}/remote/45201e074d8d4f018634217c368165b7/frames/street_back.png", "Кадр удалённого прогона: улица назад."),
    ("01_world_exterior", "remote_street_right", f"{C}/remote/45201e074d8d4f018634217c368165b7/frames/street_right.png", "Кадр удалённого прогона: улица вбок."),
    ("01_world_exterior", "remote_fap_forward", f"{C}/remote/45201e074d8d4f018634217c368165b7/frames/fap_forward.png", "Кадр удалённого прогона: ФАП вперёд."),
    ("01_world_exterior", "remote_fap_back", f"{C}/remote/45201e074d8d4f018634217c368165b7/frames/fap_back.png", "Кадр удалённого прогона: ФАП назад."),
    ("01_world_exterior", "remote_return_depth", f"{C}/remote/45201e074d8d4f018634217c368165b7/frames/return_depth.png", "Кадр удалённого прогона: обратный путь."),
    ("01_world_exterior", "roadside_shoulder", f"{C}/worker2-roadside-before-20260915/zirat_roadside_shoulder.png", "Обочина зиратской дороги до правки."),
    ("01_world_exterior", "roadside_footbridge", f"{C}/worker2-roadside-before-20260915/zirat_outer_footbridge.png", "Пешеходный мостик у зирата до правки."),
    ("01_world_exterior", "roadside_culvert", f"{C}/worker2-roadside-before-20260915/zirat_stone_culvert.png", "Каменная труба у зирата до правки."),

    # ============ 02 MATERIALS / TREES / SNOW ============
    ("02_materials_snow_trees", "style_targets_accepted", f"{P}/urman_visual_review_pack/10_style_targets.png",
     "Принятые в проекте референсы стиля (painterly low-poly). Целевое направление."),
    ("02_materials_snow_trees", "ref_atmosphere_target", f"{A}/style_refs/painterly_low_poly_atmosphere_target.png", "Референс атмосферы."),
    ("02_materials_snow_trees", "ref_geometry_target", f"{A}/style_refs/painterly_low_poly_geometry_target.png", "Референс геометрии/силуэтов."),
    ("02_materials_snow_trees", "trees_before_after", f"{P}/urman_visual_review_pack/16_winter_trees_before_after.png",
     "Три породы зимних деревьев до/после: было «рогатая палка»."),
    ("02_materials_snow_trees", "spruce_boughs_after", f"{P}/urman_visual_review_pack/17_spruce_boughs_before_after.png",
     "Ель до/после: слоёный конус из плоских ярусов."),
    ("02_materials_snow_trees", "window_surrounds_after", f"{P}/urman_visual_review_pack/15_window_surrounds_before_after.png",
     "Наличники до/после: окна как тёмные прорези."),
    ("02_materials_snow_trees", "snow_albedo_set", f"{P}/urman_visual_review_pack/03_snow_albedo_set.png",
     "6 сгенерированных альбедо снега/льда: в текстуре снег есть, в кадре — нет."),
    ("02_materials_snow_trees", "trample_trail", f"{P}/urman_visual_review_pack/04_trample_trail_before_after.png",
     "След игрока: проминание снега слишком деликатное."),
    ("02_materials_snow_trees", "fap_floor_after", f"{P}/urman_visual_review_pack/18_fap_floor_before_after.png",
     "Пол ФАПа до/после привязки текстуры."),
    ("02_materials_snow_trees", "material_swatches", f"{A}/texture_candidate_frames/godot_material_swatches_texture_candidates_1080p.png",
     "Свотчи материалов: как текстуры выглядят в движке."),
    ("02_materials_snow_trees", "day_street_tex_v4", f"{A}/texture_candidate_frames/v4/godot_day_street_texture_candidates_1080p.png",
     "Кандидаты текстур улицы, v4."),
    ("02_materials_snow_trees", "day_street_tex_v6_motion", f"{A}/texture_candidate_motion_sweep_v6/godot_day_street_texture_v6_motion_sweep_1080p.png",
     "Тот же материал в движении: швы, тайлинг, мыло."),
    ("02_materials_snow_trees", "wetness_0_40", f"{A}/wetness_candidate_matrix_v2/godot_day_street_wetness_roughness_0_40_1080p.png", "Матрица влажности, roughness 0.40."),
    ("02_materials_snow_trees", "wetness_0_60", f"{A}/wetness_candidate_matrix_v2/godot_day_street_wetness_roughness_0_60_1080p.png", "Матрица влажности, roughness 0.60."),
    ("02_materials_snow_trees", "style_calib_baseline", f"{A}/style_calibration_candidate/godot_day_street_baseline_1080p.png", "Калибровка стиля: базовая картинка."),
    ("02_materials_snow_trees", "style_calib_candidate", f"{A}/style_calibration_candidate/godot_day_street_candidate_1080p.png", "Калибровка стиля: предлагавшаяся правка."),
    ("02_materials_snow_trees", "style_temporal_day", f"{A}/style_temporal_sweep/godot_day_street_temporal_sweep_1080p.png", "Временной свип: мерцание и стабильность картинки."),
    ("02_materials_snow_trees", "style_frame_day_street", f"{A}/style_frames/godot_day_street_1080p.png", "Опорный кадр улицы днём."),
    ("02_materials_snow_trees", "style_frame_edge", f"{A}/style_frames/godot_kara_urman_edge_1080p.png", "Опорный кадр кромки Кара-Урмана."),
    ("02_materials_snow_trees", "style_frame_house_interior", f"{A}/style_frames/godot_house_old_pc_1080p.png", "Опорный кадр дома со старым ПК."),
    ("02_materials_snow_trees", "puddle_silhouette", f"{A}/puddle_silhouette_candidate/godot_day_street_puddle_silhouette_diagnostic_1080p.png", "Диагностика лужи/силуэта на снегу."),
    ("02_materials_snow_trees", "oldpc_close_front", f"{A}/oldpc_hero_detail_candidate/godot_oldpc_close_front_1080p.png", "Старый ПК крупным планом, спереди."),
    ("02_materials_snow_trees", "oldpc_close_side", f"{A}/oldpc_hero_detail_candidate/godot_oldpc_close_side_1080p.png", "Старый ПК крупным планом, сбоку."),

    # ============ 03 INTERIORS ============
    ("03_interiors", "interiors_montage", f"{P}/urman_visual_review_pack/12_act1_interiors_1080p.png",
     "Четыре интерьера (дом бабая, ФАП, старый ПК, архив): обжитость и свет."),
    ("03_interiors", "police_reception", f"{P}/police_post_2026-10-04/licensed-v8/reception.png", "Участок: приёмная, стойка, персонаж."),
    ("03_interiors", "police_cell", f"{P}/police_post_2026-10-04/licensed-v8/cell.png", "Участок: камера."),
    ("03_interiors", "police_corridor", f"{P}/police_post_2026-10-04/licensed-v8/corridor.png", "Участок: коридор."),
    ("03_interiors", "police_mouse", f"{P}/police_post_2026-10-04/licensed-v8/mouse.png", "Участок: мелкий реквизит (мышь) — уровень детализации."),
    ("03_interiors", "mosque_hall", f"{P}/village_repair_2026-10-04/mosque-hall.png", "Молельный зал мечети."),
    ("03_interiors", "mosque_library", f"{P}/village_repair_2026-10-04/mosque-library.png", "Библиотека при мечети."),
    ("03_interiors", "mosque_spiral", f"{P}/village_repair_2026-10-04/mosque-spiral.png", "Винтовая лестница минарета."),
    ("03_interiors", "library_open", f"{P}/village_repair_2026-10-04/library-open.png", "Открытая книга/документ крупно."),
    ("03_interiors", "club_hall_stage", f"{P}/visual_rework_2026-09-30/captures/club_hall_stage.png", "ДК: зал и сцена."),
    ("03_interiors", "club_parquet_close", f"{P}/visual_rework_2026-09-30/captures/club_parquet_close.png", "ДК: паркет крупно — качество материала пола."),
    ("03_interiors", "school_corridor", f"{P}/visual_rework_2026-09-30/captures/school_corridor.png", "Школа: коридор."),
    ("03_interiors", "school_room", f"{P}/visual_rework_2026-09-30/captures/school_room.png", "Школа: класс."),
    ("03_interiors", "visual_rework_hall_chairs", f"{P}/visual_rework_2026-09-29/captures/hall_chairs.png", "ДК: стулья в зале до правки."),
    ("03_interiors", "visual_rework_school_desks", f"{P}/visual_rework_2026-09-29/captures/school_desks.png", "Школа: парты до правки."),
    ("03_interiors", "fap_waiting_aisle", f"{P}/act1_takeover_evidence_2026-09-16/texture-clinic-20260922-02/fap_furniture_waiting_aisle.png", "ФАП: мебель в коридоре ожидания."),
    ("03_interiors", "fap_mirror_front", f"{P}/act1_takeover_evidence_2026-09-16/texture-clinic-20260922-02/fap_mirror_front.png", "ФАП: зеркало (отражения/roughness)."),
    ("03_interiors", "fap_wash_basin", f"{P}/act1_takeover_evidence_2026-09-16/texture-clinic-20260922-02/fap_wash_basin_close.png", "ФАП: раковина крупно."),
    ("03_interiors", "fap_window_left", f"{P}/act1_takeover_evidence_2026-09-16/texture-clinic-20260922-02/fap_window_left.png", "ФАП: окно — наличники, стекло, свет."),
    ("03_interiors", "fap_entry_approach", f"{P}/act1_takeover_evidence_2026-09-16/texture-clinic-20260922-02/fap_aligned_entry_approach.png", "ФАП: подход ко входу."),
    ("03_interiors", "hero_house_entry", f"{P}/act1_takeover_evidence_2026-09-16/texture-home-finish-20260922-01/hero_house_entry.png", "Дом бабая: вход изнутри."),
    ("03_interiors", "hero_house_wall_front", f"{P}/act1_takeover_evidence_2026-09-16/texture-home-finish-20260922-01/hero_house_wall_front.png", "Дом бабая: передняя стена — бревно, швы, текстура."),
    ("03_interiors", "hero_house_photo_corner", f"{P}/act1_takeover_evidence_2026-09-16/texture-home-finish-20260922-01/hero_house_photo_corner.png", "Дом бабая: фото-угол (обжитость)."),
    ("03_interiors", "hero_house_stove_support", f"{P}/act1_takeover_evidence_2026-09-16/texture-home-finish-20260922-01/hero_house_stove_support.png", "Дом бабая: печь."),
    ("03_interiors", "hero_house_window_close", f"{P}/act1_takeover_evidence_2026-09-16/texture-home-finish-20260922-01/hero_house_window_close.png", "Дом бабая: окно крупно."),
    ("03_interiors", "mosque_vestibule", f"{P}/act1_takeover_evidence_2026-09-16/images-mosque-continuous-01/09_mosque_vestibule.png", "Мечеть: вестибюль."),
    ("03_interiors", "mosque_hall_timur", f"{P}/act1_takeover_evidence_2026-09-16/images-mosque-continuous-01/09_mosque_hall_timur.png", "Мечеть: зал, Тимур хәзрәт."),
    ("03_interiors", "mosque_west_hall", f"{P}/act1_takeover_evidence_2026-09-16/images-mosque-continuous-01/09b_mosque_west_hall.png", "Мечеть: западный зал."),
    ("03_interiors", "mosque_courtyard_gate", f"{P}/act1_takeover_evidence_2026-09-16/images-mosque-continuous-01/08c_mosque_courtyard_gate.png", "Мечеть: ворота двора."),
    ("03_interiors", "school_office", f"{P}/act1_takeover_evidence_2026-09-16/images-public-buildings-08/school_03_office.png", "Школа: кабинет."),
    ("03_interiors", "school_classroom_photo", f"{P}/act1_takeover_evidence_2026-09-16/images-public-buildings-08/school_04_classroom_photo.png", "Школа: класс с фото-документом."),
    ("03_interiors", "council_archive_album", f"{P}/act1_takeover_evidence_2026-09-16/images-public-buildings-08/council_04_archive_album.png", "Сельсовет: архивный альбом."),
    ("03_interiors", "bath_stove_steam", f"{P}/act1_takeover_evidence_2026-09-16/images-facilities-11/03_bath_stove_and_steam.png", "Баня: печь и пар."),
    ("03_interiors", "bath_dry_rack", f"{P}/act1_takeover_evidence_2026-09-16/images-facilities-11/06_bath_empty_dry_rack.png", "Баня: пустая сушилка — «пустая плоскость»."),
    ("03_interiors", "carry_log_yard", f"{P}/act1_takeover_evidence_2026-09-16/images-carry-24/01_log_and_yard.png", "Двор, бревно, взаимодействие с предметом."),
    ("03_interiors", "carry_snow_before", f"{P}/act1_takeover_evidence_2026-09-16/images-carry-24/03_snow_before.png", "Снег до расчистки."),
    ("03_interiors", "carry_snow_cleared", f"{P}/act1_takeover_evidence_2026-09-16/images-carry-24/04_snow_cleared.png", "Снег после расчистки."),
    ("03_interiors", "carry_uncovered_axe", f"{P}/act1_takeover_evidence_2026-09-16/images-carry-24/05_uncovered_axe.png", "Найденный топор крупно."),

    # ============ 04 CHARACTERS ============
    ("04_characters", "cast_closeups", f"{P}/urman_visual_review_pack/11_act1_cast_closeups_1080p.png",
     "Шесть персонажей крупным планом: читаемость лиц, возраст, «манекенность»."),
    ("04_characters", "cast_motion_phases", f"{P}/urman_visual_review_pack/14_act1_cast_motion_phases.png",
     "По три фазы движения каждого персонажа: поза, силуэт в ходьбе."),
    ("04_characters", "npc_idle_phases", f"{P}/urman_visual_review_pack/20_npc_idle_motion_phases.png", "Покачивание NPC: две фазы."),
    ("04_characters", "npc_clip_after_skin", f"{P}/urman_visual_review_pack/21_npc_clip_motion_after_skin.png", "Idle-клип после перехода на скин."),
    ("04_characters", "cast_after_skin", f"{P}/urman_visual_review_pack/24_cast_after_skin.png", "Все шесть NPC после скина: целостность частей тела."),
    ("04_characters", "hands_before_after", f"{P}/urman_visual_review_pack/13_act1_hands_before_after.png", "Кисти рук до/после сужения — «рукавицы»."),
    ("04_characters", "police_officer", f"{P}/police_post_2026-10-04/licensed-v8/officer.png", "Участковый крупно: форма, лицо, руки."),
    ("04_characters", "player_standing_look_down", f"{P}/act1_takeover_evidence_2026-09-20/player-movement/01_standing_look_down.png", "Игрок: взгляд вниз стоя."),
    ("04_characters", "player_walking_lower_body", f"{P}/act1_takeover_evidence_2026-09-20/player-movement/02_walking_lower_body.png", "Игрок: ноги в ходьбе."),
    ("04_characters", "player_crouched", f"{P}/act1_takeover_evidence_2026-09-20/player-movement/03_crouched_look_down.png", "Игрок: присед, взгляд вниз."),
    ("04_characters", "gait_00", f"{P}/act1_takeover_evidence_2026-09-20/gait/gait_00_tick000.png", "Цикл ходьбы, кадр 0."),
    ("04_characters", "gait_04", f"{P}/act1_takeover_evidence_2026-09-20/gait/gait_04_tick024.png", "Цикл ходьбы, кадр 24."),
    ("04_characters", "gait_08", f"{P}/act1_takeover_evidence_2026-09-20/gait/gait_08_tick048.png", "Цикл ходьбы, кадр 48."),
    ("04_characters", "gait_12", f"{P}/act1_takeover_evidence_2026-09-20/gait/gait_12_tick072.png", "Цикл ходьбы, кадр 72."),
    ("04_characters", "alsu_invitation", f"{P}/act1_takeover_evidence_2026-09-16/images-alsu-walk-08/alsu_invitation.png", "Алсу: приглашение, поза и лицо."),
    ("04_characters", "alsu_step", f"{P}/act1_takeover_evidence_2026-09-16/images-alsu-walk-08/alsu_actual_supported_step.png", "Алсу: шаг по двору."),
    ("04_characters", "alsu_yard_turn", f"{P}/act1_takeover_evidence_2026-09-16/images-alsu-walk-08/alsu_yard_turn_clear_06.png", "Алсу: поворот во дворе."),
    ("04_characters", "alsu_waiting", f"{P}/act1_takeover_evidence_2026-09-16/images-alsu-walk-08/alsu_waiting_after_departure.png", "Алсу: ожидание после ухода."),
    ("04_characters", "intro_handoff", f"{P}/mvp_tracker_2026-09-27/a02-intro-smoke/03_handoff.png", "Передача управления после вступления: первый вид от игрока."),

    # ============ 05 UI ============
    ("05_ui", "ui_screens_montage", f"{P}/urman_visual_review_pack/09_ui_screens.png",
     "Интерфейс: журнал, старый ПК, документ, диалог, настройки — стиль «архив/бумага»."),
    ("05_ui", "ui_720p_large_text", f"{P}/urman_visual_review_pack/19_ui_720p_large_text.png", "UI при 720p с крупным шрифтом: читаемость."),
    ("05_ui", "pc_desktop", f"{P}/act1_takeover_evidence_2026-09-16/images-oldpc-desktop-06/01_desktop.png", "Старый ПК: рабочий стол."),
    ("05_ui", "pc_start_menu", f"{P}/act1_takeover_evidence_2026-09-16/images-oldpc-desktop-06/01b_start_menu.png", "Старый ПК: меню Пуск."),
    ("05_ui", "pc_tatwiki", f"{P}/act1_takeover_evidence_2026-09-16/images-oldpc-desktop-06/02_tatwiki.png", "Старый ПК: статья TatWiki."),
    ("05_ui", "pc_yalkyn", f"{P}/act1_takeover_evidence_2026-09-16/images-oldpc-desktop-06/03_yalkyn.png", "Старый ПК: мессенджер Ялкын."),
    ("05_ui", "pc_yalkyn_replies", f"{P}/act1_takeover_evidence_2026-09-16/images-oldpc-desktop-06/03b_yalkyn_replies.png", "Ялкын: ветка ответов."),
    ("05_ui", "pc_file_manager", f"{P}/act1_takeover_evidence_2026-09-16/images-oldpc-desktop-06/04a_file_manager.png", "Старый ПК: файловый менеджер."),
    ("05_ui", "pc_writer_windows", f"{P}/act1_takeover_evidence_2026-09-16/images-oldpc-desktop-06/04_writer_and_windows.png", "Старый ПК: несколько окон."),
    ("05_ui", "journal_photo_overview", f"{P}/act1_takeover_evidence_2026-09-16/images-len01-13/len01_arrival_photo_journal_1920x1080_overview.png", "Журнал: фото-разворот, общий вид."),
    ("05_ui", "journal_photo_detail", f"{P}/act1_takeover_evidence_2026-09-16/images-len01-13/len01_arrival_photo_journal_1920x1080_detail.png", "Журнал: фото-разворот, деталь."),
    ("05_ui", "journal_edge_sketch", f"{P}/act1_takeover_evidence_2026-09-16/images-len01-13/len01_edge_sketch_journal_1920x1080_overview.png", "Журнал: набросок кромки леса."),
    ("05_ui", "notebook_draft", f"{P}/act1_takeover_evidence_2026-09-16/images-notebook-shop-11/01_notebook_newer_draft.png", "Блокнот: черновик записи."),
    ("05_ui", "shop_purchases", f"{P}/act1_takeover_evidence_2026-09-16/images-notebook-shop-11/02_shop_purchases.png", "Магазин: покупки."),
    ("05_ui", "mosque_notebook", f"{P}/act1_takeover_evidence_2026-09-16/images-mosque-continuous-01/09a_mosque_observation_notebook.png", "Запись наблюдения в мечети."),
    ("05_ui", "bath_notebook", f"{P}/act1_takeover_evidence_2026-09-16/images-facilities-11/04b_bath_observation_notebook.png", "Запись наблюдения в бане."),
    ("05_ui", "manual_read_notebook", f"{P}/act1_takeover_evidence_2026-09-16/images-address-world-08/05_manual_read_notebook.png", "Ручное чтение (ноутбук/журнал)."),

    # ============ 06 NIGHT / FOREST / ATMOSPHERE ============
    ("06_night_forest_atmosphere", "forest_walk", f"{P}/visual_rework_2026-09-30/prologue_watch/forest_walk.png", "Ночной лес: туман, деревья, глубина."),
    ("06_night_forest_atmosphere", "niva_handover", f"{P}/visual_rework_2026-09-30/prologue_watch/niva_handover.png", "Ночная передача у Нивы."),
    ("06_night_forest_atmosphere", "peripheral_presence", f"{P}/visual_rework_2026-09-30/prologue_watch/peripheral_presence.png", "Периферийное присутствие в лесу (хоррор-момент)."),
    ("06_night_forest_atmosphere", "square_eye", f"{P}/visual_rework_2026-09-30/captures/square_eye.png", "Площадь с высоты глаз."),
    ("06_night_forest_atmosphere", "square_high", f"{P}/visual_rework_2026-09-30/captures/square_high.png", "Площадь сверху: композиция центра деревни."),
    ("06_night_forest_atmosphere", "new_layout", f"{P}/visual_rework_2026-09-29/captures/new_layout.png", "Новая планировка: общий вид."),
    ("06_night_forest_atmosphere", "old_layout", f"{P}/visual_rework_2026-09-29/captures/old_layout.png", "Старая планировка: с чем сравнивать."),
    ("06_night_forest_atmosphere", "north_street", f"{P}/visual_rework_2026-09-29/captures/north_street.png", "Северная улица."),
    ("06_night_forest_atmosphere", "yard_approach", f"{P}/a01_ex13_yard_2026-09-28/frames_yard_after/yard-approach--19-0.png", "Подход к двору EX13 после правки."),
    ("06_night_forest_atmosphere", "yard_board_approach", f"{P}/a01_ex13_yard_2026-09-28/frames_yard_after/yard-board-approach.png", "Подход к доске ворот (обход)."),
    ("06_night_forest_atmosphere", "rear_house_gap", f"{P}/a01_ex13_yard_2026-09-28/frames_ex13_chain/rear-house-gap--24,3--2,85.png", "Щель за домом: как читается проход."),
    ("06_night_forest_atmosphere", "neighbour_doors", f"{P}/act1_takeover_evidence_2026-09-16/images-address-standalone-access-05/14_ArrivalReverseEastDomesticShed_door_support_neighbours.png", "Сарай и соседние двери: детализация бытовых построек."),

    # ============ 07 VEHICLE / CUTSCENE ============
    ("07_vehicle_cutscene", "car_front_left", f"{P}/visual_rework_2026-09-30/ride_capture/car_front_left.png", "Машина спереди-слева в поездке."),
    ("07_vehicle_cutscene", "car_side_driver_window", f"{P}/visual_rework_2026-09-30/ride_capture/car_side_driver_window.png", "Вид сбоку через окно водителя."),
    ("07_vehicle_cutscene", "car_top_cabin", f"{P}/visual_rework_2026-09-30/ride_capture/car_top_cabin.png", "Салон сверху: материалы, швы."),
    ("07_vehicle_cutscene", "car_rear_over_seats", f"{P}/visual_rework_2026-09-30/ride_capture/car_rear_over_seats.png", "Салон сзади через сиденья."),
    ("07_vehicle_cutscene", "cabin_forward", f"{P}/visual_rework_2026-09-30/ride_capture/cabin_forward.png", "Вид вперёд из салона (как в игре)."),
    ("07_vehicle_cutscene", "cabin_driver_close", f"{P}/visual_rework_2026-09-30/ride_capture/cabin_driver_close.png", "Водитель крупно (руки, лицо)."),
    ("07_vehicle_cutscene", "cabin_driver_wide", f"{P}/visual_rework_2026-09-30/ride_capture/cabin_driver_wide.png", "Водитель общим планом."),
    ("07_vehicle_cutscene", "ride_shake_a", f"{P}/visual_rework_2026-09-30/ride_capture/shake_a.png", "Тряска камеры, фаза A."),
    ("07_vehicle_cutscene", "ride_shake_b", f"{P}/visual_rework_2026-09-30/ride_capture/shake_b.png", "Тряска камеры, фаза B."),
    ("07_vehicle_cutscene", "niva_outside", f"{P}/act1_takeover_evidence_2026-09-16/images-vehicle-08/babay-niva-outside.png", "Нива бабая снаружи."),
    ("07_vehicle_cutscene", "niva_full_side", f"{P}/act1_takeover_evidence_2026-09-16/images-vehicle-08/babay-niva-full-side.png", "Нива в профиль: пропорции и колёса."),
    ("07_vehicle_cutscene", "niva_driver", f"{P}/act1_takeover_evidence_2026-09-16/images-vehicle-08/babay-niva-driver.png", "Бабай за рулём."),
    ("07_vehicle_cutscene", "niva_wheel_moving", f"{P}/act1_takeover_evidence_2026-09-16/images-vehicle-08/niva-wheel-moving-contact.png", "Колесо Нивы в движении: контакт с землёй."),
    ("07_vehicle_cutscene", "motorcycle_fap", f"{P}/mvp_tracker_2026-09-27/a09-motorcycle-rear-exit/motorcycle-fap-1080.png", "Мотоцикл у ФАП, 1080p."),
    ("07_vehicle_cutscene", "niva_babay_720", f"{P}/mvp_tracker_2026-09-27/a09-motorcycle-rear-exit/niva-babay-720.png", "Нива у дома бабая."),
]


def main():
    manifest = []
    missing = []
    os.makedirs(OUT, exist_ok=True)
    # очистка прежней сборки
    for name in os.listdir(OUT):
        p = os.path.join(OUT, name)
        if os.path.isdir(p):
            shutil.rmtree(p)
        else:
            os.remove(p)

    counters = {}
    for category, slug, rel, note in ITEMS:
        src = os.path.join(ROOT, rel)
        if not os.path.isfile(src):
            missing.append(rel)
            continue
        counters[category] = counters.get(category, 0) + 1
        idx = counters[category]
        cat_dir = os.path.join(OUT, category)
        os.makedirs(cat_dir, exist_ok=True)
        dst = os.path.join(cat_dir, f"{idx:03d}_{slug}.jpg")
        # sips: конвертация PNG -> JPEG, оригинальный размер
        r = subprocess.run(
            ["sips", "-s", "format", "jpeg", "-s", "formatOptions", "88",
             "-s", "dpiHeight", "72", "-s", "dpiWidth", "72", src, "--out", dst],
            capture_output=True, text=True)
        if r.returncode != 0 or not os.path.isfile(dst):
            missing.append(rel + "  (sips failed: %s)" % r.stderr.strip()[:200])
            continue
        manifest.append({
            "category": category,
            "index": idx,
            "file": os.path.relpath(dst, os.path.dirname(OUT)),
            "source": rel,
            "note": note,
            "bytes": os.path.getsize(dst),
        })

    with open(os.path.join(os.path.dirname(OUT), "_research", "pack_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)

    total = sum(m["bytes"] for m in manifest)
    print(f"собрано: {len(manifest)} файлов, {total/1024/1024:.1f} MB")
    for cat in sorted(counters):
        print(f"  {cat}: {counters[cat]}")
    if missing:
        print("\nНЕ НАЙДЕНО / ОШИБКИ:")
        for m in missing:
            print("  " + m)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
