using Godot;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urman.Godot.Tests;

public partial class VehicleSmokeTest
{
    private async Task RunWheelOnlyChecks(VehicleController vehicle)
    {
        await Approach(vehicle);await Press("interact");await Frames(4);
        Require(vehicle.Driver==_player&&_player.VehicleControlled,"wheel-only proof enters through the ordinary ray and action");
        await SteeringWheelCollisionChecks(vehicle);
        await Press("carry_use");await Frames(40);
        await MovingWheelCollisionChecks(vehicle);
        await Press("crouch");await Frames(35);
        Require(vehicle.TryExit()&&_player.CanStandAt(_player.GlobalPosition),
            "wheel-only proof finishes with an ordinary physically clear exit");
    }

    private async Task SteeringWheelCollisionChecks(VehicleController vehicle)
    {
        var limit=Mathf.DegToRad(vehicle.Definition.SteeringDegrees);
        StaticBody3D? post=null;
        try
        {
            Require(vehicle.Driver==_player&&vehicle.ParkingBrake&&!vehicle.EngineRunning,
                "wheel proof starts in the ordinarily entered parked Niva");
            await Frames(8);
            // 1.80 m tall: the roof rack is part of the car.
            var hull=vehicle.GetNode<CollisionShape3D>("ChassisCollision");
            Require(hull.Shape is BoxShape3D box&&box.Size.IsEqualApprox(new(1.82f,1.80f,4.2f)),
                "wheel repair retains the authored Niva chassis dimensions");
            Require(vehicle.GetChildren().OfType<CollisionShape3D>().Count()==3,
                "two front tyre shapes share the existing chassis body");
            CheckWheelMeshEnvelope(vehicle,allPhases:true);
            post=WheelPost(vehicle,new(.995f,.36f,-1.391f),"StationarySteeringPost");
            await Frames(3);
            var postPath=post.GetPath().ToString();
            Require(!WheelContacts(vehicle,vehicle.GlobalTransform,0,postPath).Any(),
                "straight wheels and chassis clear the actual 40mm post");
            var predicted=WheelContacts(vehicle,vehicle.GlobalTransform,limit,postPath);
            Require(predicted.Any(hit=>hit["vehicleShape"]!.GetValue<string>()=="FrontRightWheelCollision")
                &&predicted.All(hit=>hit["vehicleShape"]!.GetValue<string>()!="ChassisCollision"),
                "full lock would hit the post with the tyre while the unchanged chassis is clear");
            _records.Add(new{kind="stationary-wheel-post-fixture",post=postPath,pose=post.GlobalTransform.ToString(),
                size="0.04 x 0.72 x 0.04 m",predicted=predicted.Select(hit=>hit.ToJsonString()).ToArray(),
                limit="explicit local collision fixture; not a player traversal or art acceptance"});
            var before=vehicle.GlobalPosition;var stops=vehicle.SteeringContactStops;
            Input.ActionPress("move_right");await Frames(35);
            Require(vehicle.SteeringContactStops>stops&&vehicle.SteeringRadians>0&&vehicle.SteeringRadians<limit-.01f,
                "ordinary steering stops before the real post at rest");
            Require(new Vector2(vehicle.GlobalPosition.X-before.X,vehicle.GlobalPosition.Z-before.Z).Length()<.01f,
                "blocked steering does not displace the parked vehicle sideways");
            Require(!WheelContacts(vehicle,vehicle.GlobalTransform,vehicle.SteeringRadians,postPath).Any(),
                "accepted wheel pose has no full-shape overlap with the post");
            var refusal=vehicle.DescribeSteeringCollision();
            Require(refusal["lastContact"]?["colliderPath"]?.GetValue<string>()==postPath,
                "steering refusal names the actual post owner");
            CheckWheelMeshEnvelope(vehicle,allPhases:false);
            _records.Add(new{kind="actual-stationary-steering-contact",proof=refusal});
            await CaptureWheelFixture(vehicle,post,"niva-wheel-stationary-contact");
            var stoppedAngle=vehicle.SteeringRadians;
            Input.ActionRelease("move_right");Input.ActionPress("move_left");await Frames(12);
            Require(vehicle.SteeringRadians<stoppedAngle-.1f,
                "ordinary opposite steering leaves the obstruction without ignoring it");
            Input.ActionRelease("move_left");await Frames(35);
            post.QueueFree();post=null;await Frames(3);
            foreach(var action in new[]{"move_left","move_right"})
            {
                Input.ActionPress(action);await Frames(45);
                var expected=action=="move_left"?-limit:limit;
                Require(Math.Abs(vehicle.SteeringRadians-expected)<.0001f,
                    action+" reaches full lock after the post is removed");
                var physical=vehicle.GetNode<CollisionShape3D>("FrontRightWheelCollision");
                Require(Math.Sign((-physical.Basis.Z).X)==(action=="move_right"?1:-1),
                    "physical wheel rolling direction agrees with the ordinary steering action");
                CheckWheelMeshEnvelope(vehicle,allPhases:false);
                Input.ActionRelease(action);
            }

            // Hold the real action through load. The normal neutral-release gate
            // prevents self-centring from hiding the restored physical angle.
            Input.ActionPress("move_right");await Frames(4);
            const string slot="vehicle-smoke-niva-wheel-steering";
            var savedAngle=vehicle.SteeringRadians;
            Require(await _bridge.SaveSlotAsync(slot),"existing snapshot saves the physical steering angle");
            Require(await _bridge.LoadSlotAsync(slot),"ordinary occupied load restores the steering snapshot");
            await Frames(3);
            Require(vehicle.Driver==_player&&Math.Abs(vehicle.SteeringRadians-savedAngle)<.0001f,
                "held input cannot alter the restored steering angle before neutral release");
            var restored=vehicle.DescribeSteeringCollision();
            Require(restored["volumes"]!.AsArray().All(item=>Math.Abs(item!["colliderYaw"]!.GetValue<float>()+savedAngle)<.0001f
                &&Math.Abs(item["visualYaw"]!.GetValue<float>()+savedAngle)<.0001f),
                "saved steering restores both tyre colliders and visible wheels together");

            post=WheelPost(vehicle,new(.995f,.36f,-1.391f),"ChangedSavedTyrePost");await Frames(3);
            postPath=post.GetPath().ToString();
            Require(WheelContacts(vehicle,vehicle.GlobalTransform,savedAngle,postPath).Any(hit=>
                hit["vehicleShape"]!.GetValue<string>()=="FrontRightWheelCollision"),
                "changed saved obstacle occupies only the turned tyre region");
            Require(await _bridge.LoadSlotAsync(slot),"existing parking recovery handles a saved tyre obstruction");
            await Frames(3);
            Require(vehicle.HasMeta("rejectedPlacementProbe")&&vehicle.GetMeta("rejectedPlacementProbe").AsString().Contains("FrontRightWheelCollision",StringComparison.Ordinal),
                "saved-placement rejection includes the actual tyre shape");
            Require(vehicle.PlacementAvailable&&vehicle.ValidatePhysicalPlacement(out _)&&Math.Abs(vehicle.SteeringRadians)<.0001f,
                "parking recovery checks and commits straight wheels at a physically clear candidate");
            post.QueueFree();post=null;await Frames(3);
            Require(await _bridge.LoadSlotAsync(slot),"the same unmodified snapshot loads after removal of the tyre obstacle");
            Require(vehicle.Driver==_player&&Math.Abs(vehicle.SteeringRadians-savedAngle)<.0001f,
                "removing the obstacle restores the original saved angle and possession");
            Input.ActionRelease("move_right");

            Require(vehicle.TryExit(),"steering migration fixture starts from an ordinary safe exit");
            var original=vehicle.Capture();var legacy=(JsonObject)original.DeepClone();legacy.Remove("steeringRadians");
            using(var data=JsonDocument.Parse(legacy.ToJsonString()))Require(vehicle.Restore(data.RootElement),
                "legacy version1 vehicle record without steering remains readable");
            Require(Math.Abs(vehicle.SteeringRadians)<.0001f&&vehicle.DescribeSteeringCollision()["volumes"]!.AsArray()
                .All(item=>Math.Abs(item!["colliderYaw"]!.GetValue<float>())<.0001f),
                "legacy steering defaults to zero in the physical forms");
            var retained=vehicle.Capture().ToJsonString();var invalid=(JsonObject)legacy.DeepClone();invalid["steeringRadians"]=limit+.1f;
            using(var data=JsonDocument.Parse(invalid.ToJsonString()))Require(!vehicle.Restore(data.RootElement),
                "out-of-range steering rejects the record before mutation");
            Require(vehicle.Capture().ToJsonString()==retained,"rejected steering leaves the complete vehicle record unchanged");
            Require(await _bridge.LoadSlotAsync(slot),"ordinary snapshot restores possession after the explicit migration fixture");
            await Frames(40);
            Require(vehicle.Driver==_player&&Math.Abs(vehicle.SteeringRadians)<.0001f&&vehicle.ParkingBrake&&!vehicle.EngineRunning,
                "wheel fixture returns to the original parked controls without a second state owner");
        }
        finally
        {
            Input.ActionRelease("move_left");Input.ActionRelease("move_right");
            if(post is not null&&GodotObject.IsInstanceValid(post))post.QueueFree();
        }
    }

