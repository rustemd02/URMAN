using Godot;
using System.Text.Json.Nodes;

namespace Urman.Godot;

public partial class VehicleController
{
    private sealed record SteeringWheelVolume(Node3D Visual,CollisionShape3D Collider,Vector3 Centre,float Radius,float Width);
    private readonly List<SteeringWheelVolume> _steeringVolumes=new();
    private JsonObject? _lastSteeringContact;
    private float _lastRejectedSteering;
    private float _lastRejectedLean;
    private float _lastAcceptedLeanBeforeContact;
    internal float SteeringRadians=>_steering;
    // Positive input turns the bicycle path right; Godot's forward is -Z,
    // so the road wheels' actual yaw has the opposite sign to that input.
    internal static float RoadWheelYaw(float steering)=>-steering;
    internal int SteeringContactStops { get; private set; }

    private void BuildSteeringCollision()
    {
        if(Definition.Kind==VehicleKind.HorseCart)return;
        foreach(var wheel in _visual.FrontWheels)
        {
            var radius=wheel.GetMeta("roadTyreRadius").AsSingle();
            var width=wheel.GetMeta("roadTyreWidth").AsSingle();
            var collider=new CollisionShape3D{
                Name=Definition.Kind==VehicleKind.Motorcycle?"FrontWheelCollision"
                    :wheel.Position.X<0?"FrontLeftWheelCollision":"FrontRightWheelCollision",
                Position=wheel.Position,
                Shape=new ConvexPolygonShape3D{Points=VehicleWheelGeometry.EnvelopePoints(radius,width)}};
            collider.SetMeta("collisionOwner","vehicle front tyre; same CharacterBody3D");
            AddChild(collider);
            _steeringVolumes.Add(new(wheel,collider,wheel.Position,radius,width));
        }
    }

    private void ApplySteeringCollision()
    {
        var lean=MotorcycleLeanTransform();
        foreach(var volume in _fixedVolumes)
            volume.Node.Transform=volume.Leans?lean*volume.Bind:volume.Bind;
        foreach(var wheel in _steeringVolumes)
            wheel.Collider.Transform=lean*new Transform3D(Basis.FromEuler(new(0,RoadWheelYaw(_steering),0)),wheel.Centre);
    }

    private IEnumerable<(Shape3D Shape,Transform3D Transform,string Name)> CollisionVolumes(
        Transform3D pose,float steering,bool includeChassis=true,float? leanRadians=null)
    {
        var lean=MotorcycleLeanTransform(leanRadians);
        foreach(var volume in _fixedVolumes)
            if(includeChassis||volume.Leans)
                yield return (volume.Node.Shape!,pose*(volume.Leans?lean*volume.Bind:volume.Bind),volume.Node.Name.ToString());
        foreach(var wheel in _steeringVolumes)
            yield return (wheel.Collider.Shape!,pose*lean*new Transform3D(Basis.FromEuler(new(0,RoadWheelYaw(steering),0)),wheel.Centre),wheel.Collider.Name.ToString());
        if(includeChassis && HorseFrameForPose(pose) is {} horse)
        {
            for(var index=0;index<_hoofQueries.Count;index++)
                yield return (_hoofQueries[index].Shape,horse.Legs[index].HoofPose,_hoofQueries[index].Name);
            for(var index=0;index<_lowerLegQueries.Count;index++)
                yield return (_lowerLegQueries[index].Shape,horse.Legs[index].KneePose,_lowerLegQueries[index].Name);
        }
    }

    private global::Godot.Collections.Array<global::Godot.Collections.Dictionary> VolumeOverlaps(
        Transform3D pose,float steering,global::Godot.Collections.Array<Rid> excluded,int maximum,bool includeChassis=true,float? leanRadians=null)
    {
        var result=new global::Godot.Collections.Array<global::Godot.Collections.Dictionary>();
        using var query=new PhysicsShapeQueryParameters3D{CollisionMask=CollisionMask,Exclude=excluded,Margin=0};
        foreach(var volume in CollisionVolumes(pose,steering,includeChassis,leanRadians))
        {
            var hoofIndex=_hoofQueries.FindIndex(hoof=>hoof.Name==volume.Name);
            var lowerLegIndex=_lowerLegQueries.FindIndex(leg=>leg.Name==volume.Name);
            query.Exclude=HorseFrameForPose(pose) is {} horse
                ?hoofIndex>=0?HoofExcluded(horse,hoofIndex,excluded)
                    :lowerLegIndex>=0?LowerLegExcluded(horse,lowerLegIndex,excluded):excluded
                :excluded;
            query.Shape=volume.Shape;query.Transform=volume.Transform;
            foreach(var hit in GetWorld3D().DirectSpaceState.IntersectShape(query,maximum-result.Count))
            {
                hit["vehicleShape"]=volume.Name;result.Add(hit);
                if(result.Count>=maximum)return result;
            }
        }
        return result;
    }

