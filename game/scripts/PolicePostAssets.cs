using Godot;

namespace Urman.Godot;

internal static class PolicePostAssets
{
    private const string Folder="res://assets/third_party/police/";
    private static readonly Dictionary<string,PackedScene> Scenes=new(StringComparer.Ordinal);
    internal static void ClearCacheForTests()=>Scenes.Clear();
    public static Node3D Attach(Node3D parent,string asset,string name,Vector3 at,float yaw=0,float scale=1)
    {
        if(!Scenes.TryGetValue(asset,out var packed)||!GodotObject.IsInstanceValid(packed))
            Scenes[asset]=packed=ResourceLoader.Load<PackedScene>(Folder+asset+".glb")
                ?? throw new InvalidOperationException("Missing licensed police prop: "+asset);
        var model=packed.Instantiate<Node3D>();model.Name=name;model.Position=at;
        model.RotationDegrees=new(0,yaw,0);model.Scale=Vector3.One*scale;
        parent.AddChild(model);model.SetMeta("licensedAsset",asset);
        foreach(var mesh in model.FindChildren("*",nameof(MeshInstance3D),true,false).OfType<MeshInstance3D>())
        {
            mesh.VisibilityRangeEnd=asset=="vaz2106_static"?110:55;
            mesh.VisibilityRangeEndMargin=4;
        }
        return model;
    }
}
