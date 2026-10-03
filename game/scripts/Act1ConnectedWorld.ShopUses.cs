using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    internal const string ShopBatteryUseInteraction = "urman.chapter1:local/shop-batteries";
    internal const string ShopTeaUseInteraction = "urman.chapter1:local/shop-tea";
    private CarryableProp? _shopSupplyLamp;
    private OmniLight3D? _shopSupplyLight;
    private StandardMaterial3D? _shopSupplyGlow;
    private float _shopSupplyBaseEnergy, _shopSupplyBaseRange, _shopSupplyBaseEmission;
    private Node3D? _shopTeaPackage;
    private InteractionTarget? _shopBatteryTarget, _shopTeaTarget;
    private RuntimeBridge? _shopSupplyBridge;
    private bool _shopSupplyBusy;

    /// <summary>Physical consumers of the existing shop/custody transaction.
    /// Build before collecting zone interaction bindings, after carry props exist.</summary>
    private void BuildShopUses()
    {
        if (_shopBatteryTarget is not null) throw new InvalidOperationException("Shop uses already exist.");
        var carry = _carryCoordinator ?? throw new InvalidOperationException("Carry coordinator must be constructed before shop affordances.");
        _shopSupplyLamp = carry.Items.Single(item => item.ItemId == "carry-lantern");
        _shopSupplyLight = _shopSupplyLamp.GetNode<OmniLight3D>("PortableLight");
        _shopSupplyBaseEnergy = _shopSupplyLight.LightEnergy;
        _shopSupplyBaseRange = _shopSupplyLight.OmniRange;
        var diffuser = _shopSupplyLamp.GetNode<MeshInstance3D>("LitDiffuser");
        _shopSupplyGlow = (StandardMaterial3D)diffuser.MaterialOverride.Duplicate();
        _shopSupplyBaseEmission = _shopSupplyGlow.EmissionEnergyMultiplier;
        diffuser.MaterialOverride = _shopSupplyGlow;
        _shopBatteryTarget = SupplyTarget(_shopSupplyLamp, "ReplaceLanternBatteries", ShopBatteryUseInteraction,
            "Заменить батарейки в основании фонаря", new(0, .044f, .114f), new(.212f, .086f, .016f));
        _shopBatteryTarget.SetMeta("suppliedItem", "shop/batteries");
        // The ray is limited to the real base. The upper handle continues to
        // belong to CarryCoordinator, including taking the same lamp afterward.
        _shopBatteryTarget.PresentationRepeatAvailable = () => SupplyPotential("batteries");
        _shopBatteryTarget.PresentationRepeat = () => _ = UseShopSupply("batteries", "portable-light/batteries");
        PublicBox(_shopSupplyLamp, "BatteryBaseLatch", new(.030f, .018f, .008f),
            new(.055f, .045f, .101f), "9ca095", "metal");
        _shopSupplyLamp.AddChild(new Label3D { Name = "BatteryBaseMark", Text = "AA",
            Position = new(-.039f, .046f, .104f), FontSize = 22, PixelSize = .0010f, OutlineSize = 0,
            Modulate = Color.FromHtml("c8cbb8"), Shaded = true, DoubleSided = false });

        var house = _zoneInstances["house_old_pc"];
        // Bind to the selected source instance, independently of the parent's
        // current zone visibility during world construction or a saved load.
        var furniture = house.GetNode<Node3D>("GeneratedHouseInteriorAct1");
        var tray = FindDescendants<MeshInstance3D>(furniture)
            .Single(mesh => mesh.Name == "HouseInterior_TableTray_LOD0" && mesh.Visible);
        var bounds = PublicBuildingShell.Bounds(house, tray);
        // The actual authored kettle occupies the middle/right of this tray.
        // Use its clear front-left corner, keeping the PC and folio untouched.
        var at = new Vector3(bounds.Position.X + .17f, bounds.End.Y + .003f, bounds.End.Z - .12f);
        // The printed front faces the person at the table, as on the shop shelf.
        _shopTeaPackage = new Node3D { Name = "TeaBroughtHome", Position = at,
            RotationDegrees = new(0, 180, 0), Visible = false };
        _shopTeaPackage.SetMeta("presentationSource", "shop/used/tea");
        _shopTeaPackage.SetMeta("supportOwner", tray.GetPath().ToString());
        house.AddChild(_shopTeaPackage);
        PublicGoods(_shopTeaPackage, Vector3.Zero, compact: false);
        _shopTeaTarget = SupplyTarget(house, "PutTeaOnHomeTray", ShopTeaUseInteraction,
            "Оставить купленный чай на домашнем подносе", at + Vector3.Up * .08f, new(.31f, .18f, .25f));
        _shopTeaTarget.SetMeta("suppliedItem", "shop/tea");
        _shopTeaTarget.SetMeta("supportOwner", tray.GetPath().ToString());
        _shopTeaTarget.PresentationRepeatAvailable = () => SupplyPotential("tea");
        _shopTeaTarget.PresentationRepeat = () => _ = UseShopSupply("tea", "house/tea");

        AddChild(new Act1FacilityPresentation
        {
            Name = "ShopSupplyPresentation",
            Tick = _ =>
            {
                if (_shopSupplyBridge is null && _runtimeBridge?.SessionIdentity is not null)
                {
                    _shopSupplyBridge = _runtimeBridge;
                    _shopSupplyBridge.RuntimeStateChanged += ApplyShopSupplyState;
                    ApplyShopSupplyState();
                }
                RefreshShopSupplyTargets();
            },
            Detached = () =>
            {
                if (_shopSupplyBridge is not null && IsInstanceValid(_shopSupplyBridge))
                    _shopSupplyBridge.RuntimeStateChanged -= ApplyShopSupplyState;
                _shopSupplyBridge = null;
            }
        });
    }

    private static InteractionTarget SupplyTarget(Node3D parent, string name, string id, string prompt, Vector3 at, Vector3 size)
    {
        var target = new InteractionTarget { Name = name, InteractionId = id, Prompt = prompt,
            Position = at, CollisionLayer = 4, CollisionMask = 0 };
        target.AddChild(new CollisionShape3D { Name = "UseRay", Shape = new BoxShape3D { Size = size } });
        target.SetMeta("runtimeStateOwner", "RuntimeBridge/shop.use + existing custody/world.props");
        parent.AddChild(target);
        return target;
    }

    private bool ShopSupplyUsed(string sku)
    {
        var props = _shopSupplyBridge?.SelectWorldProps() ?? _runtimeBridge?.SelectWorldProps() ?? default;
        return props.ValueKind == JsonValueKind.Object && props.TryGetProperty("shop/used/" + sku, out var item)
            && item.TryGetProperty("used", out var used) && used.ValueKind == JsonValueKind.True;
    }

    private bool SupplyPotential(string sku)
    {
        var bridge = _shopSupplyBridge ?? _runtimeBridge;
        if (bridge?.HasPocketShopItem(sku) != true || ShopSupplyUsed(sku)) return false;
        return sku == "tea" ? ActiveZoneId == "house_old_pc"
            : _shopSupplyLamp is { IsConcealed: false } lamp && lamp.IsVisibleInTree()
                && lamp.State != CarryableProp.CarryState.Held;
    }

    private void ApplyShopSupplyState()
    {
        // These are projections of the already committed use fact. Resetting or
        // loading a pre-purchase session restores the exact original lamp values.
        var installed = ShopSupplyUsed("batteries");
        if (_shopSupplyLight is not null)
        {
            _shopSupplyLight.LightEnergy = installed ? _shopSupplyBaseEnergy * (1.7f / 1.3f) : _shopSupplyBaseEnergy;
            _shopSupplyLight.OmniRange = installed ? _shopSupplyBaseRange * 1.125f : _shopSupplyBaseRange;
            _shopSupplyLight.SetMeta("batteryUseSource", installed ? "shop/used/batteries" : "authored-default");
        }
        if (_shopSupplyGlow is not null)
            _shopSupplyGlow.EmissionEnergyMultiplier = installed ? _shopSupplyBaseEmission * 1.1666667f : _shopSupplyBaseEmission;
        if (_shopTeaPackage is not null) _shopTeaPackage.Visible = ShopSupplyUsed("tea");
        RefreshShopSupplyTargets();
    }

    private void RefreshShopSupplyTargets()
    {
        // Availability does not depend on the transient async busy flag: a save
        // cannot permanently remove this target from the world interaction ray.
        if (_shopBatteryTarget is not null)
        {
            _shopBatteryTarget.CollisionLayer = SupplyPotential("batteries") ? 4u : 0u;
            _shopBatteryTarget.Prompt = _shopSupplyLamp?.LightOn == true
                ? "Перед заменой батареек выключить фонарь" : "Заменить батарейки в основании фонаря";
        }
        if (_shopTeaTarget is not null) _shopTeaTarget.CollisionLayer = SupplyPotential("tea") ? 4u : 0u;
    }

    internal bool CanUseShopSupplyInteraction(string id)
    {
        var target = id == ShopBatteryUseInteraction ? _shopBatteryTarget : id == ShopTeaUseInteraction ? _shopTeaTarget : null;
        return target is null || SupplyPhysicalTargetVisible(target, id == ShopBatteryUseInteraction);
    }

    /// <summary>RuntimeBridge checks this immediately before its one atomic
    /// custody-consume plus shop/used transaction, including direct API callers.</summary>
    internal bool CanUseShopSupply(string sku, string useId)
    {
        if (sku == "matches") return false; // The stove owner supplies the atomic optional ignition integration.
        if (sku == "batteries" ? useId != "portable-light/batteries" : sku != "tea" || useId != "house/tea") return false;
        if (!SupplyPotential(sku)
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController { ModalOpen: false, VehicleControlled: false }
            || GetTree().GetFirstNodeInGroup("carry_coordinator") is not CarryCoordinator { HeldItem: null, ActionInProgress: false })
            return false;
        if (sku == "batteries" && _shopSupplyLamp?.LightOn != false) return false;
        var target = sku == "batteries" ? _shopBatteryTarget : _shopTeaTarget;
        return target is not null && SupplyPhysicalTargetVisible(target, sku == "batteries");
    }

    private bool SupplyPhysicalTargetVisible(InteractionTarget target, bool battery)
    {
        if (GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || player.VehicleControlled || !IsInstanceValid(target) || !target.IsVisibleInTree()) return false;
        if (battery)
        {
            if (_shopSupplyLamp is null || !_shopSupplyLamp.IsVisibleInTree()
                || _shopSupplyLamp.IsConcealed || _shopSupplyLamp.State == CarryableProp.CarryState.Held) return false;
        }
        else if (ActiveZoneId != "house_old_pc") return false;
        var camera = GetViewport().GetCamera3D();
        if (camera is null || camera.GlobalPosition.DistanceTo(target.GlobalPosition) > 2.7f) return false;
        var shopExclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
        using var shopExcludeOwner = (global::Godot.Collections.Array)shopExclude;
        using var query = PhysicsRayQueryParameters3D.Create(camera.GlobalPosition, target.GlobalPosition, 3,
            shopExclude);
        query.HitFromInside = true;
        using var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count == 0 || (battery && hit["collider"].AsGodotObject() == _shopSupplyLamp);
    }

    private async Task UseShopSupply(string sku, string useId)
    {
        var bridge = _shopSupplyBridge ?? _runtimeBridge;
        if (_shopSupplyBusy || bridge?.SessionIdentity is not { } session
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player) return;
        if (!CanUseShopSupply(sku, useId))
        {
            var carry = GetTree().GetFirstNodeInGroup("carry_coordinator") as CarryCoordinator;
            player.NotifyTraversal(carry?.HeldItem is not null ? "Сначала поставьте предмет и освободите руки."
                : sku == "batteries" && _shopSupplyLamp?.LightOn == true ? "Сначала выключите фонарь."
                : "Нужно подойти к предмету с купленной упаковкой в кармане.");
            return;
        }
        _shopSupplyBusy = true;
        try
        {
            if (!await bridge.TryUsePocketShopItemAsync(sku, useId)
                || !IsInsideTree() || !ReferenceEquals(session, bridge.SessionIdentity)) return;
            ApplyShopSupplyState();
            var source = sku == "batteries" ? _shopSupplyLamp!.GlobalPosition : _shopTeaTarget!.GlobalPosition;
            UiFoley.PlayWorld(this, source, sku == "batteries" ? "metal_rattle" : "paper_open");
            player.NotifyTraversal(sku == "batteries"
                ? "Батарейки заменены. При включении фонарь будет светить ярче."
                : "Купленный чай оставлен на домашнем подносе.");
        }
        finally { _shopSupplyBusy = false; }
    }
}
