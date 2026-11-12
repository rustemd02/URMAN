using System;
using System.Linq;
using Godot;

namespace Urman.Godot;

public static partial class StyleBenchmarkInteriorFactory
{
    private static void BuildNewBabaiHouseInterior(Node3D room)
    {
        var oldWing = room.GetNode<Node3D>("BabaiHouseWing");
        foreach (var node in room.GetChildren().ToArray())
        {
            var name = node.Name.ToString();
            if (IsWindowShellRoot(node) || name is "Floor" or "Ceiling" or "ZalWingDoor"
                || name.StartsWith("CeilingBeam", StringComparison.Ordinal)
                || name.StartsWith("Skirting", StringComparison.Ordinal)
                || name.StartsWith("Cornice", StringComparison.Ordinal)) RetireBabaiInterior(node);
        }

        Node3D MoveRoom(string name, Vector3 origin, float yaw = 0)
        {
            var group = oldWing.GetNode<Node3D>(name);
            group.Reparent(room, false);
            group.Position = origin;
            group.RotationDegrees = new(0, yaw, 0);
            foreach (var child in group.GetChildren().ToArray())
                if (child.Name.ToString().Contains("Window", StringComparison.Ordinal)) RetireBabaiInterior(child);
            return group;
        }
        var kitchen = MoveRoom("Kitchen", new(-2.6f, 0, -8.8f), 90);
        foreach (var name in new[] { "KitchenSideboard", "KitchenSideboardGlass" })
            kitchen.GetNode<Node3D>(name).Position += Vector3.Back * 1.20f;
        // Wall-mounted belongings follow the new kitchen walls, not the old
        // wing's narrower partition lines.
        foreach (var name in new[] { "KitchenHookRail", "KitchenCoat", "KitchenShawl", "KitchenBootTray" })
            kitchen.GetNode<Node3D>(name).Position += new Vector3(2.1f, 0, 1.25f);
        for (var i = 0; i < 3; i++)
        {
            kitchen.GetNode<Node3D>("KitchenHerbBundle"+i).Position += Vector3.Back * 1.25f;
            kitchen.GetNode<Node3D>("KitchenOnionPlait"+i).Position += Vector3.Forward * .38f;
        }
        kitchen.GetNode<Node3D>("KitchenClock").Position += Vector3.Right * .22f;
        kitchen.GetNode<Node3D>("KitchenTowel").Position += Vector3.Right * .19f;
        var bedroom = MoveRoom("Bedroom", new(-5, 2.8f, -3.8f));
        foreach (var name in new[] { "BedroomDresser", "DresserMirror", "DresserMirrorFrame", "DresserVase" })
            bedroom.GetNode<Node3D>(name).Position += Vector3.Forward * .30f;
        bedroom.GetNode<Node3D>("BedWallCarpet").Position = new(-.1f, 1.75f, .48f);
        MoveRoom("Toilet", new(-1, 0, -7.4f));
        var hall = MoveRoom("Hall", new(0, 2.8f, -.5f));
        for (var i = 0; i < 3; i++) hall.GetNode<Node3D>("HallPhoto"+i).Position = new(3.35f, 1.6f, .2f+i*.45f);
        Prop(hall,"HallLampCord",new(.012f,.35f,.012f),new(1f,2.465f,1.4f),"302d29","fabric_upholstery");
        Prop(bedroom,"BedroomLampCord",new(.012f,.35f,.012f),new(2f,2.465f,-1.5f),"302d29","fabric_upholstery");
        var attic = MoveRoom("Attic", new(0, 2.8f, -2));
        foreach (var child in attic.GetChildren().ToArray())
        {
            var name = child.Name.ToString();
            if (name.StartsWith("AtticRoof", StringComparison.Ordinal)
                || name.StartsWith("AtticKnee", StringComparison.Ordinal)
                || name.StartsWith("AtticGable", StringComparison.Ordinal)
                || name.StartsWith("AtticDormer", StringComparison.Ordinal)
                || name.StartsWith("AtticChimney", StringComparison.Ordinal)
                || name.StartsWith("AtticRidge", StringComparison.Ordinal)
                || name.StartsWith("AtticCollar", StringComparison.Ordinal)) RetireBabaiInterior(child);
        }
        var jacket = attic.GetNode<Node3D>("AtticJacket");
        jacket.Position = jacket.Position with { Z = -5.44f };
        Prop(attic,"JacketHookRail",new(.52f,.045f,.04f),new(-2.9f,4.87f,-5.47f),"5d4a38","wood_prop");
        for (var i = 0; i < 2; i++)
        {
            var ski = attic.GetNode<Node3D>("AtticSki"+i);
            ski.Position = ski.Position with { Z = -5.17f+i*.10f };
        }
        foreach (var name in new[] { "AtticBulb", "AtticBulbGlass" })
        {
            var bulb = attic.GetNode<Node3D>(name);
            bulb.Position = bulb.Position with { Z = 0 };
        }
        Prop(attic,"LoftLampCord",new(.012f,.12f,.012f),new(-.6f,5.23f,0),"302d29","fabric_upholstery");
        RetireBabaiInterior(oldWing);
        var interior = new Node3D { Name = "NewBabaiHouseInterior" };
        room.AddChild(interior);
        void Floor(string name, float x0, float x1, float z0, float z1, float top)
            => Block(interior, name, new(x1-x0, .16f, z1-z0),
                new((x0+x1)*.5f, top-.08f, (z0+z1)*.5f), "7a4634", "wood_floor_planked");
        Floor("GroundFloor", -6.5f, 6.5f, -7.5f, 3.5f, 0);
        Floor("EntryFloorJoint", DoorX-.75f, DoorX+.75f, 3.5f, 3.70f, 0);
        foreach (var level in new[] { 2.8f, 5.6f })
        {
            Floor("FloorWest"+level, -6.5f, 5.15f, -7.5f, 3.5f, level);
            Floor("FloorLanding"+level, 5.15f, 6.5f, -7.5f, -1.90f, level);
            Floor("FloorSouth"+level, 5.15f, 6.5f, 3.2f, 3.5f, level);
        }
        void Partition(string name, bool alongX, float plane, float start, float end,
            float floor, float door = float.NaN, float width = 1.1f)
        {
            var wall = new Node3D { Name = name, Position = Vector3.Up * floor };
            interior.AddChild(wall);
            var holes = float.IsNaN(door) ? Array.Empty<Opening>()
                : new[] { new Opening(door, width, 0, 2.25f) };
            WallRun(wall, "Partition", alongX, plane, start, end, 2.64f, .12f,
                holes, "d5c9ae", "plaster_domestic");
            if (!float.IsNaN(door)) DoorCasing(wall, "PassageTrim", alongX, plane, door, width, 2.25f, .16f);
        }
        Partition("ZalEast", false, 4.0f, -3.60f, 3.5f, 0, 2.75f, 1.30f);
        Partition("ZalBack", true, -3.60f, -4.1f, 4f, 0, 1.55f, 1.25f);
        Partition("ZalWest", false, -4.1f, -3.6f, 3.5f, 0);
        Partition("ToiletWest", false, 2.2f, -7.3f, -3.8f, 0, -5.4f, 1f);
        Partition("ToiletEast", false, 4.05f, -7.3f, -3.8f, 0);
        Partition("ToiletSouth", true, -3.8f, 2.2f, 4.05f, 0);
        Partition("BedroomEast", false, .08f, -7.3f, -3.25f, 2.8f);
        Partition("BedroomSouth", true, -3.25f, -6.3f, .08f, 2.8f, -2.7f, 1.2f);
        Partition("UpperHallEast", false, 3.42f, -.5f, 3.5f, 2.8f);
        Partition("UpperFamilyRoom", false, -1.6f, -3.25f, 3.5f, 2.8f, -1.55f, 1.25f);
        DressNewBabaiFamilyRoom(interior);
        BuildNewBabaiStairFlight(interior, 0);
        BuildNewBabaiStairFlight(interior, 2.8f);
        // Guard the open shaft, leaving a full side-entry bay beside the low
        // treads. The .30 m strip at the south facade is not a turning landing.
        foreach (var level in new[] { 2.8f, 5.6f })
        {
            Block(interior, "ShaftRail"+level, new(.055f, .06f, 4.40f),
                new(5.18f, level+.96f, .10f), "5d4a38", "wood_prop");
            for (var i=0; i<=20; i++)
                Block(interior, "ShaftBaluster"+level+"_"+i, new(.035f,.93f,.035f),
                    new(5.18f,level+.465f,-2.1f+i*.22f), "765842", "wood_prop");
        }
        FirstPersonController.SealedInteriorVolume = point =>
        {
            if (!GodotObject.IsInstanceValid(room) || !room.IsInsideTree()) return false;
            var p = room.ToLocal(point);
            return p.X > -6.5f && p.X < 6.5f && p.Z > -7.5f && p.Z < 3.7f && p.Y > -.4f && p.Y < 10f;
        };
        room.SetMeta("houseRooms", "zal|kitchen|toilet|upper-bedroom|upper-hall|inhabited-attic");
        room.SetMeta("houseWingPolicy", "one physical house; two full stair flights; legacy underground retired");
    }

