using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private void BuildMosqueHallFinishes()
    {
        var room = _mosqueRoom!;
        room.SetMeta("interiorRevision", "warm-hall-2026-11-12");
        room.SetMeta("vestibulePartition", "open hall; clear entrance mat and shoe bench");
        // Thin linings touch the structural wall; original window apertures,
        // reveals and radiator clearances are the fixed layout inputs.
        foreach (var side in new[] { -1f, 1f })
        {
            var z = side * 4.565f;
            void Paper(string name, Vector3 size, Vector3 at)
            {
                var mesh = AddVisualBox(room, name, size, at, "eee6d2", "paper");
                mesh.MaterialOverride = MosqueFinish("wallpaper", "ffffff", 1.20f);
            }
            Paper("MosqueWallpaperHead"+side,new(11.85f,1.07f,.012f),new(-.7f,3.19f,z));
            foreach (var (x, width) in new[] { (-4.9625f,3.325f),(0f,3.78f),(4.25f,1.90f) })
                Paper($"MosqueWallpaperPier{side}_{x}",new(width,1.64f,.012f),new(x,1.83f,z));
            Paper("MosqueWallpaperSill"+side,new(11.85f,.25f,.012f),new(-.7f,1.125f,z));
            MosqueWallPanelling(room,"MosqueDado"+side,new(-.7f,.50f,side*4.54f),11.85f,0);
            foreach(var x in new[]{-2.6f,2.6f})
            {
                MosqueWood(room,$"MosqueInnerSill{side}_{x}",new(1.52f,.045f,.27f),new(x,1.255f,side*4.45f));
                foreach(var dx in new[]{-.755f,.755f})
                    AddVisualBox(room,$"MosqueInnerCasing{side}_{x}_{dx}",new(.085f,1.51f,.055f),new(x+dx,1.96f,side*4.535f),"e7deca","wood_painted_trim");
            }
        }
        // The west wall has one office connection behind the new annex.
        foreach(var (z,depth) in new[]{(-1.8375f,5.475f),(3.3875f,2.375f)})
        {
            var lining=AddVisualBox(room,"MosqueWestWallpaper"+z,new(.012f,2.69f,depth),new(-6.613f,2.375f,z),"eee6d2","paper");
            lining.MaterialOverride=MosqueFinish("wallpaper","ffffff",1.20f);
            MosqueWallPanelling(room,"MosqueWestDado"+z,new(-6.59f,.50f,z),depth,Mathf.Pi*.5f);
        }
        var overDoor=AddVisualBox(room,"MosqueOfficeDoorWallpaper",new(.012f,1.36f,1.30f),new(-6.613f,3.06f,1.55f),"eee6d2","paper");
        overDoor.MaterialOverride=MosqueFinish("wallpaper","ffffff",1.20f);
        foreach(var (z,depth) in new[]{(-2.765f,3.62f),(2.765f,3.62f)})
        {
            var lining=AddVisualBox(room,"MosqueEastWallpaper"+z,new(.012f,2.65f,depth),new(5.215f,2.40f,z),"eee6d2","paper");
            lining.MaterialOverride=MosqueFinish("wallpaper","ffffff",1.20f);
        }
    }

    private void MosqueWallPanelling(Node3D parent,string name,Vector3 at,float width,float yaw)
    {
        var panel=new Node3D{Name=name,Position=at,Rotation=new(0,yaw,0)};parent.AddChild(panel);
        MosqueWood(panel,"RecessedPanels",new(width,.92f,.024f),Vector3.Zero,tint:"b2a48e");
        foreach(var y in new[]{-.45f,.44f})MosqueWood(panel,"Rail"+y,new(width,.065f,.052f),new(0,y,0));
        var stiles=new List<Transform3D>();var count=Mathf.CeilToInt(width/.66f);
        for(var i=0;i<=count;i++)stiles.Add(MosqueBoxTransform(new(-width*.5f+i*width/count,0,0),new(.035f,.84f,.047f),0));
        MosqueBatch(panel,"Stiles",stiles,MosqueFinish("walnut","c1b395",.75f));
        MosqueWood(panel,"TopCap",new(width,.030f,.075f),new(0,.492f,0));
    }

    private void BuildMosqueEntranceFurniture()
    {
        var room=_mosqueRoom!;
        // Keep the full front of the seat west of the new washroom wall.
        // Footwear and visit rays follow this same bench anchor.
        var bench=new Node3D{Name="MosqueShoeBench",Position=new(2.45f,0,3.05f)};room.AddChild(bench);
        MosqueWood(bench,"Seat",new(1.90f,.075f,.425f),new(0,.445f,0),true);
        foreach(var x in new[]{-.78f,.78f})
        {
            MosqueWood(bench,"Trestle"+x,new(.07f,.4075f,.34f),new(x,.20375f,0),true);
            MosqueWood(bench,"Foot"+x,new(.16f,.035f,.43f),new(x,.0175f,0));
        }
        MosqueWood(bench,"Stretcher",new(1.56f,.06f,.05f),new(0,.20f,0));
        for(var level=0;level<3;level++)
            MosqueWood(room,"MosqueShoeShelf"+level,new(1.53f,.05f,.34f),new(3.95f,.13f+level*.24f,4.19f),true);
        foreach(var x in new[]{3.16f,4.74f})
            MosqueWood(room,"MosqueShoeRackUpright"+x,new(.06f,.78f,.40f),new(x,.39f,4.20f),true);
        MosqueWood(room,"MosqueShoeRackBack",new(1.64f,.70f,.025f),new(3.95f,.39f,4.36f));
        var mat=AddVisualBox(room,"MosqueEntranceMat",new(2.63f,.008f,1.35f),new(3.785f,.006f,0),"5c635a","fabric");
        mat.MaterialOverride=MosqueFinish("bookcloth","d3c7ad",.45f);
        MosqueWood(room,"MosqueEntranceCourtesyBacking",new(.90f,.32f,.018f),new(4.15f,1.60f,4.544f));
        var notice=AddVisualBox(room,"MosqueEntranceCourtesyPaper",new(.84f,.26f,.002f),new(4.15f,1.60f,4.533f),"e8dfc7","paper");
        notice.MaterialOverride=MosqueFinish("paper");
        MosquePrintedSign(room,"MosqueEntranceCourtesy","Пожалуйста,\nснимите обувь",new(4.15f,1.60f,4.519f),180,.63f,"signs-details",1);
        FacilitySolid(room,"MosqueHeatingCabinet",new(.52f,.76f,.30f),new(4.72f,1.74f,4.42f),"d2d2c5","metal");
        for(var i=0;i<5;i++)AddVisualBox(room,"MosqueHeatingVent"+i,new(.34f,.012f,.014f),new(4.72f,1.53f+i*.046f,4.268f),"626c63","metal");
        FacilityRod(room,"MosqueHeatSupply",new(4.65f,1.36f,4.51f),new(4.65f,.27f,4.51f),.018f,"a3aaa8");
        FacilityRod(room,"MosqueHeatReturn",new(4.83f,1.36f,4.51f),new(4.83f,.12f,4.51f),.018f,"a3aaa8");
    }

    private void DressMosqueEntrance(FacilityDoor entrance)
    {
        var leaf=entrance.Hinge.GetNode<MeshInstance3D>("MosqueEntranceLeaf");
        leaf.MaterialOverride=MosqueFinish("walnut","d1c5ac",.85f);
        foreach(var joint in entrance.Hinge.GetChildren().OfType<MeshInstance3D>().Where(n=>n.Name.ToString().StartsWith("MosqueEntrancePlankJoint",StringComparison.Ordinal)))joint.Visible=false;
        foreach(var face in new[]{-1f,1f})foreach(var y in new[]{.57f,1.58f})
        {
            MosqueWood(entrance.Hinge,$"RaisedPanel{face}_{y}",new(.010f,.74f,1.11f),new(face*.038f,y,.72f),tint:"e1d2b6");
            foreach(var z in new[]{.14f,1.30f})MosqueWood(entrance.Hinge,$"PanelStile{face}_{y}_{z}",new(.018f,.80f,.045f),new(face*.043f,y,z));
        }
        // Author authorised lettering on 12.11. The supplied proper name is
        // retained; the possessive form is source-checked against Tatarica:
        // https://tatarica.org/tat/razdely/religiya/islam/mechet
        // This source check does not replace a native-language art acceptance.
        var sign=MosquePrintedSign(_mosqueRoom!,"MosqueNameSign","Кара-Урман мәчете",new(8.02f,2.98f,0),90,1.25f,"signs-main",0);
        sign.SetMeta("languageReview","author wording; мәчете source-checked; native visual review pending");
    }
}
