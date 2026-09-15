from pathlib import Path
import json
from mechanics import build_road_graph,audit_world,assign_addresses

def run(input_path,root=None):
    p=Path(input_path)
    world=json.loads(p.read_text(encoding="utf-8"))
    root=Path(root) if root else Path(__file__).resolve().parents[1]
    catalog=json.loads((root/"data/street_catalog.json").read_text(encoding="utf-8"))
    rules=json.loads((root/"data/mechanics_rules.json").read_text(encoding="utf-8"))
    graph=build_road_graph(world["roads"])
    audit=audit_world(world,graph)
    buildings,addresses=assign_addresses(world,graph,catalog,rules)
    proposals=[]
    for issue in audit:
        proposals.append({
            "proposal_id":"PROP-"+issue["code"]+"-"+str(len(proposals)+1),
            "severity":issue["severity"],
            "reason":issue["message"],
            "affected":[issue.get("entity")] if issue.get("entity") else [],
            "automatic_apply":False,
            "suggestion":"Inspect in editor; preserve authored geometry unless connectivity/access is genuinely broken."
        })
    out={
        "schema_version":"3.0",
        "source_map_id":world.get("map_id","unknown"),
        "roads":world["roads"],
        "streets":world.get("streets",[]),
        "road_graph":graph,
        "buildings":buildings,
        "addresses":addresses,
        "audit":audit,
        "repair_proposals":proposals,
        "visual_assets":{"status":"NOT_INCLUDED","generator":"Astra 6"}
    }
    return out

if __name__=="__main__":
    root=Path(__file__).resolve().parents[1]
    out=run(root/"sample/existing_map_input.json",root)
    (root/"sample/existing_map_enriched.json").write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding="utf-8")
    print("audit issues:",len(out["audit"]))
    print("addresses:",len(out["addresses"]))