    private async Task MovingWheelCollisionChecks(VehicleController vehicle)
    {
        StaticBody3D? post=null;
        try
        {
            Require(vehicle.Driver==_player&&vehicle.EngineRunning&&!vehicle.ParkingBrake,
                "moving-wheel proof uses ordinary running controls");
            await Press("crouch");await Frames(10);
            Input.ActionPress("move_right");await Frames(40);
            var angle=vehicle.SteeringRadians;var before=vehicle.GlobalTransform;
            Require(Math.Abs(angle-Mathf.DegToRad(vehicle.Definition.SteeringDegrees))<.0001f,
                "moving-wheel proof starts with real full steering lock");
            // Predict only the fixture's position, never the vehicle. The next
            // 16 cm of the existing bicycle path brings the outer tyre here,
            // while the chassis remains inside the post's inner X face.
            const float distance=.16f;
            var yaw=-distance/vehicle.Definition.WheelBase*Mathf.Tan(angle);
            var basis=before.Basis.Rotated(Vector3.Up,yaw);
            var predictedPose=new Transform3D(basis,before.Origin-basis.Z*distance);
            var sidewall=new Vector3(.78f,.345f,-1.18f)+Basis.FromEuler(new(0,VehicleController.RoadWheelYaw(angle),0))*new Vector3(.095f,0,-.3036f);
            var predicted=predictedPose*sidewall;
            post=WheelPost(vehicle,vehicle.ToLocal(predicted)+new Vector3(.008f,.015f,0),"MovingTyrePost");await Frames(3);
            var postPath=post.GetPath().ToString();
            Require(!WheelContacts(vehicle,vehicle.GlobalTransform,angle,postPath).Any(),
                "moving-wheel fixture begins with a clear full vehicle volume");
            var anticipated=WheelContacts(vehicle,predictedPose,angle,postPath);
            Require(anticipated.Any(hit=>hit["vehicleShape"]!.GetValue<string>()=="FrontRightWheelCollision")
                &&anticipated.All(hit=>hit["vehicleShape"]!.GetValue<string>()!="ChassisCollision"),
                "the predicted short path isolates tyre contact from chassis contact");
            await Press("crouch");Input.ActionPress("move_forward");
            JsonObject? actual=null;
            for(var frame=0;frame<80&&actual is null;frame++)
            {
                await Frames(1);
                for(var i=0;i<vehicle.GetSlideCollisionCount();i++)
                {
                    var hit=vehicle.GetSlideCollision(i);
                    for(var j=0;j<hit.GetCollisionCount();j++)if(hit.GetCollider(j)==post)
                    {
                        actual=new JsonObject{["localShape"]=(hit.GetLocalShape(j) as Node)?.Name.ToString(),
                            ["colliderPath"]=postPath,["position"]=hit.GetPosition(j).ToString(),
                            ["normal"]=hit.GetNormal(j).ToString(),["vehicleOrigin"]=vehicle.GlobalPosition.ToString(),
                            ["speed"]=vehicle.Speed,["steering"]=vehicle.SteeringRadians};break;
                    }
                }
            }
            Input.ActionRelease("move_forward");Input.ActionPress("jump");
            Require(actual is not null&&actual["localShape"]?.GetValue<string>()=="FrontRightWheelCollision",
                "ordinary throttle produces an actual MoveAndSlide tyre-versus-post contact");
            Require(vehicle.GlobalPosition.DistanceTo(before.Origin)>.005f,
                "contact follows real vehicle movement rather than an overlapped fixture start");
            Require(Mathf.AngleDifference(before.Basis.GetEuler().Y,vehicle.Rotation.Y)<-.001f,
                "right-steering wheel contact follows an actual right turn of the chassis");
            Require(!WheelContacts(vehicle,vehicle.GlobalTransform,vehicle.SteeringRadians,postPath)
                .Any(hit=>hit["vehicleShape"]!.GetValue<string>()=="ChassisCollision"),
                "the chassis is still clear at the actual tyre contact");
            CheckWheelMeshEnvelope(vehicle,allPhases:false);
            _records.Add(new{kind="actual-moving-wheel-contact",contact=actual,fixturePose=post.GlobalTransform.ToString(),
                fullVolume=vehicle.DescribePhysicalVolumeContacts(vehicle.GlobalTransform,vehicle.SteeringRadians)});
            await CaptureWheelFixture(vehicle,post,"niva-wheel-moving-contact");
            await Frames(12);Input.ActionRelease("jump");
            var contactPosition=vehicle.GlobalPosition;
            Input.ActionPress("move_backward");await Frames(40);Input.ActionRelease("move_backward");
            Input.ActionPress("jump");await Frames(15);Input.ActionRelease("jump");
            Require(vehicle.GlobalPosition.DistanceTo(contactPosition)>.06f,
                "ordinary reverse safely leaves the tyre contact with the post still present");
            Require(!WheelContacts(vehicle,vehicle.GlobalTransform,vehicle.SteeringRadians,postPath).Any(),
                "reversing leaves the complete physical volume clear of the post");
            Input.ActionRelease("move_right");await Frames(35);
            Require(vehicle.ValidatePhysicalPlacement(out _),"movement proof ends with actual terrain support and clear shapes");
        }
        finally
        {
            foreach(var action in new[]{"move_forward","move_backward","move_left","move_right","jump"})Input.ActionRelease(action);
            if(post is not null&&GodotObject.IsInstanceValid(post))post.QueueFree();
        }
    }