    private static void RetireBabaiInterior(Node node)
    {
        if (node is Node3D spatial) spatial.Visible = false;
        if (node is CollisionObject3D body) { body.CollisionLayer = 0; body.CollisionMask = 0; }
        if (node is CollisionShape3D shape) shape.Disabled = true;
        if (node is Area3D area) { area.Monitoring = false; area.Monitorable = false; }
        node.ProcessMode = Node.ProcessModeEnum.Disabled;
        foreach (var child in node.GetChildren()) RetireBabaiInterior(child);
    }

    private static void DressNewBabaiFamilyRoom(Node3D interior)
    {
        var furnishings = new Node3D { Name = "UpperFamilyRoomFurniture", Position = new(0,2.8f,0) };
        interior.AddChild(furnishings);
        var contacts = new StaticBody3D { Name="FurnitureContacts", CollisionLayer=1, CollisionMask=0 };
        furnishings.AddChild(contacts);
        var table = RuralPropModels.Desk(furnishings,"ReadingTable",new(-4.4f,0,-.7f),0,1.6f,.8f,.76f);
        RuralPropGeometry.AttachMemberContacts(table,contacts);
        foreach (var (at,yaw) in new[] { (new Vector3(-4.4f,0,.25f),0f), (new Vector3(-4.4f,0,-1.65f),180f) })
            RuralPropGeometry.AttachMemberContacts(RuralPropModels.Chair(furnishings,"ReadingChair",at,yaw),contacts);
        RuralPropGeometry.AttachMemberContacts(RuralPropModels.Bench(furnishings,"WindowBench",new(-3.8f,0,2.55f),0,2.4f,false),contacts);
        Prop(furnishings,"WovenRunner",new(2.9f,.014f,2.7f),new(-4.4f,.008f,-.7f),"645244","carpet");
        // Books face the room from a shallow shelf on the existing hall wall;
        // window arcs and the route between the stair and bedroom stay clear.
        foreach (var y in new[] { .15f,.72f,1.29f,1.86f })
            Block(furnishings,"BookcaseShelf",new(.28f,.035f,1.2f),new(3.18f,y,2.4f),"71543d","wood_furniture_interior");
        foreach (var z in new[] { 1.78f,3.02f })
            Block(furnishings,"BookcaseSide",new(.28f,1.86f,.04f),new(3.18f,.95f,z),"71543d","wood_furniture_interior");
        for (var row=0;row<3;row++) for(var book=0;book<9;book++)
            Prop(furnishings,"BoundBook"+row+"_"+book,new(.20f,.27f+book%3*.025f,.045f),
                new(3.15f,.31f+row*.57f,1.90f+book*.105f),book%3==0?"4d6266":book%3==1?"786146":"754a41","cloth");
        Light(furnishings,"ReadingRoomLight",new(-3.8f,2.25f,.25f),"e5c99b",.65f,5.5f,shadow:false);
    }

