using Godot;

namespace Urman.Godot.Tests;

public partial class VehicleSmokeTest
{
    private async Task CaptureWholeVehicle(VehicleController vehicle)
    {
        if(System.Environment.GetEnvironmentVariable("URMAN_VEHICLE_CAPTURE")!="1")return;
        var previous=GetViewport().GetCamera3D();
        var camera=new Camera3D{Name="VehicleArtProofCamera",Fov=56,Near=.05f};AddChild(camera);
        try
        {
            var subject=vehicle.ToGlobal(vehicle.Definition.HullCenter);
            foreach(var view in new[]{
                (Name:"front-three-quarter",Azimuth:150f),
                (Name:"full-side",Azimuth:90f),
                (Name:"rear-three-quarter",Azimuth:-32f)})
            {
                var rejected=new List<string>();
                Require(TryPlaceWholeVehicleCamera(vehicle,camera,view.Azimuth,rejected),
                    vehicle.Definition.Id+" has a clear complete "+view.Name+" camera, without hiding world geometry");
                camera.MakeCurrent();await Frames(3);
                await Capture(vehicle.Definition.Id+"-"+view.Name);
                _records.Add(new{kind="diagnostic-art-camera",vehicle=vehicle.Definition.Id,view=view.Name,
                    cameraFeet=camera.GlobalPosition.ToString(),target=subject.ToString(),fov=camera.Fov,
                    rejectedCandidates=rejected.Count,firstRejections=rejected.Take(6).ToArray(),
                    limit="actual world geometry; full hull framing, camera clearance and visibility rays; rendered appearance still requires visual review"});
            }
        }
        finally
        {
            if(previous is not null&&GodotObject.IsInstanceValid(previous))previous.MakeCurrent();
            camera.QueueFree();await Frames(2);
        }
    }

    private bool TryPlaceWholeVehicleCamera(VehicleController vehicle,Camera3D camera,float azimuth,List<string> rejected)
    {
        var definition=vehicle.Definition;
        var centre=definition.HullCenter;
        var half=definition.HullSize*.5f;
        var subject=vehicle.ToGlobal(centre);
        var corners=new List<Vector3>();
        foreach(var x in new[]{-1f,1f})foreach(var y in new[]{-1f,1f})foreach(var z in new[]{-1f,1f})
            corners.Add(vehicle.ToGlobal(centre+new Vector3(half.X*x,half.Y*y,half.Z*z)));
        // Rays avoid the contact edge at ground level: on a cross slope the
        // terrain legitimately touches the lowest hull corners. The upper and
        // belt-line endpoints still cover both ends and the whole visible side.
        var targets=new List<Vector3>{subject};
        foreach(var x in new[]{-.9f,.9f})foreach(var y in new[]{-.35f,.85f})foreach(var z in new[]{-.92f,.92f})
            targets.Add(vehicle.ToGlobal(centre+new Vector3(half.X*x,half.Y*y,half.Z*z)));
        var space=vehicle.GetWorld3D().DirectSpaceState;
        var exclude=new global::Godot.Collections.Array<Rid>{vehicle.GetRid(),_player.GetRid()};
        using var lens=new SphereShape3D{Radius=.13f};
        using var query=new PhysicsShapeQueryParameters3D{Shape=lens,CollisionMask=3,Margin=.01f,Exclude=exclude};
        using var ray=PhysicsRayQueryParameters3D.Create(Vector3.Zero,Vector3.One,3,exclude);
        var size=GetViewport().GetVisibleRect().Size;
        var distance=Math.Max(3.1f,definition.HullSize.Z*.76f);
        foreach(var fov in new[]{56f,64f})
        foreach(var height in new[]{2.0f,2.7f,3.4f})
        foreach(var offset in new[]{0f,-12f,12f,-24f,24f,-36f,36f})
        foreach(var side in new[]{1f,-1f})
        foreach(var scale in new[]{1f,1.18f,.86f})
        {
            var angle=Mathf.DegToRad((azimuth+offset)*side);
            camera.Fov=fov;
            camera.GlobalPosition=vehicle.ToGlobal(new(Mathf.Sin(angle)*distance*scale,height,
                centre.Z+Mathf.Cos(angle)*distance*scale));
            camera.LookAt(subject,Vector3.Up);
            if(corners.Any(point=>!camera.IsPositionInFrustum(point)
                ||camera.UnprojectPosition(point).X<16||camera.UnprojectPosition(point).Y<16
                ||camera.UnprojectPosition(point).X>size.X-16||camera.UnprojectPosition(point).Y>size.Y-16))
            { rejected.Add("full hull outside camera framing");continue; }
            query.Transform=new(Basis.Identity,camera.GlobalPosition);
            var contact=space.IntersectShape(query,1);
            if(contact.Count>0)
            { rejected.Add("camera overlaps "+ContactPath(contact[0]));continue; }
            string? blocker=null;
            foreach(var target in targets)
            {
                ray.From=camera.GlobalPosition;ray.To=target;
                var hit=space.IntersectRay(ray);
                if(hit.Count==0)continue;
                blocker=ContactPath(hit);break;
            }
            if(blocker is not null){rejected.Add("view blocked by "+blocker);continue;}
            return true;
        }
        return false;
    }

