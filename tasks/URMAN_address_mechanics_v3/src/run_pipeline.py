from pathlib import Path
import json
import math
import re
from copy import deepcopy
from mechanics import build_road_graph,audit_world,assign_addresses,pin_road_vertices,stable_id

def validate_schema(value, schema, path="$"):
    """Validate the vocabulary used by the supplied schema without a network dependency.

    Unknown keywords fail closed. This is intentionally not advertised as a general
    JSON Schema implementation; the package schema uses only the keywords below.
    """
    supported={"$schema","title","description","type","required","properties","items","minItems","maxItems","enum","additionalProperties","minLength","pattern","minimum","exclusiveMinimum","uniqueItems"}
    if isinstance(schema,bool):
        if not schema:raise ValueError(f"Schema rejects value at {path}")
        return
    if set(schema)-supported:
        raise ValueError(f"Unsupported schema keywords at {path}: {set(schema)-supported}")
    kinds={"object":dict,"array":list,"string":str,"number":(int,float),"integer":int,"boolean":bool,"null":type(None)}
    expected=schema.get("type")
    if expected:
        expected=[expected] if isinstance(expected,str) else expected
        if any(t not in kinds for t in expected):raise ValueError(f"Unsupported schema type at {path}")
        if not any(isinstance(value,kinds[t]) and not (t in ("number","integer") and isinstance(value,bool)) for t in expected):
            raise ValueError(f"Schema type mismatch at {path}: expected {expected}")
    if "enum" in schema and not any(value==option and isinstance(value,bool)==isinstance(option,bool) for option in schema["enum"]):
        raise ValueError(f"Schema enum mismatch at {path}")
    if isinstance(value,(int,float)) and not isinstance(value,bool):
        if not math.isfinite(value):raise ValueError(f"Non-finite JSON number at {path}")
        if "minimum" in schema and value<schema["minimum"]:raise ValueError(f"Schema minimum at {path}")
        if "exclusiveMinimum" in schema and value<=schema["exclusiveMinimum"]:raise ValueError(f"Schema exclusive minimum at {path}")
    if isinstance(value,str):
        if len(value)<schema.get("minLength",0):raise ValueError(f"Schema string length at {path}")
        if "pattern" in schema and re.search(schema["pattern"],value) is None:raise ValueError(f"Schema pattern at {path}")
    if isinstance(value,dict):
        for key in schema.get("required",[]):
            if key not in value: raise ValueError(f"Schema required property missing: {path}.{key}")
        for key,child in schema.get("properties",{}).items():
            if key in value: validate_schema(value[key],child,f"{path}.{key}")
        for key in value.keys()-schema.get("properties",{}).keys():
            additional=schema.get("additionalProperties",True)
            if additional is False:raise ValueError(f"Schema additional property forbidden: {path}.{key}")
            if isinstance(additional,dict):validate_schema(value[key],additional,f"{path}.{key}")
    if isinstance(value,list):
        if len(value)<schema.get("minItems",0) or len(value)>schema.get("maxItems",float("inf")):
            raise ValueError(f"Schema array size mismatch at {path}")
        for index,child in enumerate(value):
            if "items" in schema: validate_schema(child,schema["items"],f"{path}[{index}]")
        if schema.get("uniqueItems") and len({json.dumps(x,sort_keys=True,allow_nan=False) for x in value})!=len(value):
            raise ValueError(f"Schema duplicate array item at {path}")

def check_schema(schema,path="$"):
    """Check every schema branch, including optional properties absent in input."""
    if isinstance(schema,bool):return
    if not isinstance(schema,dict):raise ValueError(f"Invalid schema at {path}")
    probe={key:value for key,value in schema.items() if key not in ("type","required","enum","minLength","pattern","minimum","exclusiveMinimum")}
    validate_schema(None,probe,path)
    for name,child in schema.get("properties",{}).items():check_schema(child,path+"."+name)
    if "items" in schema:check_schema(schema["items"],path+"[]")
    if "additionalProperties" in schema:check_schema(schema["additionalProperties"],path+".*")

def load_json(path):
    def pairs(values):
        result={}
        for key,value in values:
            if key in result:raise ValueError("Duplicate JSON property: "+key)
            result[key]=value
        return result
    def invalid(value):raise ValueError("Non-finite JSON number: "+value)
    return json.loads(Path(path).read_text(encoding="utf-8"),object_pairs_hook=pairs,parse_constant=invalid)

def run(input_path,root=None):
    p=Path(input_path)
    world=load_json(p)
    root=Path(root) if root else Path(__file__).resolve().parents[1]
    catalog=load_json(root/"data/street_catalog.json")
    rules=load_json(root/"data/mechanics_rules.json")
    # v3's old enriched artifact used source_map_id. Accept it once as an
    # explicit input migration and emit map_id from now on.
    if "map_id" not in world and "source_map_id" in world:
        world["map_id"]=world["source_map_id"]
    schema=load_json(root/"schemas/existing_map_input.schema.json")
    check_schema(schema)
    validate_schema(world,schema)
    world["roads"]=pin_road_vertices(world["roads"])
    graph=build_road_graph(world["roads"],constraints=world.get("constraints",{}),previous_graph=world.get("road_graph"))
    buildings,addresses=assign_addresses(world,graph,catalog,rules)
    enriched=deepcopy(world)
    enriched.update(buildings=buildings,addresses=addresses)
    audit=audit_world(enriched,graph,rules=rules)
    proposals=[]
    for issue in audit:
        if issue["severity"]=="INFO":continue
        proposals.append({
            "proposal_id":stable_id("PROP",issue["code"]+"/"+str(issue.get("entity","world"))),
            "severity":issue["severity"],
            "reason":issue["message"],
            "affected":[issue.get("entity")] if issue.get("entity") else [],
            "automatic_apply":False,
            "suggestion":"Inspect in editor; preserve authored geometry unless connectivity/access is genuinely broken."
        })
    out=deepcopy(world)
    out.update({
        "schema_version":"3.0",
        "map_id":world["map_id"],
        "source_map_id":world.get("map_id","unknown"),
        "roads":world["roads"],
        "streets":world.get("streets",[]),
        "road_graph":graph,
        "buildings":buildings,
        "addresses":addresses,
        "audit":audit,
        "repair_proposals":proposals,
        "visual_assets":{"status":"NOT_INCLUDED","generator":"Astra 6"}
    })
    validate_schema(out,schema)
    return out

if __name__=="__main__":
    root=Path(__file__).resolve().parents[1]
    out=run(root/"sample/existing_map_input.json",root)
    (root/"sample/existing_map_enriched.json").write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding="utf-8")
    print("audit issues:",len(out["audit"]))
    print("addresses:",len(out["addresses"]))