    private StaticBody3D WheelPost(VehicleController vehicle,Vector3 local,string name)
    {
        var post=new StaticBody3D{Name=name,CollisionLayer=1,CollisionMask=0};
        var size=new Vector3(.04f,.72f,.04f);
        post.AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=size}});
        post.AddChild(new MeshInstance3D{Name="Exact40mmFixtureShell",Mesh=new BoxMesh{Size=size},MaterialOverride=new StandardMaterial3D{
            AlbedoColor=new(1f,.36f,.015f),ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded}});
        AddChild(post);post.GlobalTransform=new(vehicle.GlobalBasis,vehicle.ToGlobal(local));return post;
    }

    private JsonObject[] WheelContacts(VehicleController vehicle,Transform3D pose,float steering,string postPath)
        =>vehicle.DescribePhysicalVolumeContacts(pose,steering)["contacts"]!.AsArray().OfType<JsonObject>()
            .Where(hit=>hit["colliderPath"]?.GetValue<string>()==postPath).ToArray();

    private void CheckWheelMeshEnvelope(VehicleController vehicle,bool allPhases)
    {
        var root=vehicle.GetNode<Node3D>("VehicleVisual");
        var wheels=root.GetChildren().OfType<Node3D>().Where(node=>node.HasMeta("roadTyreRadius")&&node.Position.Z<0).ToArray();
        Require(wheels.Length==2,"actual front-wheel model bindings are available to the union audit");
        var angles=allPhases?new[]{-Mathf.DegToRad(vehicle.Definition.SteeringDegrees),0,Mathf.DegToRad(vehicle.Definition.SteeringDegrees)}
            :new[]{vehicle.SteeringRadians};
        var phases=allPhases?new[]{0f,Mathf.Pi/32,Mathf.Pi/16,Mathf.Pi/8,Mathf.Pi/4}:new[]{wheels[0].Rotation.X};
        var checkedVertices=0;var outside=0;var maximumX=0f;
        foreach(var wheel in wheels)foreach(var mesh in wheel.FindChildren("*","MeshInstance3D",true,false).OfType<MeshInstance3D>())
        {
            var relative=wheel.GlobalTransform.AffineInverse()*mesh.GlobalTransform;
            for(var surface=0;surface<mesh.Mesh!.GetSurfaceCount();surface++)
            foreach(var vertex in mesh.Mesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
            foreach(var angle in angles)foreach(var phase in phases)
            {
                var point=wheel.Position+Basis.FromEuler(new(phase,VehicleController.RoadWheelYaw(angle),0))*(relative*vertex);
                if(!vehicle.PhysicalEnvelopeContains(point,angle))outside++;
                maximumX=Math.Max(maximumX,Math.Abs(point.X));checkedVertices++;
            }
        }
        _records.Add(new{kind="actual-tyre-mesh-union",allPhases,angles,phases,checkedVertices,outside,maximumX,
            physical=vehicle.DescribeSteeringCollision(),limit="mesh vertices against the production convex envelope; contact is separately exercised through normal input"});
        Require(checkedVertices>100&&outside==0,"real tyre, rim and tread vertices fit the chassis-plus-wheel volume at the checked angles and phases");
        if(allPhases)Require(maximumX>1.017f,"actual turned geometry retains its authored track and protrusion beyond the old hull");
    }

    private async Task CaptureWheelFixture(VehicleController vehicle,StaticBody3D post,string name)
    {
        if(System.Environment.GetEnvironmentVariable("URMAN_VEHICLE_CAPTURE")!="1")return;
        var previous=GetViewport().GetCamera3D();var camera=new Camera3D{Name="WheelContactProofCamera",Fov=56,Near=.05f};AddChild(camera);
        try
        {
            // The whole-car camera may legitimately choose the opposite side.
            // This proof must show the actual right tyre and its exact post.
            var wheel=vehicle.ToGlobal(new(.78f,.345f,-1.18f));
            var subject=(wheel+post.GlobalPosition)*.5f+Vector3.Up*.05f;
            var visiblePost=post.GlobalPosition+Vector3.Up*.22f;
            var points=new List<Vector3>();
            foreach(var x in new[]{-.02f,.02f})foreach(var y in new[]{-.36f,.36f})foreach(var z in new[]{-.02f,.02f})
                points.Add(post.ToGlobal(new(x,y,z)));
            foreach(var y in new[]{-.35f,.35f})foreach(var z in new[]{-.36f,.36f})
                points.Add(wheel+vehicle.GlobalBasis*new Vector3(0,y,z));
            var space=vehicle.GetWorld3D().DirectSpaceState;
            var excluded=new global::Godot.Collections.Array<Rid>{_player.GetRid()};
            using var lens=new SphereShape3D{Radius=.08f};
            using var query=new PhysicsShapeQueryParameters3D{Shape=lens,CollisionMask=3,Margin=.01f,Exclude=excluded};
            using var ray=PhysicsRayQueryParameters3D.Create(Vector3.Zero,visiblePost,3,excluded);
            var viewport=GetViewport().GetVisibleRect().Size;var accepted=false;var rejected=new List<string>();
            foreach(var distance in new[]{1.1f,1.5f,1.9f})
            {
                foreach(var height in new[]{.30f,.50f,.70f})
                {
                    foreach(var forward in new[]{-.25f,-.55f,.25f,.55f})
                    {
                        camera.GlobalPosition=post.GlobalPosition+vehicle.GlobalBasis*new Vector3(distance,height,forward);
                        camera.LookAt(subject,Vector3.Up);query.Transform=new(Basis.Identity,camera.GlobalPosition);
                        if(space.IntersectShape(query,1).Count>0){rejected.Add("lens overlap");continue;}
                        if(points.Any(point=>!camera.IsPositionInFrustum(point)||camera.UnprojectPosition(point).X<24
                            ||camera.UnprojectPosition(point).Y<24||camera.UnprojectPosition(point).X>viewport.X-24
                            ||camera.UnprojectPosition(point).Y>viewport.Y-24)){rejected.Add("tyre or post outside framing");continue;}
                        ray.From=camera.GlobalPosition;var hit=space.IntersectRay(ray);
                        if(hit.Count==0||hit["collider"].AsGodotObject()!=post){rejected.Add("post face occluded");continue;}
                        accepted=true;break;
                    }
                    if(accepted)break;
                }
                if(accepted)break;
            }
            Require(accepted,"right tyre and exact 40mm fixture have a clear close-up camera and a visible post face");
            camera.MakeCurrent();await Frames(2);await Capture(name);
            var pixel=camera.UnprojectPosition(visiblePost)/viewport;
            Act1VisibleSurfaceProbe.Log(GetTree().Root,camera,"vehicle/"+name+"/actual-post",pixel);
            _records.Add(new{kind="wheel-contact-close-up",name,post=post.GetPath().ToString(),postPose=post.GlobalTransform.ToString(),
                shellSize="0.04 x 0.72 x 0.04 m; same as physical fixture",camera=camera.GlobalTransform.ToString(),
                actualSide=vehicle.ToLocal(camera.GlobalPosition).X,postPixel=pixel.ToString(),rejectedCandidates=rejected.Count,
                limit="diagnostic camera and contrasting exact-size shell; actual contact owner is recorded separately"});
        }
        finally
        {
            if(previous is not null&&GodotObject.IsInstanceValid(previous))previous.MakeCurrent();
            camera.QueueFree();
        }
    }
}
