using Godot;

namespace Urman.Godot;

public partial class W01Controller
{
    private static void ConfigureShuraleLod(MeshInstance3D lod0, MeshInstance3D lod1)
    {
        lod0.Visible = true;
        lod0.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;
        lod0.VisibilityRangeBegin = 0f;
        lod0.VisibilityRangeBeginMargin = 0f;
        lod0.VisibilityRangeEnd = 14f;
        lod0.VisibilityRangeEndMargin = 2f;

        lod1.Visible = true;
        lod1.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;
        lod1.VisibilityRangeBegin = 10f;
        lod1.VisibilityRangeBeginMargin = 2f;
        lod1.VisibilityRangeEnd = 0f;
        lod1.VisibilityRangeEndMargin = 0f;
    }
}