    private static void BuildNewBabaiStairFlight(Node3D parent, float floor)
    {
        var stairs = new Node3D { Name = "StairsFrom"+floor, Position = Vector3.Up*floor };
        parent.AddChild(stairs);
        const float rise=2.8f/18f, tread=.28f;
        // Side entry: the front wall leaves no capsule-sized turning landing
        // ahead of the first tread. This first-height platform connects the
        // corridor to the second tread with another single .1556 m rise.
        Block(stairs,"FirstStepSideLanding",new(1.175f,rise,.80f),
            new(4.6875f,rise*.5f,3.10f),"98754f","wood_furniture_interior");
        for (var i=0;i<18;i++)
        {
            var top=(i+1)*rise; var z=3.14f-(i+.5f)*tread;
            Block(stairs,"Tread"+i,new(1.15f,.045f,tread),new(5.85f,top-.0225f,z),"98754f","wood_furniture_interior");
            Block(stairs,"Riser"+i,new(1.15f,rise-.045f,.025f),
                new(5.85f,i*rise+(rise-.045f)*.5f,z+tread*.5f-.0125f),"765842","wood_furniture_interior");
        }
        var wood=PainterlyMaterialLibrary.ForColor("5d4a38","wood_prop",sheltered:true);
        foreach (var x in new[] { 5.32f,6.38f })
        {
            RuralPropGeometry.Tube(stairs,"Stringer"+x,new(x,.025f,3.14f),new(x,2.67f,-1.9f),.065f,wood);
            var first=x<5.8f?4:0;
            for(var i=first;i<18;i+=2)
                Block(stairs,"Baluster"+x+"_"+i,new(.035f,.88f,.035f),
                    new(x,(i+1)*rise+.44f,3.14f-(i+.5f)*tread),"765842","wood_prop");
            var a=new Vector3(x,(first+1)*rise+.90f,3.14f-(first+.5f)*tread);
            var b=new Vector3(x,2.8f+.90f,-1.76f);
            RuralPropGeometry.Tube(stairs,"Handrail"+x,a,b,.032f,wood);
        }
    }
}