    private static string ContactPath(global::Godot.Collections.Dictionary contact)
        =>contact["collider"].AsGodotObject() is Node owner?owner.GetPath().ToString():"unresolved collider";

    private void ValidateRinatParkingClearance(VehicleController vehicle)
    {
        if(vehicle.Definition.Kind!=VehicleKind.Niva)return;
        var actor=_demo.FindChildren("Npc_rinat","Node3D",true,false).OfType<Node3D>().SingleOrDefault()
            ??throw new InvalidOperationException("Rinat's existing presentation owner is absent from the arrival world.");
        Require(actor.IsVisibleInTree()&&actor.GetMeta("rinatStage").AsString()=="village",
            "Niva parking is checked against Rinat's actual ordinary village stage");
        var inverse=vehicle.GlobalTransform.AffineInverse();
        var actorInverse=actor.GlobalTransform.AffineInverse();
        var low=new Vector3(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity);
        var high=new Vector3(float.NegativeInfinity,float.NegativeInfinity,float.NegativeInfinity);
        var anchorLow=low;var anchorHigh=high;
        var native=DisplayServer.GetName()!="headless";
        var meshes=new List<object>();
        var count=0;
        foreach(var mesh in actor.FindChildren("*","MeshInstance3D",true,false).OfType<MeshInstance3D>())
        {
            if(!mesh.IsVisibleInTree()||mesh.Mesh is null)continue;
            var skeleton=mesh.GetNodeOrNull<Skeleton3D>(mesh.Skeleton);
            var skin=mesh.GetSkinReference()?.GetSkin()??mesh.Skin;
            if(skin is not null&&skeleton is null)
                throw new InvalidOperationException($"{mesh.Name} parking skin has no live skeleton.");
            var skinned=skeleton is not null&&skin is not null;
            List<Vector3> vertices;
            if(skinned&&native)
            {
                // GetAabb is the imported bind-pose box; it can still sit at the
                // character bank origin after the renderer has skinned the NPC.
                // Follow the native NPC proof's actual rendered-pose contract.
                using var baked=mesh.BakeMeshFromCurrentSkeletonPose();
                vertices=Enumerable.Range(0,baked.GetSurfaceCount()).SelectMany(surface=>
                    baked.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                    .Select(vertex=>mesh.GlobalTransform*vertex).ToList();
            }
            else if(skinned)vertices=SkinnedVertices(mesh,skeleton!,skin!);
            else vertices=Enumerable.Range(0,mesh.Mesh.GetSurfaceCount()).SelectMany(surface=>
                mesh.Mesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                .Select(vertex=>mesh.GlobalTransform*vertex).ToList();
            Require(vertices.Count>0&&vertices.All(vertex=>vertex.IsFinite()),
                "Rinat parking has finite posed vertices for "+mesh.Name);
            var partLow=new Vector3(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity);
            var partHigh=new Vector3(float.NegativeInfinity,float.NegativeInfinity,float.NegativeInfinity);
            foreach(var vertex in vertices)
            {
                var point=inverse*vertex;low=low.Min(point);high=high.Max(point);
                var anchor=actorInverse*vertex;anchorLow=anchorLow.Min(anchor);anchorHigh=anchorHigh.Max(anchor);
                partLow=partLow.Min(anchor);partHigh=partHigh.Max(anchor);
            }
            meshes.Add(new{mesh=mesh.GetPath().ToString(),vertexCount=vertices.Count,
                mode=skinned?(native?"native-current-skeleton-pose":"headless-live-skin-pose"):"unskinned-world-vertices",
                skeleton=skeleton?.GetPath().ToString(),skinBinds=skin?.GetBindCount()??0,
                boundsAtActor=new{minimum=partLow.ToString(),maximum=partHigh.ToString()}});
            count++;
        }
        Require(count>0,"Rinat clearance uses actual visible actor meshes");
        var hull=new Aabb(vehicle.Definition.HullCenter-vehicle.Definition.HullSize*.5f,vehicle.Definition.HullSize);
        var plausibleAtActor=anchorLow.X>=-1.2f&&anchorHigh.X<=1.2f
            &&anchorLow.Z>=-1.2f&&anchorHigh.Z<=1.2f&&anchorLow.Y>=-.35f
            &&anchorHigh.Y>=1.3f&&anchorHigh.Y<=2.5f;
        var clearsHull=!hull.Grow(.05f).Intersects(new Aabb(low,high-low));
        _records.Add(new{kind="authored-npc-parking-clearance",vehicle=vehicle.Definition.Id,
            npc=actor.GetPath().ToString(),stage=actor.GetMeta("rinatStage").AsString(),
            npcFeet=actor.GlobalPosition.ToString(),vehicleFeet=vehicle.GlobalPosition.ToString(),
            actorBoundsInVehicle=new{minimum=low.ToString(),maximum=high.ToString()},visibleMeshes=count,
            actorLocalBounds=new{minimum=anchorLow.ToString(),maximum=anchorHigh.ToString()},
            plausibleAtActor,clearsHull,meshes,
            limit="current posed presentation bounds and parking; headless uses live skin math, not rendered evidence; not a dynamic vehicle-versus-person contact test"});
        Require(plausibleAtActor,"Rinat posed bounds surround his actual actor anchor, not the imported character bank");
        Require(clearsHull,"authored Niva parking clears the existing Rinat posed presentation bounds");

        static List<Vector3> SkinnedVertices(MeshInstance3D mesh,Skeleton3D skeleton,Skin skin)
        {
            // Same live-bind fallback as Act1NpcPresentationSmokeTest. A native
            // run always uses BakeMeshFromCurrentSkeletonPose above.
            var transforms=new Transform3D[skin.GetBindCount()];
            for(var bind=0;bind<transforms.Length;bind++)
            {
                var name=skin.GetBindName(bind).ToString();
                var bone=name.Length>0?skeleton.FindBone(name):skin.GetBindBone(bind);
                if(bone<0||bone>=skeleton.GetBoneCount())
                    throw new InvalidOperationException($"{mesh.Name} parking skin bind {bind} has no live bone.");
                transforms[bind]=skeleton.GlobalTransform*skeleton.GetBoneGlobalPose(bone)*skin.GetBindPose(bind);
            }
            var result=new List<Vector3>();
            for(var surface=0;surface<mesh.Mesh.GetSurfaceCount();surface++)
            {
                var arrays=mesh.Mesh.SurfaceGetArrays(surface);
                var vertices=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var bones=arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
                var weights=arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
                if(vertices.Length==0||bones.Length!=weights.Length||weights.Length%vertices.Length!=0)
                    throw new InvalidOperationException($"{mesh.Name} has invalid parking skin arrays.");
                var stride=weights.Length/vertices.Length;
                for(var vertex=0;vertex<vertices.Length;vertex++)
                {
                    var point=Vector3.Zero;var total=0f;
                    for(var influence=0;influence<stride;influence++)
                    {
                        var offset=vertex*stride+influence;
                        if(weights[offset]<=0)continue;
                        if(bones[offset]<0||bones[offset]>=transforms.Length)
                            throw new InvalidOperationException($"{mesh.Name} references a missing parking skin bind.");
                        point+=(transforms[bones[offset]]*vertices[vertex])*weights[offset];total+=weights[offset];
                    }
                    if(total<.99f||total>1.01f)
                        throw new InvalidOperationException($"{mesh.Name} parking skin weights sum to {total}.");
                    result.Add(point/total);
                }
            }
            return result;
        }
    }
}
