using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    // These sources have separate authored placements, but no authored family
    // owner. A technical parcel records that fact without inventing a house
    // number or changing the source, geometry, dialogue or player knowledge.
    internal static readonly (string SourceName,string LogicalAnchor,string Cadastral)[] StandaloneShedSources =
    [
        ("ZiratVillageEdgeEastShed","zirat_road@village-edge-east-shed","URM-Q01-P0201"),
        ("PerimeterWestStreetShed","village_day@perimeter-west-street-shed","URM-Q01-P0202"),
        ("ArrivalReverseEastDomesticShed","village_day@arrival-reverse-east-domestic-shed","URM-Q01-P0203")
    ];

    internal static string StandaloneShedSourceKey(string sourceName) => "act1/outbuilding/" + sourceName;

    private void ImportStandaloneAddressParcels(HashSet<Node3D> imported)
    {
        var registry=AddressRegistry!;
        foreach(var binding in StandaloneShedSources)
        {
            var source=FindDescendants<Node3D>(this).SingleOrDefault(n=>n.Name==binding.SourceName&&n.IsVisibleInTree());
            if(source is null)continue;
            // Never infer this relation from shared presentation parents or
            // nearest neighbours. The committed logical anchor must still match.
            if(!source.HasMeta("logicalAnchor")||source.GetMeta("logicalAnchor").AsString()!=binding.LogicalAnchor)
                throw new InvalidOperationException("Standalone parcel source identity changed: "+binding.SourceName);
            imported.Add(source);
            const string leafName="OutbuildingShed_Door_Panel_LOD0";
            var leaf=FindDescendants<MeshInstance3D>(source).SingleOrDefault(n=>n.Name==leafName&&n.IsVisibleInTree()&&n.Mesh is not null);
            if(leaf is null)
            {
                _addressImportIssues.Add(new("STANDALONE_ENTRANCE_MISSING",binding.SourceName,"Expected actual authored shed door panel."));
                continue;
            }
            var key=StandaloneShedSourceKey(binding.SourceName);
            var buildingId=SettlementRegistry.StableId("BLD",key);
            var parcelId=SettlementRegistry.StableId("PAR",key);
            var accessId=SettlementRegistry.StableId("ACC",key);
            var outward=leaf.GlobalBasis*Vector3.Back;outward.Y=0;outward=outward.Normalized();
            var centre=leaf.GlobalTransform*leaf.Mesh!.GetAabb().GetCenter();
            var approach=AddressGround(centre+outward*1.05f);
            var footprint=AddressFootprint(source);
            registry.Register(new(buildingId,key,parcelId,null,"shed",AddressPoint(source.GlobalPosition),footprint),
                new(parcelId,"Q01",binding.Cadastral,buildingId,accessId,AddressParcelPolygon(footprint,AddressPoint(approach))),
                null,new(accessId,buildingId,"entrance",AddressPoint(approach),"","pending-physics"));
            source.SetMeta("building_id",buildingId);source.SetMeta("parcel_id",parcelId);
            source.SetMeta("settlement_source_key",key);source.SetMeta("addressStandaloneNonAddressable",true);
            source.SetMeta("addressEntranceSource",leaf.GetPath().ToString());
            source.SetMeta("addressParcelOwnershipEvidence","independent authored placement: "+binding.LogicalAnchor);
            _addressPendingAccess.Add((accessId,approach));
        }
    }
}
