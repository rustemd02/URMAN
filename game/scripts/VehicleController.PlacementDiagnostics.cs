using Godot;
using System.Text.Json.Nodes;

namespace Urman.Godot;

public partial class VehicleController
{
    private global::Godot.Collections.Array<global::Godot.Collections.Dictionary> PlacementOverlaps(Transform3D pose,int maximum,float? steering=null,float? lean=null)
        =>VolumeOverlaps(pose,steering??_steering,PlacementExcluded(),maximum,leanRadians:lean);

    private static JsonObject DescribePlacementContact(global::Godot.Collections.Dictionary hit)
    {
        var collider=hit["collider"].AsGodotObject() as Node;
        var index=hit["shape"].AsInt32();
        var shape=collider is CollisionObject3D body
            ?body.ShapeOwnerGetOwner(body.ShapeFindOwner(index)) as Node:null;
        var source=shape?.HasMeta("authoredSourceMesh")==true?shape:
            collider?.HasMeta("authoredSourceMesh")==true?collider:null;
        return new JsonObject{
            ["vehicleShape"]=hit.ContainsKey("vehicleShape")?hit["vehicleShape"].AsString():null,
            ["colliderPath"]=collider?.GetPath().ToString(),["shapeIndex"]=index,
            ["shapePath"]=shape?.GetPath().ToString(),
            ["shapeType"]=(shape as CollisionShape3D)?.Shape?.GetClass().ToString(),
            ["collisionOwner"]=collider?.HasMeta("collisionOwner")==true?collider.GetMeta("collisionOwner").AsString():null,
            ["authoredSourceMesh"]=source?.GetMeta("authoredSourceMesh").AsString()};
    }

    // The exact production hull, mask and exclusions are queried before any
    // recovery moves a vehicle. This is a read-only diagnostic, not another
    // source of parking geometry or an alternate acceptance condition.
    internal JsonObject DescribePlacementProbe(Transform3D pose)
    {
        static JsonArray V(Vector3 value)=>new(value.X,value.Y,value.Z);
        var overlaps=new JsonArray();
        foreach(var hit in PlacementOverlaps(pose,8))overlaps.Add(DescribePlacementContact(hit));
        var supports=new JsonArray();
        foreach(var local in SupportPoints())
        {
            var bottom=pose*local;
            using var ray=PhysicsRayQueryParameters3D.Create(bottom+Vector3.Up*.24f,bottom-Vector3.Up*.42f,CollisionMask);
            ray.Exclude=PlacementExcluded();
            var hit=GetWorld3D().DirectSpaceState.IntersectRay(ray);
            var sample=new JsonObject{["bottom"]=V(bottom),["hit"]=hit.Count!=0};
            if(hit.Count!=0)
            {
                var position=hit["position"].AsVector3();
                sample["position"]=V(position);sample["normal"]=V(hit["normal"].AsVector3());
                sample["gapMetres"]=bottom.Y-position.Y;sample["contact"]=DescribePlacementContact(hit);
            }
            supports.Add(sample);
        }
        var valid=ValidatePhysicalPlacement(pose,out var reason);
        return new JsonObject{["vehicleId"]=Definition.Id,["origin"]=V(pose.Origin),["basis"]=pose.Basis.ToString(),
            ["hullCenter"]=V(Definition.HullCenter),["hullSize"]=V(Definition.HullSize),
            ["valid"]=valid,["reason"]=reason,["overlaps"]=overlaps,["supports"]=supports,
            ["steering"]=DescribeSteeringCollision(),["compound"]=DescribeCompoundCollision()};
    }
}
