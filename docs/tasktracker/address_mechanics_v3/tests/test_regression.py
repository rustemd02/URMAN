"""Executable reference regressions. These do not certify the Godot world."""
import copy
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import math

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/"src"))
from run_pipeline import run,validate_schema,check_schema
from mechanics import build_road_graph, components, normalize_house_number,dijkstra,nearest_road_access,resolve_address

class AddressReferenceTests(unittest.TestCase):
    def pipeline(self,world):
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)/"input.json"
            path.write_text(json.dumps(world,ensure_ascii=False),encoding="utf-8")
            return run(path,ROOT)
    def sample(self):
        return json.loads((ROOT/"sample/existing_map_input.json").read_text(encoding="utf-8"))
    def straight_street(self):
        return {"map_id":"regression","streets":[{"id":"tukay","odd_side":"left"}],
            "roads":[{"id":"main","street_id":"tukay","points":[[0,0],[30,0]]}],
            "buildings":[{"id":"house"+str(x),"position_xz":[x,-5],"entrance_street_id":"tukay"} for x in (5,15,25)]}
    def test_repeat_build_preserves_all_records_and_constraints(self):
        first=self.pipeline(self.sample())
        second=self.pipeline(first)
        self.assertEqual(first,second)
        self.assertEqual(8,len(second["addresses"]))
        self.assertEqual(first["constraints"],self.sample()["constraints"])
        self.assertTrue(any(i["code"]=="ACCESS_NOT_PHYSICALLY_VERIFIED" for i in second["audit"]))
    def test_legacy_enriched_migration_keeps_records(self):
        old=json.loads((ROOT/"sample/existing_map_enriched.json").read_text(encoding="utf-8"))
        migrated=self.pipeline(old)
        self.assertEqual({a["address_id"] for a in old["addresses"]},{a["address_id"] for a in migrated["addresses"]})
        self.assertEqual(migrated,self.pipeline(migrated))
    def test_input_order_and_reversed_polylines_keep_graph_and_entities(self):
        original=self.sample();changed=copy.deepcopy(original)
        changed["roads"].reverse();changed["buildings"].reverse()
        for road in changed["roads"]:road["points"].reverse()
        a=self.pipeline(original);b=self.pipeline(changed)
        self.assertEqual(a["road_graph"],b["road_graph"])
        self.assertEqual(a["buildings"],b["buildings"])
        self.assertEqual(a["addresses"],b["addresses"])
    def test_committed_building_ids_survive_small_geometry_edit(self):
        first=self.pipeline(self.sample());changed=copy.deepcopy(first)
        for building in changed["buildings"]:building["position_xz"][0]+=.03
        second=self.pipeline(changed)
        self.assertEqual({b["id"]:(b["building_id"],b["parcel_id"],b.get("address_id"),b.get("house_number")) for b in first["buildings"]},
                         {b["id"]:(b["building_id"],b["parcel_id"],b.get("address_id"),b.get("house_number")) for b in second["buildings"]})
    def test_infill_uses_suffix_without_renumbering(self):
        first=self.pipeline(self.sample());changed=copy.deepcopy(first)
        changed["buildings"].append({"id":"new_house","source_key":"scene/new_house","position_xz":[-210,26],"addressable":True,"entrance_street_id":"tukay"})
        second=self.pipeline(changed)
        for old in first["addresses"]:self.assertIn(old,second["addresses"])
        new=next(b for b in second["buildings"] if b["id"]=="new_house")
        self.assertTrue(new["house_number"][-1].isalpha())
    def test_normalized_duplicates_fail(self):
        world=self.sample()
        world["buildings"][1]["legacy_address"]={"street_id":"tukay","house_number":" 01 "}
        with self.assertRaisesRegex(ValueError,"Duplicate normalized address"):self.pipeline(world)
        self.assertEqual(normalize_house_number("07 a"),normalize_house_number("7А"))
    def test_invalid_schema_rejected(self):
        world=self.sample();world["roads"][0]["points"]=[[0,0]]
        with self.assertRaisesRegex(ValueError,"array size"):self.pipeline(world)
    def test_near_miss_is_not_rounded_connected(self):
        graph=build_road_graph([{"id":"a","points":[[0,0],[1,0]]},{"id":"b","points":[[1.01,0],[2,0]]}])
        self.assertEqual(2,len(components(graph)))
        self.assertTrue(all(e["a"]!=e["b"] for e in graph["edges"]))
    def test_collinear_partial_overlap_connects(self):
        roads=[{"id":"a","points":[[0,0],[10,0]]},{"id":"b","points":[[3,0],[7,0]]}]
        graph=build_road_graph(roads)
        self.assertEqual(1,len(components(graph)))
        self.assertEqual(4,len(graph["nodes"]))
        self.assertTrue(all(e["length"]>0 and e["a"]!=e["b"] for e in graph["edges"]))
    def test_missing_stable_source_is_rejected(self):
        world=self.sample();world["buildings"][0].pop("source_key");world["buildings"][0]["id"]=""
        with self.assertRaisesRegex(ValueError,"Stable source_key"):self.pipeline(world)
    def test_cadastral_collision_is_detected(self):
        world=self.pipeline(self.sample())
        world["buildings"][0]["game_cadastral_id"]=world["buildings"][1]["game_cadastral_id"]
        with self.assertRaisesRegex(ValueError,"Duplicate committed cadastral"):self.pipeline(world)
    def test_auxiliary_inherits_explicit_parent(self):
        world=self.sample();world["buildings"][-1]["parent_building_id"]="house_usal_52"
        out=self.pipeline(world);byid={b["id"]:b for b in out["buildings"]}
        self.assertEqual(byid["house_usal_52"]["parcel_id"],byid["shed_usal"]["parcel_id"])
        self.assertEqual(byid["house_usal_52"]["address_id"],byid["shed_usal"]["address_id"])
        self.assertEqual(out,self.pipeline(out))
    def test_initial_numbering_uses_regular_sequence_then_infill(self):
        first=self.pipeline(self.straight_street())
        self.assertEqual(["2","4","6"],[b["house_number"] for b in sorted(first["buildings"],key=lambda b:b["position_xz"][0])])
        first["buildings"].append({"id":"inserted","position_xz":[10,-5],"entrance_street_id":"tukay"})
        second=self.pipeline(first)
        self.assertEqual("2А",next(b["house_number"] for b in second["buildings"] if b["id"]=="inserted"))
        self.assertEqual({a["address_id"]:a for a in first["addresses"]},
                         {a["address_id"]:a for a in second["addresses"] if a in first["addresses"]})
    def test_enriched_points_only_reversal_recovers_pinned_vertices(self):
        first=self.pipeline(self.sample());changed=copy.deepcopy(first)
        for road in changed["roads"]:road["points"].reverse()
        second=self.pipeline(changed)
        self.assertEqual(first["road_graph"],second["road_graph"])
        self.assertEqual(first["addresses"],second["addresses"])
    def test_pinned_graph_ids_survive_endpoint_lexical_order_change(self):
        world=self.straight_street();world["roads"][0]["points"]=[[0,0],[0,30]]
        first=self.pipeline(world);changed=copy.deepcopy(first)
        changed["roads"][0]["points"][0][0]=.03
        second=self.pipeline(changed)
        self.assertEqual({n["id"] for n in first["road_graph"]["nodes"]},{n["id"] for n in second["road_graph"]["nodes"]})
        self.assertEqual(first["streets"][0]["origin_node"],second["streets"][0]["origin_node"])
        self.assertEqual(first["addresses"],second["addresses"])
    def test_vertex_keys_are_scoped_to_the_source_road(self):
        graph=build_road_graph([{"id":"a","points":[[0,0],[1,0]],"point_keys":["start","end"]},
                               {"id":"b","points":[[5,0],[6,0]],"point_keys":["start","end"]}])
        self.assertEqual(4,len({n["id"] for n in graph["nodes"]}))
        self.assertEqual(2,len(components(graph)))
    def test_orphan_committed_record_is_retained_and_audited(self):
        first=self.pipeline(self.straight_street());removed=first["buildings"].pop()
        old=next(a for a in first["addresses"] if a["address_id"]==removed["address_id"])
        second=self.pipeline(first)
        self.assertIn(old,second["addresses"])
        self.assertTrue(any(i["code"]=="ORPHAN_ADDRESS_RECORD" and i["entity"]==old["address_id"] for i in second["audit"]))
        self.assertEqual(second,self.pipeline(second))
    def test_inconsistent_committed_owner_is_rejected(self):
        world=self.pipeline(self.straight_street())
        world["addresses"][0]["parcel_id"]="PAR-another"
        with self.assertRaisesRegex(ValueError,"another parcel"):self.pipeline(world)
    def test_cadastral_serial_collision_probes_unused_serials(self):
        world=self.straight_street()
        with patch("mechanics._cadastral_start",return_value=7):
            first=self.pipeline(world)
            reverse=copy.deepcopy(world);reverse["buildings"].reverse()
            self.assertEqual(first["buildings"],self.pipeline(reverse)["buildings"])
            first["buildings"].append({"id":"inserted","position_xz":[10,-5],"entrance_street_id":"tukay"})
            second=self.pipeline(first)
        self.assertEqual({"URM-Q01-P0007","URM-Q01-P0008","URM-Q01-P0009"},
                         {b["game_cadastral_id"] for b in first["buildings"] if "game_cadastral_id" in b})
        self.assertEqual(4,len({b["game_cadastral_id"] for b in second["buildings"]}))
        for old in first["buildings"]:
            if "game_cadastral_id" in old:self.assertEqual(old["game_cadastral_id"],next(b["game_cadastral_id"] for b in second["buildings"] if b["building_id"]==old["building_id"]))
    def test_cadastral_quarter_mismatch_rejected(self):
        world=self.pipeline(self.straight_street());world["buildings"][0]["game_quarter_id"]="Q02"
        with self.assertRaisesRegex(ValueError,"quarter mismatch"):self.pipeline(world)
    def test_auxiliary_chain_is_order_independent_and_cycle_rejected(self):
        world=self.straight_street()
        world["buildings"] += [{"id":"shed","position_xz":[6,-6],"addressable":False,"parent_building_id":"bath"},
                              {"id":"bath","position_xz":[5,-7],"addressable":False,"parent_building_id":"house5"}]
        first=self.pipeline(world);reverse=copy.deepcopy(world);reverse["buildings"].reverse()
        self.assertEqual(first["buildings"],self.pipeline(reverse)["buildings"])
        byid={b["id"]:b for b in first["buildings"]}
        self.assertEqual(byid["house5"]["address_id"],byid["shed"]["address_id"])
        world["buildings"][-1]["parent_building_id"]="shed"
        with self.assertRaisesRegex(ValueError,"ownership cycle"):self.pipeline(world)
    def test_route_policy_respects_modes_gate_winter_width_and_surface(self):
        road={"id":"route","points":[[0,0],[10,0]],"modes":["foot","car"],"width_m":2.2,"surface":"ice","winter_blocked":True}
        graph=build_road_graph([road]);start=next(n["id"] for n in graph["nodes"] if n["x"]==0);end=next(n["id"] for n in graph["nodes"] if n["x"]==10)
        self.assertTrue(math.isinf(dijkstra(graph,start,mode="car")[end]))
        self.assertAlmostEqual(17,dijkstra(graph,start,mode="car",winter=False)[end])
        self.assertAlmostEqual(10,dijkstra(graph,start)[end])  # chainage is seasonal-state independent
        road.update(winter_blocked=False,width_m=1.0,gate_id="gate",gate_open=False)
        self.assertIsNone(nearest_road_access([5,0],build_road_graph([road]),mode="foot"))
        road["gate_open"]=True;graph=build_road_graph([road])
        self.assertIsNotNone(nearest_road_access([5,0],graph,mode="foot"))
        self.assertIsNone(nearest_road_access([5,0],graph,mode="car"))
    def test_explicit_constraint_closes_same_graph_and_preserves_input(self):
        road={"id":"route","points":[[0,0],[10,0]],"modes":["foot","car"]}
        constraints={"water":{"type":"polygon","points":[[4,-1],[6,-1],[6,1],[4,1]],"blocks_movement":True,"modes":["car"],"winter_only":True}}
        original=copy.deepcopy(constraints);graph=build_road_graph([road],constraints=constraints)
        self.assertIsNone(nearest_road_access([0,0],graph,mode="car"))
        self.assertIsNotNone(nearest_road_access([0,0],graph,mode="foot"))
        self.assertIsNotNone(nearest_road_access([0,0],graph,mode="car",winter=False))
        road["constraint_exemptions"]=["water"]
        self.assertIsNotNone(nearest_road_access([0,0],build_road_graph([road],constraints=constraints),mode="car"))
        self.assertEqual(original,constraints)
    def test_schema_enforces_additional_properties_and_checks_unused_branches(self):
        with self.assertRaisesRegex(ValueError,"additional property"):
            validate_schema({"unexpected":1},{"type":"object","additionalProperties":False})
        with self.assertRaisesRegex(ValueError,"type mismatch"):
            validate_schema({"a":"bad"},{"type":"object","additionalProperties":{"type":"number"}})
        with self.assertRaisesRegex(ValueError,"Unsupported schema keywords"):
            check_schema({"properties":{"absent":{"type":"string","made_up_keyword":True}}})
        with self.assertRaisesRegex(ValueError,"Non-finite"):
            world=self.sample();world["roads"][0]["points"][0][0]=float("nan");self.pipeline(world)
        with self.assertRaisesRegex(ValueError,"type mismatch"):
            world=self.sample();world["roads"][0]["blocked"]="false";self.pipeline(world)
    def test_duplicate_json_keys_are_rejected_before_enrichment(self):
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)/"input.json";path.write_text('{"map_id":"one","map_id":"two"}')
            with self.assertRaisesRegex(ValueError,"Duplicate JSON property"):run(path,ROOT)
    def test_usal_exception_preserved_and_distance_violation_reported(self):
        world=self.sample();first=self.pipeline(world)
        pair={a["house_number"] for a in first["addresses"] if a["street_id"]=="usal"}
        self.assertEqual({"15","52"},pair)
        world["buildings"][-2]["position_xz"]=[-350,300]
        second=self.pipeline(world)
        self.assertTrue(any(i["code"]=="USAL_PAIR_DISTANCE" for i in second["audit"]))
        self.assertEqual(pair,{a["house_number"] for a in second["addresses"] if a["street_id"]=="usal"})
        self.assertTrue(any(i["code"]=="USAL_FOREST_EDGE_OBSERVATION" for i in second["audit"]))
    def test_protected_catalog_names_cannot_silently_change(self):
        world=self.sample();world["streets"][0]["russian"]="ул. Новая"
        with self.assertRaisesRegex(ValueError,"Protected street catalog"):self.pipeline(world)
    def test_new_road_cannot_take_over_a_committed_node_identity(self):
        first=self.pipeline(self.straight_street());old={n["id"]:(n["x"],n["z"]) for n in first["road_graph"]["nodes"]}
        changed=copy.deepcopy(first)
        changed["roads"].append({"id":"aaa-earlier-key","street_id":"tukay","points":[[0,0],[0,10]]})
        second=self.pipeline(changed)
        for node in second["road_graph"]["nodes"]:
            if node["id"] in old:self.assertEqual(old[node["id"]],(node["x"],node["z"]))
        self.assertTrue(old.keys()<={n["id"] for n in second["road_graph"]["nodes"]})
        self.assertEqual(first["streets"][0]["origin_node"],second["streets"][0]["origin_node"])
        self.assertEqual(first["addresses"],second["addresses"])
        self.assertEqual(second,self.pipeline(second))
    def test_collinear_graph_rebuild_keeps_distinct_endpoint_bindings(self):
        roads=[{"id":"a","points":[[0,0],[10,0]]},{"id":"b","points":[[3,0],[7,0]]}]
        first=build_road_graph(roads)
        self.assertEqual(first,build_road_graph(roads,previous_graph=first))
    def test_migration_aliases_and_renamed_street_preserve_stable_resolution(self):
        first=self.pipeline(self.sample());changed=copy.deepcopy(first)
        record=next(a for a in changed["addresses"] if a["street_id"]=="urman")
        old_number=record["house_number"];record["history"].append({"street_id":"urman","house_number":old_number})
        record["house_number"]="99А"
        next(b for b in changed["buildings"] if b.get("address_id")==record["address_id"])["house_number"]="99А"
        next(s for s in changed["streets"] if s["id"]=="urman")["russian"]="ул. Лесная тропа"
        second=self.pipeline(changed)
        by_id=resolve_address(second["addresses"],reference=record["building_id"])
        by_old=resolve_address(second["addresses"],street_id="urman",house_number=old_number)
        by_new=resolve_address(second["addresses"],street_id="urman",house_number="099 a")
        self.assertEqual(by_id,by_old);self.assertEqual(by_id,by_new)
        self.assertEqual(second,self.pipeline(second))
    def test_usal_cannot_be_required_transit_to_an_ordinary_street(self):
        world={"map_id":"transit","streets":[{"id":s} for s in ("tukay","usal","urman")],
            "roads":[{"id":s,"street_id":s,"points":[[x,0],[x+10,0]]} for s,x in (("tukay",0),("usal",10),("urman",20))],
            "buildings":[{"id":"ordinary","position_xz":[25,-3],"entrance_street_id":"urman"}],
            "constraints":{"forest_edge":{"type":"line","points":[[10,5],[20,5]]}}}
        out=self.pipeline(world)
        self.assertTrue(any(i["code"]=="USAL_REQUIRED_TRANSIT" for i in out["audit"]))
    def test_invalid_explicit_constraint_and_zero_length_road_are_rejected(self):
        road={"id":"r","points":[[0,0],[10,0]]}
        with self.assertRaisesRegex(ValueError,"blocks_movement must be boolean"):
            build_road_graph([road],constraints={"water":{"blocks_movement":"false"}})
        with self.assertRaisesRegex(ValueError,"insufficient points"):
            build_road_graph([road],constraints={"water":{"blocks_movement":True,"type":"polygon","points":[[0,0],[1,0]]}})
        with self.assertRaisesRegex(ValueError,"non-degenerate"):
            build_road_graph([{"id":"r","points":[[0,0],[0,0]]}])

if __name__=="__main__":unittest.main(verbosity=2)
