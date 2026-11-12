using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private void BuildMosqueImamOffice(Node3D complex)
    {
        var room = _mosqueRoom!;
        // A compact heated lean-to within the existing courtyard: outer west
        // face -9.16 stays 14 cm clear of the courtyard wall's -9.30 face.
        // Only the concealed connection pierces the blank hall wall. The old
        // node stays available to shell/address consumers; its late contact is
        // retired by FinalizeFacilityContacts with the other replaced owners.
        var west = complex.GetNode<MeshInstance3D>("MosqueHallWest");
        west.Visible = false;
        // Two independent openings: the street entrance and the study door.
        foreach (var (name, z, depth) in new[] { ("North", -3.75f, 2.20f),
            ("Middle", -.025f, 1.85f), ("South", 3.525f, 2.65f) })
            FacilitySolid(room, "MosqueOfficeConnection" + name, new(.55f, 5.20f, depth),
                new(-6.90f, 2.07f, z), MosqueHallPaint, "wood_painted_trim");
        FacilitySolid(room, "MosqueOfficeConnectionHeader", new(.55f, 2.31f, 1.30f),
            new(-6.90f, 3.515f, 1.55f), MosqueHallPaint, "wood_painted_trim");
        FacilitySolid(room, "MosqueDoorHeaderInfill", new(.55f, 2.45f, 1.70f),
            new(-6.90f, 3.445f, -1.80f), MosqueHallPaint, "wood_painted_trim");

        var office = new Node3D { Name = "MosqueImamOffice" };
        room.AddChild(office);
        office.SetMeta("presentationRole", "working study; ordinary books, correspondence and household tools; no new story facts");
        office.SetMeta("clearFloorRect", new Rect2(-8.92f, .35f, 1.745f, 3.30f));
        BuildMosqueOfficeFoundation(office);
        var floor = FacilitySolid(office, "OfficeTimberFloor", new(2.56f, .08f, 3.54f), new(-7.88f, -.04f, 2f), "a68a62", "wood_furniture");
        floor.MaterialOverride = MosqueFinish("walnut", "ead6b3", 1.10f);
        FacilitySolid(office, "OfficeWestWall", new(.24f, 3.20f, 3.78f), new(-9.04f, 1.60f, 2f), MosqueHallPaint, "wood_painted_trim");
        foreach (var z in new[] { .23f, 3.77f })
            FacilitySolid(office, "OfficeEndWall" + z, new(2.14f, 3.20f, .24f), new(-7.97f, 1.60f, z), MosqueHallPaint, "wood_painted_trim");
        var lining = AddVisualBox(office, "OfficeWallpaper", new(.012f, 2.2f, 3.28f), new(-8.913f, 2.05f, 2f), "eee6d2", "paper");
        lining.MaterialOverride = MosqueFinish("wallpaper", "ffffff", 1.20f);
        foreach (var z in new[] { .356f, 3.644f })
        {
            var panel = AddVisualBox(office, "OfficeEndWallpaper" + z, new(1.73f, 2.2f, .012f), new(-8.045f, 2.05f, z), "eee6d2", "paper");
            panel.MaterialOverride = MosqueFinish("wallpaper", "ffffff", 1.20f);
        }
        MosqueWallPanelling(office,"OfficeWestPanelling",new(-8.88f,.50f,2f),3.30f,Mathf.Pi*.5f);
        FacilitySolid(office,"OfficeCeiling",new(2.14f,.12f,3.78f),new(-7.97f,3.26f,2f),"eee9dc","wood_painted_trim");
        // Closed roof boards and metal skin with full overlap at the hall wall.
        var roof=AddVisualBox(office,"OfficeRoof",new(2.64f,.09f,4.06f),new(-7.98f,3.51f,2f),"536b5d","roof_metal");
        roof.Rotation=new(0,0,Mathf.DegToRad(8f));
        var seams=new List<Transform3D>();
        for(var i=0;i<9;i++)seams.Add(new Transform3D(roof.Basis,roof.Position+roof.Basis*new Vector3(0,.052f,-1.82f+i*.455f)) *
            new Transform3D(Basis.FromScale(new Vector3(2.64f,.014f,.028f)),Vector3.Zero));
        MosqueBatch(office,"OfficeRoofOverlaps",seams,PainterlyMaterialLibrary.ForColor("536b5d","roof_metal"));
        AddVisualBox(office,"OfficeRoofFlashing",new(.14f,.27f,4.06f),new(-6.73f,3.66f,2f),"536b5d","roof_metal");
        foreach(var z in new[]{.05f,3.95f})
            AddVisualBox(office,"OfficeFascia"+z,new(2.65f,.15f,.09f),new(-7.98f,3.45f,z),"e8e3d5","wood_painted_trim").Rotation=new(0,0,Mathf.DegToRad(8f));

        // Reuse the previous library door's persistent key; the existing hall
        // library remains open, while the door now leads to its working annex.
        // The hinge is within the 55 cm connection wall. Beyond 90 degrees
        // the leaf folds back into its north pier (Z <= .90), so the full
        // safety sweep correctly refuses to start. At 90 degrees the leaf's
        // north face is Z .9175: 15.5 mm clear after the sweep's 2 mm margin.
        var door=FacilityManualDoor(room,"MosqueLibrary","mosque/library",new(-6.90f,0,.95f),1.20f,2.30f,0,-90);
        door.Hinge.GetNode<MeshInstance3D>("MosqueLibraryLeaf").MaterialOverride=MosqueFinish("walnut","d8ccb4",.8f);
        foreach(var z in new[]{.92f,2.18f})MosqueWood(room,"OfficeDoorJamb"+z,new(.10f,2.36f,.09f),new(-6.57f,1.18f,z));
        MosqueWood(room,"OfficeDoorLintel",new(.10f,.10f,1.44f),new(-6.57f,2.36f,1.55f));
        MosquePrintedSign(room,"OfficeDoorLabel","Кабинет",new(-6.505f,2.58f,1.55f),90,.53f,"signs-main",5);
        MosqueWood(room,"OfficeDoorSill",new(.62f,.003f,1.29f),new(-6.91f,.0015f,1.55f));

        var shelves=new Node3D{Name="OfficeBookcase",Position=new(-8.70f,0,1.10f),RotationDegrees=new(0,-90,0)};
        office.AddChild(shelves);BuildMosqueLibraryCase(shelves,"Office",Vector3.Zero,1.35f);
        // Desk is a separate joined object, with a modest working surface and
        // drawers facing the chair; no prop blocks the door's inward sweep.
        var desk=new Node3D{Name="MosqueImamDesk",Position=new(-8.04f,0,3.05f)};room.AddChild(desk);
        MosqueWood(desk,"Top",new(1.42f,.045f,.68f),new(0,.76f,0),true);
        foreach(var x in new[]{-.62f,.62f})foreach(var z in new[]{-.24f,.24f})
            MosqueWood(desk,$"Leg{x}_{z}",new(.065f,.7375f,.065f),new(x,.36875f,z),true);
        MosqueWood(desk,"DrawerPedestal",new(.38f,.54f,.52f),new(.43f,.49f,0),true);
        for(var i=0;i<3;i++)
        {
            MosqueWood(desk,"DrawerFront"+i,new(.35f,.15f,.025f),new(.43f,.31f+i*.17f,-.274f));
            FacilityRod(desk,"DrawerPull"+i,new(.37f,.31f+i*.17f,-.302f),new(.49f,.31f+i*.17f,-.302f),.008f,"8e8060");
        }
        // Leave a .79 m aisle on the chair's east side for the .70 m capsule.
        var chair=new Node3D{Name="OfficeChair",Position=new(-8.18f,0,2.20f)};office.AddChild(chair);
        MosqueWood(chair,"Seat",new(.43f,.055f,.43f),new(0,.46f,0),true);
        foreach(var x in new[]{-.17f,.17f})foreach(var z in new[]{-.17f,.17f})
            MosqueWood(chair,$"Leg{x}_{z}",new(.04f,.4325f,.04f),new(x,.21625f,z));
        foreach(var x in new[]{-.17f,.17f})MosqueWood(chair,"BackPost"+x,new(.035f,.43f,.035f),new(x,.70f,-.18f));
        MosqueWood(chair,"Back",new(.38f,.19f,.035f),new(0,.83f,-.18f),true);
        var cushion=AddVisualBox(chair,"WovenSeat",new(.38f,.025f,.38f),new(0,.50f,0),"52665a","cloth");
        cushion.MaterialOverride=MosqueFinish("bookcloth");

        var notebook=AddVisualBox(room,"MosqueImamNotebook",new(.29f,.018f,.21f),new(-8.22f,.7915f,3.00f),"d6c7a5","paper");
        notebook.MaterialOverride=MosqueFinish("paper","ffffff",.30f);
        MosqueOfficeDocument(desk,"OpenNotebookLeft",new(-.23f,.802f,-.04f),.135f,"documents-a",0);
        MosqueOfficeDocument(desk,"OpenNotebookRight",new(-.08f,.802f,-.04f),.135f,"documents-b",2);
        for(var i=0;i<3;i++)MosqueOfficeDocument(desk,"Letters"+i,new(.33f,.7831f+i*.0012f,.18f),.17f,"documents-a",i==2?1:3,i*3-3);
        MosqueOfficeDocument(desk,"OfficeExpenseSheet",new(-.54f,.7831f,-.12f),.16f,"documents-a",2,-7);
        MosqueOfficeDocument(desk,"MosqueNikahBlank",new(.17f,.7831f,-.10f),.21f,"nikah-blank",yaw:-2);
        FacilityRod(desk,"Pencil",new(-.05f,.795f,-.22f),new(.11f,.795f,-.20f),.003f,"877044");
        FacilityVessel(desk,"TeaCup",new(-.54f,.84f,.18f),.038f,.08f,"dad8c9",false);
        DiscoveryCylinder(desk,"Saucer",.065f,.065f,.009f,new(-.54f,.787f,.18f),"dad8c9");
        DiscoveryCylinder(desk,"DeskLampFoot",.085f,.095f,.025f,new(.53f,.795f,.18f),"575f51");
        FacilityRod(desk,"DeskLampStem",new(.53f,.81f,.18f),new(.53f,1.12f,.18f),.010f,"857751");
        var shade=DiscoveryCylinder(desk,"DeskLampShade",.085f,.135f,.16f,new(.53f,1.13f,.18f),"d6cbb1");
        shade.MaterialOverride=MosqueFinish("paper");
        FacilityLamp(office,"OfficeWarmLight",new(-8.02f,2.88f,2.04f),"ffe5c2",.50f,2.9f);
        var lamp=new OmniLight3D{Name="DeskPool",Position=new(.53f,1.07f,.18f),LightColor=new Color("ffe4b7"),LightEnergy=.32f,OmniRange=1.4f,ShadowEnabled=false};
        desk.AddChild(lamp);_facilityLights.Add(lamp);
        MosqueWood(office,"OfficeNoticeBoard",new(.035f,.74f,1.10f),new(-8.87f,1.78f,2.75f));
        for(var i=0;i<3;i++)
        {
            MosqueOfficeDocument(office,"PinnedNote"+i,new(-8.845f,1.82f,2.41f+i*.31f),.20f,"documents-b",i==2?3:i,pinned:true);
            DiscoveryCylinder(office,"NotePin"+i,.009f,.009f,.014f,new(-8.83f,1.95f,2.41f+i*.31f),"8e8060").RotationDegrees=new(0,0,90);
        }
        // Retain the old anchors; the handwritten Tatar headings now belong
        // to each actual sheet rather than floating over the notice board.
        office.AddChild(new Label3D { Name="OfficeNotesTitle", Text=string.Empty });
        office.AddChild(new Label3D { Name="OfficeLetterHeading", Text=string.Empty });
    }

    private void BuildMosqueOfficeFoundation(Node3D office)
    {
        var contact=GetNode<CollisionShape3D>("Act1CoreWorldGreybox/AgentBExteriorWorld/AgentB_TerrainCollision/AgentB_TerrainFaces");
        var toOffice=office.GlobalTransform.AffineInverse()*contact.GlobalTransform;
        var faces=((ConcavePolygonShape3D)contact.Shape).GetFaces().Select(p=>toOffice*p).ToArray();
        var ground=MosqueTerrainBounds(faces,new(-9.16f,0,.11f),new(-6.90f,0,3.89f));
        var bottom=Mathf.Min(-.20f,ground.Minimum-.08f);
        FacilitySolid(office,"OfficePlinth",new(2.26f,-.08f-bottom,3.78f),new(-8.03f,(bottom-.08f)*.5f,2f),"8d8979","stone");
    }
}