    private float ConstrainSteering(float requested)
    {
        if(_steeringVolumes.Count==0)return requested;
        var distance=requested-_steering;
        var initialLean=_motorcycleLean;
        var leanDistance=RequestedMotorcycleLean(requested)-initialLean;
        var steps=Math.Max(1,(int)Math.Ceiling(Math.Max(Math.Abs(distance),Math.Abs(leanDistance))/Mathf.DegToRad(.25f)));
        var accepted=_steering;var acceptedLean=initialLean;var excluded=Excluded();
        for(var i=1;i<=steps;i++)
        {
            var candidate=_steering+distance*i/steps;
            var candidateLean=initialLean+leanDistance*i/steps;
            var contacts=VolumeOverlaps(GlobalTransform,candidate,excluded,1,includeChassis:false,leanRadians:candidateLean);
            if(contacts.Count!=0)
            {
                _lastSteeringContact=DescribePlacementContact(contacts[0]);_lastRejectedSteering=candidate;
                _lastRejectedLean=candidateLean;_lastAcceptedLeanBeforeContact=acceptedLean;SteeringContactStops++;
                _motorcycleLean=acceptedLean;
                return accepted;
            }
            accepted=candidate;acceptedLean=candidateLean;
        }
        _motorcycleLean=acceptedLean;
        return accepted;
    }

    internal JsonObject DescribeSteeringCollision()
    {
        var volumes=new JsonArray();
        foreach(var wheel in _steeringVolumes)
            volumes.Add(new JsonObject{["name"]=wheel.Collider.Name.ToString(),["centre"]=wheel.Centre.ToString(),
                ["radius"]=wheel.Radius,["width"]=wheel.Width,["colliderYaw"]=wheel.Collider.Rotation.Y,
                ["visualYaw"]=wheel.Visual.Rotation.Y,["spin"]=wheel.Visual.Rotation.X,
                ["envelopeSides"]=VehicleWheelGeometry.EnvelopeSides,
                ["radialOverestimateMetres"]=VehicleWheelGeometry.TreadOuterRadius(wheel.Radius)
                    *(1/Mathf.Cos(Mathf.Pi/VehicleWheelGeometry.EnvelopeSides)-1)});
        return new JsonObject{["angle"]= _steering,["leanRadians"]=_motorcycleLean,["stops"]=SteeringContactStops,["volumes"]=volumes,
            ["lastRejectedAngle"]=_lastRejectedSteering,
            ["lastRejectedLean"]=_lastRejectedLean,["lastAcceptedLeanBeforeContact"]=_lastAcceptedLeanBeforeContact,
            ["lastContact"]=_lastSteeringContact?.DeepClone()};
    }

    // Test/read-only helpers use the same volumes and transforms as production.
    internal JsonObject DescribePhysicalVolumeContacts(Transform3D pose,float steering,float? leanRadians=null)
    {
        var hits=new JsonArray();
        foreach(var hit in VolumeOverlaps(pose,steering,PlacementExcluded(),24,leanRadians:leanRadians))hits.Add(DescribePlacementContact(hit));
        return new JsonObject{["origin"]=pose.Origin.ToString(),["steering"]=steering,
            ["leanRadians"]=leanRadians??_motorcycleLean,["contacts"]=hits};
    }

    internal bool PhysicalEnvelopeContains(Vector3 vehiclePoint,float steering,float tolerance=.00002f)
    {
        if(HorseFrameForPose(GlobalTransform) is {} horse)
        {
            for(var index=0;index<_hoofQueries.Count;index++)
            {
                var point=horse.Legs[index].HoofPose.AffineInverse()*(GlobalTransform*vehiclePoint);
                if(_hoofQueries[index].Planes.All(plane=>plane.DistanceTo(point)<=tolerance))return true;
            }
            for(var index=0;index<_lowerLegQueries.Count;index++)
            {
                var point=horse.Legs[index].KneePose.AffineInverse()*(GlobalTransform*vehiclePoint);
                if(_lowerLegQueries[index].Planes.All(plane=>plane.DistanceTo(point)<=tolerance))return true;
            }
        }
        var lean=MotorcycleLeanTransform();
        foreach(var volume in _fixedVolumes)
        {
            var local=(volume.Leans?lean*volume.Bind:volume.Bind).AffineInverse()*vehiclePoint;
            if(volume.WheelRadius>0
                ?volume.CartProfile is {} profile?CartWheelContains(profile,local,tolerance)
                    :VehicleWheelGeometry.EnvelopeContains(local,volume.WheelRadius,volume.WheelWidth,tolerance)
                :ContainsSimpleShape(volume.Node.Shape!,local,tolerance))return true;
        }
        vehiclePoint=lean.AffineInverse()*vehiclePoint;
        var inverse=Basis.FromEuler(new(0,RoadWheelYaw(steering),0)).Inverse();
        return _steeringVolumes.Any(wheel=>VehicleWheelGeometry.EnvelopeContains(inverse*(vehiclePoint-wheel.Centre),
            wheel.Radius,wheel.Width,tolerance));
    }
}
