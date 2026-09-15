using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>
/// Test-only first-person evidence for the complete Act I presentation
/// envelope. It launches the production main scene, uses its real player
/// Camera3D and root viewport, and only repositions the test-owned camera
/// between visual checkpoints. Default frames do not mutate narrative state.
/// Explicitly selected discovery-result frames dispatch their authored action
/// and checkpoint; run these only through the protected userdata wrapper.
/// </summary>
public partial class Act1FullRouteCoreWorldCapture : Node
{
    private static int CaptureWidth => DisplayServer.WindowGetSize().X;
    private static int CaptureHeight => DisplayServer.WindowGetSize().Y;
    private const int WarmupFrames = 18;
    private const int SettleFrames = 8;
    private const string OutputArgumentPrefix = "--urman-act1-core-output=";
    private const string ReceiptFileName = "act1_full_route_core_world_receipt.json";

    private static readonly JsonSerializerOptions ReceiptJsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly IReadOnlyList<FrameSpec> Frames =
    [
        // Arrival
        Frame("arrival_forward", "arrival", "village_day", "arrival", GroundedPosition(0f, 9f), new(0f, 1.45f, -10f), "forward", "near-mid-far"),
        Frame("arrival_back", "arrival", "village_day", "arrival", GroundedPosition(0f, 9f), new(0f, 1.45f, 32f), "back", "near-mid-far"),
        Frame("arrival_left", "arrival", "village_day", "arrival", GroundedPosition(0f, 9f), new(-16f, 1.55f, 25f), "left", "lateral"),
        Frame("arrival_right", "arrival", "village_day", "arrival", GroundedPosition(0f, 9f), new(16f, 1.55f, 26f), "right", "lateral"),
        Frame("arrival_depth", "arrival", "village_day", "arrival", GroundedPosition(0f, 14f), new(0f, 1.60f, 45f), "forward", "near-mid-far"),

        // Main street
        Frame("main_street_forward", "main_street", "village_day", "from_house", GroundedPosition(-2.2f, 2.4f), new(0f, 1.45f, -15f), "forward", "near-mid-far"),
        Frame("main_street_back", "main_street", "village_day", "from_house", GroundedPosition(-2.2f, 2.4f), new(0f, 1.45f, 13f), "back", "near-mid-far"),
        Frame("main_street_left", "main_street", "village_day", "from_house", GroundedPosition(-2.2f, 2.4f), new(-18f, 1.55f, -5f), "left", "lateral"),
        Frame("main_street_right", "main_street", "village_day", "from_house", GroundedPosition(-2.2f, 2.4f), new(18f, 1.55f, -9f), "right", "lateral"),
        Frame("main_street_depth", "main_street", "village_day", "from_house", GroundedPosition(0f, -3f), new(0f, 1.60f, -28f), "forward", "near-mid-far"),
        // Close player-height views of the actual branch-side holding, not
        // only the distant street silhouette. Keep both directions auditable.
        Frame("east_holding_entry", "main_street", "village_day", "from_house", new(24f, (float)Urman.Experiments.AgentBAct1.AgentBAct1HeightField.Ground(24f, -20f) + .05f, -20f), new(23f, .45f, -11f), "forward", "near-mid-far"),
        Frame("east_holding_return", "main_street", "village_day", "from_house", new(24f, (float)Urman.Experiments.AgentBAct1.AgentBAct1HeightField.Ground(24f, -15f) + .05f, -15f), new(19f, .45f, -23f), "back", "near-mid-far"),

        // Same player-height pose as the chimney diagnostic that exposed
        // floating face / collar pieces on the existing background character.
        Frame("resident_contact_detail", "main_street", "village_day", "arrival",
            new(-12.559999f, -.1847323f, .5680003f),
            new(-24.56f, 4.3218613f, -7.4319997f), "detail", "existing character face and collar contact"),

        // Babai / Ebi yard
        Frame("alsu_conversation_close", "main_street", "village_day", "arrival", GroundedPosition(.1f, 4.1f), new(.85f, 1.35f, 2.05f), "detail", "Alsu visible beside her interaction"),
        Frame("timur_conversation_close", "connective_street_return", "village_day", "from_house", GroundedPosition(-1.4f, -17.6f), new(-3.8f, 1.4f, -19f), "detail", "Timur outdoor conversation shoulder"),
        Frame("rinat_village_close", "main_street", "village_day", "arrival", GroundedPosition(-.8f, -1.5f), new(-1.5f, 1.4f, -3.8f), "detail", "Rinat before alert"),

        Frame("babai_yard_forward", "babai_yard", "village_day", "from_house", GroundedPosition(-20f, 6.5f), new(-8f, 1.55f, 12f), "forward", "near-mid-far"),
        Frame("babai_yard_back", "babai_yard", "village_day", "from_house", GroundedPosition(-20f, 6.5f), new(-30f, 1.55f, -5f), "back", "near-mid-far"),
        Frame("babai_yard_left", "babai_yard", "village_day", "from_house", GroundedPosition(-20f, 6.5f), new(-34f, 1.55f, 6f), "left", "lateral"),
        Frame("babai_yard_right", "babai_yard", "village_day", "from_house", GroundedPosition(-20f, 6.5f), new(-8f, 1.55f, 5f), "right", "lateral"),
        Frame("babai_yard_depth", "babai_yard", "village_day", "from_house", GroundedPosition(-20f, 9f), new(-20f, 1.60f, 22f), "forward", "near-mid-far"),

        // House exterior / approach
        Frame("house_exterior_forward", "house_exterior", "village_day", "from_house", GroundedPosition(-28f, 7f), new(-28f, 1.55f, -1f), "forward", "near-mid-far"),
        Frame("house_exterior_back", "house_exterior", "village_day", "from_house", GroundedPosition(-28f, 7f), new(-13f, 1.45f, 14f), "back", "near-mid-far"),
        Frame("house_exterior_depth", "house_exterior", "village_day", "from_house", GroundedPosition(-24f, 7.5f), new(-28f, 1.55f, -6f), "forward", "near-mid-far"),

        Frame("babai_door_detail", "house_exterior", "village_day", "from_house",
            new(-28.2f, AgentBAct1HeightField.CollisionGround(-28.2f, 6f) + .05f, 6f),
            new(-29.7f, AgentBAct1HeightField.CollisionGround(-29.7f, 4.3f) + .65f, 4.3f), "detail", "cleared doorstep and shovel"),
        Frame("babai_firewood_detail", "babai_yard", "village_day", "from_house",
            new(-28.2f, AgentBAct1HeightField.CollisionGround(-28.2f, 6f) + .05f, 6f),
            new(-33.15f, AgentBAct1HeightField.CollisionGround(-33.15f, 3.88f) + .7f, 3.88f), "detail", "firewood under shelter"),

        Frame("arrival_notches", "arrival", "village_day", "from_house", new(4.9f, AgentBAct1HeightField.CollisionGround(4.9f, 7.8f) + .05f, 7.8f), new(4.9f, 0.66f, 6.3f), "detail", "optional discovery before action"),
        Frame("arrival_notches_used", "arrival", "village_day", "from_house", new(4.9f, AgentBAct1HeightField.CollisionGround(4.9f, 7.8f) + .05f, 7.8f), new(4.9f, 0.66f, 6.3f), "detail", "optional discovery after action", "arrival-bench-race-notches"),
        Frame("arrival_mitten", "arrival", "village_day", "from_house", new(-2.5f, AgentBAct1HeightField.CollisionGround(-2.5f, 5.7f) + .05f, 5.7f), new(-4.2f, 1.1f, 4.6f), "detail", "optional discovery before action"),
        Frame("arrival_mitten_used", "arrival", "village_day", "from_house", new(-2.5f, AgentBAct1HeightField.CollisionGround(-2.5f, 5.7f) + .05f, 5.7f), new(-4.2f, 1.1f, 4.6f), "detail", "optional discovery after action", "arrival-insulated-well"),
        Frame("sign_reverse", "main_street", "village_day", "from_house", new(-1.8f, AgentBAct1HeightField.CollisionGround(-1.8f, 7.6f) + .05f, 7.6f), new(-2.05f, 1.28f, 5.8f), "detail", "optional discovery before action"),
        Frame("sign_reverse_used", "main_street", "village_day", "from_house", new(-1.8f, AgentBAct1HeightField.CollisionGround(-1.8f, 7.6f) + .05f, 7.6f), new(-2.05f, 1.28f, 5.8f), "detail", "optional discovery after action", "main-street-sign-reverse"),
        Frame("yard_spinner", "babai_yard", "village_day", "from_house", new(-32.1f, AgentBAct1HeightField.CollisionGround(-32.1f, 5.4f) + .05f, 5.4f), new(-33.15f, 1.6f, 3.88f), "detail", "optional discovery before action"),
        Frame("yard_spinner_used", "babai_yard", "village_day", "from_house", new(-32.1f, AgentBAct1HeightField.CollisionGround(-32.1f, 5.4f) + .05f, 5.4f), new(-33.15f, 1.6f, 3.88f), "detail", "optional discovery after action", "babai-yard-childhood-spinner"),
        Frame("yard_sled", "babai_yard", "village_day", "from_house", new(-25.4f, AgentBAct1HeightField.CollisionGround(-25.4f, 4.77f) + .05f, 4.77f), new(-27.2f, AgentBAct1HeightField.CollisionGround(-27.2f, 3.92f) + .45f, 3.92f), "detail", "optional discovery before action"),
        Frame("yard_sled_used", "babai_yard", "village_day", "from_house", new(-25.4f, AgentBAct1HeightField.CollisionGround(-25.4f, 4.77f) + .05f, 4.77f), new(-27.2f, AgentBAct1HeightField.CollisionGround(-27.2f, 3.92f) + .45f, 3.92f), "detail", "optional discovery after action", "babai-yard-sled-repair"),
        Frame("porch_nook", "house_exterior", "village_day", "from_house", new(-25.8f, AgentBAct1HeightField.CollisionGround(-25.8f, -.36f) + .05f, -.36f), new(-27.5f, AgentBAct1HeightField.CollisionGround(-27.5f, -1.14f) + .2f, -1.14f), "detail", "optional discovery before action"),
        Frame("porch_nook_used", "house_exterior", "village_day", "from_house", new(-25.8f, AgentBAct1HeightField.CollisionGround(-25.8f, -.36f) + .05f, -.36f), new(-27.5f, AgentBAct1HeightField.CollisionGround(-27.5f, -1.14f) + .2f, -1.14f), "detail", "optional discovery after action", "house-exterior-porch-nook"),
        Frame("fap_service_gate", "fap_exterior", "village_day", "from_house", new(34.5f, AgentBAct1HeightField.CollisionGround(34.5f, -22.7f) + .05f, -22.7f), new(34.5f, 0.8f, -25.2f), "detail", "optional discovery before action"),
        Frame("fap_service_gate_used", "fap_exterior", "village_day", "from_house", new(34.5f, AgentBAct1HeightField.CollisionGround(34.5f, -22.7f) + .05f, -22.7f), new(34.5f, 0.8f, -25.2f), "detail", "optional discovery after action", "fap-exterior-service-path"),

        Frame("return_care_bench", "connective_street", "village_day", "from_house", new(-3.8f, AgentBAct1HeightField.CollisionGround(-3.8f, -38.5f) + .05f, -38.5f), new(-5.6f, 0.35f, -38.8f), "detail", "optional discovery before action"),
        Frame("return_care_bench_used", "connective_street", "village_day", "from_house", new(-3.8f, AgentBAct1HeightField.CollisionGround(-3.8f, -38.5f) + .05f, -38.5f), new(-5.6f, 0.35f, -38.8f), "detail", "optional discovery after action", "connective-street-return-bench"),
        Frame("fap_care_cup", "fap_exterior", "village_day", "from_house", new(27.25f, AgentBAct1HeightField.CollisionGround(27.25f, -26.5f) + .05f, -26.5f), new(27.8f, 0.38f, -28.15f), "detail", "optional discovery before action"),
        Frame("fap_care_cup_used", "fap_exterior", "village_day", "from_house", new(27.25f, AgentBAct1HeightField.CollisionGround(27.25f, -26.5f) + .05f, -26.5f), new(27.8f, 0.38f, -28.15f), "detail", "optional discovery after action", "fap-exterior-care-porch"),
        Frame("street_side_window", "main_street", "village_day", "from_house", new(6.7f, AgentBAct1HeightField.CollisionGround(6.7f, -10.2f) + .05f, -10.2f), new(9.0f, 0.95f, -10.2f), "detail", "optional discovery before action"),
        Frame("street_side_window_used", "main_street", "village_day", "from_house", new(6.7f, AgentBAct1HeightField.CollisionGround(6.7f, -10.2f) + .05f, -10.2f), new(9.0f, 0.95f, -10.2f), "detail", "optional discovery after action", "main-street-side-window"),
        Frame("street_repair_bench", "connective_street", "village_day", "from_house", new(-9.8f, AgentBAct1HeightField.CollisionGround(-9.8f, -26.1f) + .05f, -26.1f), new(-11.84f, 0.5f, -26.58f), "detail", "optional discovery before action"),
        Frame("street_repair_bench_used", "connective_street", "village_day", "from_house", new(-9.8f, AgentBAct1HeightField.CollisionGround(-9.8f, -26.1f) + .05f, -26.1f), new(-11.84f, 0.5f, -26.58f), "detail", "optional discovery after action", "connective-street-repair-bench"),

        Frame("yard_side_board", "babai_yard", "village_day", "from_house", new(-23.2f, AgentBAct1HeightField.CollisionGround(-23.2f, -.7f) + .05f, -.7f), new(-25.2f, .80f, -.7f), "detail", "side opening before action"),
        Frame("yard_side_board_used", "babai_yard", "village_day", "from_house", new(-23.2f, AgentBAct1HeightField.CollisionGround(-23.2f, -.7f) + .05f, -.7f), new(-25.2f, .80f, -.7f), "detail", "side opening after action", "babai-yard-loose-side-gate-board"),

        // House interior
        Frame("house_photo_detail", "house_interior", "house_old_pc", "entry", new(-30.1f, .05f, -2.9f), new(-30.1f, 1.45f, -4.60f), "detail", "painted family photograph front"),
        Frame("house_tin_detail", "house_interior", "house_old_pc", "entry", new(-31.97f, .05f, 2.0f), new(-31.97f, .98f, 3.70f), "detail", "sewing tin on the threshold chest"),
        Frame("fap_height_detail", "fap_interior", "fap_clinic", "waiting_room", new(28.86f, .05f, -25.5f), new(28.86f, 1.15f, -24.49f), "detail", "pencil growth marks inside entry"),
        Frame("fap_height_used", "fap_interior", "fap_clinic", "waiting_room", new(28.86f, .05f, -25.5f), new(28.86f, 1.15f, -24.49f), "detail", "growth marks after removing loose paint", "fap-interior-height-marks"),
        Frame("fap_lamp_detail", "fap_interior", "fap_clinic", "waiting_room", new(29.17f, .05f, -32.85f), new(29.17f, 1.15f, -34.15f), "detail", "repaired desk lamp before use"),
        Frame("house_photo_used", "house_interior", "house_old_pc", "entry", new(-30.1f, .05f, -2.9f), new(-30.1f, 1.45f, -4.60f), "detail", "photo after authored action", "house-interior-photo-back"),
        Frame("house_tin_used", "house_interior", "house_old_pc", "entry", new(-31.97f, .05f, 2.0f), new(-31.97f, .98f, 3.70f), "detail", "open tin after authored action", "house-interior-language-tin"),
        Frame("fap_lamp_used", "fap_interior", "fap_clinic", "waiting_room", new(29.17f, .05f, -32.85f), new(29.17f, 1.15f, -34.15f), "detail", "lamp after authored action", "fap-interior-repaired-desk-object"),
        Frame("mansur_conversation_close", "house_interior", "house_old_pc", "entry", new(-24.7f, .05f, -.2f), new(-25.2f, 1.48f, -1.45f), "detail", "Mansur face at conversation distance"),
        Frame("gulsina_conversation_close", "house_interior", "house_old_pc", "entry", new(-32.0f, .05f, -1.15f), new(-31.2f, 1.10f, -2.8f), "detail", "Gulsina hands and folded household towel"),
        Frame("naila_conversation_close", "fap_interior", "fap_clinic", "waiting_room", new(29.8f, .05f, -28.6f), new(29.8f, 1.45f, -30f), "detail", "Naila at the reception counter, conversation distance"),
        Frame("house_interior_forward", "house_interior", "house_old_pc", "entry", new(-25.2f, .05f, 1.8f), new(-28f, 1.45f, -2.2f), "forward", "interior-360"),
        Frame("house_interior_back", "house_interior", "house_old_pc", "entry", new(-27.4f, .05f, -1.6f), new(-29f, 1.45f, 3.2f), "back", "interior-360"),
        Frame("house_interior_left", "house_interior", "house_old_pc", "entry", new(-25.2f, .05f, -0.2f), new(-32.2f, 1.5f, -0.8f), "left", "interior-360"),
        Frame("house_interior_right", "house_interior", "house_old_pc", "entry", new(-31.8f, .05f, 1.8f), new(-24.2f, 1.5f, -1f), "right", "interior-360"),

        // Connective street and return
        Frame("connective_street_return_forward", "connective_street_return", "village_day", "from_forest", GroundedPosition(-15.4f, -22f), new(-7f, 1.50f, -40f), "forward", "near-mid-far"),
        Frame("connective_street_return_back", "connective_street_return", "village_day", "from_forest", GroundedPosition(-7f, -41.5f), new(-20f, 1.50f, -24f), "back", "near-mid-far"),
        Frame("connective_street_return_depth", "connective_street_return", "village_day", "from_forest", GroundedPosition(-3f, -50f), new(0f, 1.60f, -67f), "forward", "near-mid-far"),

        // FAP exterior
        Frame("fap_exterior_forward", "fap_exterior", "village_day", "arrival", GroundedPosition(14f, -16f), new(29f, 1.60f, -30f), "forward", "near-mid-far"),
        Frame("fap_exterior_back", "fap_exterior", "village_day", "arrival", GroundedPosition(14f, -16f), new(4f, 1.50f, -8f), "back", "near-mid-far"),
        Frame("fap_exterior_left", "fap_exterior", "village_day", "arrival", GroundedPosition(14f, -16f), new(17f, 1.55f, -28f), "left", "lateral"),
        Frame("fap_exterior_right", "fap_exterior", "village_day", "arrival", GroundedPosition(14f, -16f), new(33f, 1.55f, -21f), "right", "lateral"),
        Frame("fap_exterior_depth", "fap_exterior", "village_day", "arrival", GroundedPosition(19f, -21f), new(29f, 1.60f, -33f), "forward", "near-mid-far"),

        // FAP interior
        Frame("fap_interior_forward", "fap_interior", "fap_clinic", "waiting_room", new(29.6f, .05f, -27.2f), new(28f, 1.45f, -32.2f), "forward", "interior-360"),
        Frame("fap_interior_back", "fap_interior", "fap_clinic", "waiting_room", new(28f, .05f, -31.6f), new(28f, 1.45f, -26.2f), "back", "interior-360"),
        Frame("fap_interior_left", "fap_interior", "fap_clinic", "waiting_room", new(30f, .05f, -30.4f), new(23.8f, 1.45f, -29.2f), "left", "interior-360"),
        Frame("fap_interior_right", "fap_interior", "fap_clinic", "waiting_room", new(26f, .05f, -30.4f), new(32.2f, 1.45f, -29.2f), "right", "interior-360"),

        // Zirat
        Frame("zirat_rest_bench", "zirat", "village_day", "from_house", new(-4.5f, AgentBAct1HeightField.CollisionGround(-4.5f, -50.4f) + .05f, -50.4f), new(-5f, AgentBAct1HeightField.CollisionGround(-5f, -52f) + .48f, -52f), "detail", "roadside repaired bench before brushing"),
        Frame("zirat_rest_bench_used", "zirat", "village_day", "from_house", new(-4.5f, AgentBAct1HeightField.CollisionGround(-4.5f, -50.4f) + .05f, -50.4f), new(-5f, AgentBAct1HeightField.CollisionGround(-5f, -52f) + .48f, -52f), "detail", "new seat plank after brushing", "zirat-outer-rest-bench"),
        Frame("zirat_clue_close", "zirat", "zirat_road", "village_side", GroundedPosition(-3.2f, -74.1f), new(-3.55f, .62f, -75.70f), "left", "near"),
        Frame("zirat_forward", "zirat", "zirat_road", "village_side", GroundedPosition(0f, -56f), new(0f, 1.50f, -75f), "forward", "near-mid-far"),
        Frame("zirat_back", "zirat", "zirat_road", "village_side", GroundedPosition(0f, -56f), new(0f, 1.50f, -44f), "back", "near-mid-far"),
        Frame("zirat_left", "zirat", "zirat_road", "village_side", GroundedPosition(0f, -56f), new(-16f, 1.55f, -64f), "left", "lateral"),
        Frame("zirat_right", "zirat", "zirat_road", "village_side", GroundedPosition(0f, -56f), new(16f, 1.55f, -68f), "right", "lateral"),
        Frame("zirat_depth", "zirat", "zirat_road", "village_side", GroundedPosition(0f, -72f), new(0f, 1.60f, -92f), "forward", "near-mid-far"),

        // Last inhabited holding: outside entry and inside return sightlines.
        Frame("zirat_holding_entry", "zirat", "zirat_road", "village_side", new(-17f, (float)AgentBAct1HeightField.Ground(-17f, -63f) + .05f, -63f), new(-28f, 1f, -64f), "left", "near-mid-far"),
        Frame("zirat_holding_return", "zirat", "zirat_road", "village_side", new(-23f, (float)AgentBAct1HeightField.Ground(-23f, -63.8f) + .05f, -63.8f), new(-7f, 1f, -61f), "back", "near-mid-far"),

        // Kara-Urman edge / cliffhanger approach
        Frame("rear_minaret_view", "house_exterior", "village_day", "from_house", new(-33.8311f, AgentBAct1HeightField.CollisionGround(-33.8311f, -8.711206f) + .05f, -8.711206f), new(-46f, 13f, -34f), "detail", "actual rear corner view toward village minaret"),
        Frame("zirat_culvert", "zirat", "zirat_road", "village_side", new(1.3f, AgentBAct1HeightField.CollisionGround(1.3f, -67f) + .05f, -67f), new(3.65f, .3f, -67f), "detail", "outer roadside footbridge before clearing"),
        Frame("zirat_culvert_used", "zirat", "zirat_road", "village_side", new(1.3f, AgentBAct1HeightField.CollisionGround(1.3f, -67f) + .05f, -67f), new(3.65f, .3f, -67f), "detail", "outer roadside footbridge after clearing", "zirat-outer-culvert-crossing"),
        Frame("kara_warm_window", "kara_approach", "kara_urman_night", "village_path", new(4.45f, AgentBAct1HeightField.CollisionGround(4.45f, -114.55f) + .05f, -114.55f), new(-8.8f, 1.35f, -97.2f), "detail", "actual window sightline from optional clearing"),
        Frame("kara_profile_front", "kara_approach", "kara_urman_night", "village_path", new(3.4f, AgentBAct1HeightField.CollisionGround(3.4f, -114.4f) + .05f, -114.4f), new(6.0f, 1.9f, -117.0f), "detail", "ambiguous branch before stepping sideways"),
        Frame("kara_profile_side", "kara_approach", "kara_urman_night", "village_path", new(8.8f, AgentBAct1HeightField.CollisionGround(8.8f, -117.4f) + .05f, -117.4f), new(6.0f, 1.9f, -117.0f), "detail", "ordinary branch from the side"),
        Frame("kara_approach_forward", "kara_approach", "kara_urman_night", "village_path", GroundedPosition(0f, -103f), new(0f, 1.50f, -120f), "forward", "near-mid-far"),
        Frame("kara_approach_back", "kara_approach", "kara_urman_night", "village_path", GroundedPosition(0f, -103f), new(0f, 1.50f, -93f), "back", "near-mid-far"),
        Frame("kara_approach_left", "kara_approach", "kara_urman_night", "village_path", GroundedPosition(0f, -103f), new(-12f, 1.85f, -116f), "left", "lateral"),
        Frame("kara_approach_right", "kara_approach", "kara_urman_night", "village_path", GroundedPosition(0f, -103f), new(12f, 1.85f, -118f), "right", "lateral"),
        Frame("kara_approach_depth", "kara_approach", "kara_urman_night", "village_path", GroundedPosition(0f, -112f), new(0f, 1.90f, -132f), "forward", "near-mid-far")
    ];

    private static readonly IReadOnlyList<WaypointSpec> Waypoints =
    [
        new("arrival", new(0f, .05f, 9f)),
        new("babai_yard", new(-24.4f, .05f, 1.7f)),
        new("house_exterior", new(-30.3f, .05f, 1.2f)),
        new("main_street", new(-2.2f, .05f, 2.4f)),
        new("connective_street", new(-15.4f, .05f, -22f)),
        new("fap_exterior", new(22f, .05f, -22f)),
        new("return_street", new(-7f, .05f, -41.5f)),
        new("zirat", new(0f, .05f, -53.5f)),
        new("kara_approach", new(0f, .05f, -103f)),
        new("kara_cliffhanger_endpoint", new(0f, .05f, -122.5f))
    ];

    public override async void _Ready()
    {
        try
        {
            await CaptureAsync();
        }
        catch (Exception exception)
        {
            Fail($"Act I full-route core-world capture failed: {exception}");
        }
    }

    private async Task CaptureAsync()
    {
        if (System.Environment.GetEnvironmentVariable("URMAN_CAPTURE_BENCHMARK") == "1"
            && (System.Environment.GetEnvironmentVariable("URMAN_CAPTURE_MONO_SNOW") == "1"
                || System.Environment.GetEnvironmentVariable("URMAN_CAPTURE_SNOW_SWEEP") == "1"))
            throw new InvalidOperationException("Diagnostic material/sweep capture cannot be a production benchmark.");
        var outputDirectory = Path.GetFullPath(RequireArgument(OutputArgumentPrefix));
        if (!Directory.Exists(outputDirectory))
        {
            throw new DirectoryNotFoundException($"Capture output directory does not exist: {outputDirectory}");
        }

        if (RenderingServer.GetRenderingDevice() is null)
        {
            throw new InvalidOperationException("The full-route capture requires a real Godot 3D rendering device.");
        }

        var packedMain = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")
            ?? throw new InvalidOperationException("Could not load the production res://scenes/main.tscn.");
        var main = packedMain.Instantiate<Main>()
            ?? throw new InvalidOperationException("The production main scene did not instantiate Main.");
        main.InitialZoneId = "village_day";
        main.InitialSpawnPointId = "arrival";
        main.EnableAct1ConnectedWorld = true;
        AddChild(main);

        await WaitForFramesAsync(WarmupFrames);
        await WaitForRenderedFrameAsync();

        var connectedWorld = main.ConnectedWorld
            ?? throw new InvalidOperationException("Production Main did not create Act1ConnectedWorld.");
        var core = connectedWorld.GetNodeOrNull<Node3D>("Act1CoreWorldGreybox")
            ?? throw new InvalidOperationException("Act1ConnectedWorld is missing Act1CoreWorldGreybox.");
        var presentationAudit = AuditPresentationOnlyLayer(core);
        var backdropGround = FindDescendants(core).OfType<MeshInstance3D>()
            .Where(mesh => mesh.Mesh is not null && (mesh.Name == "BackdropGround" || mesh.Name == "RidgeSurface"))
            .Select(mesh => (Vertices: mesh.Mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array().Select(mesh.ToGlobal).ToArray(),
                Indices: mesh.Mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Index].AsInt32Array())).ToArray();
        var footingCount = 0;
        foreach (var footing in FindDescendants(core).OfType<Node3D>().Where(node => node.HasMeta("groundContactDepth")))
        {
            var point = footing.GlobalPosition;
            float? support = null;
            foreach (var ground in backdropGround)
            for (var i = 0; i < ground.Indices.Length; i += 3)
            {
                var a = ground.Vertices[ground.Indices[i]]; var b = ground.Vertices[ground.Indices[i + 1]]; var c = ground.Vertices[ground.Indices[i + 2]];
                var denominator = (b.Z - c.Z) * (a.X - c.X) + (c.X - b.X) * (a.Z - c.Z);
                var u = ((b.Z - c.Z) * (point.X - c.X) + (c.X - b.X) * (point.Z - c.Z)) / denominator;
                var v = ((c.Z - a.Z) * (point.X - c.X) + (a.X - c.X) * (point.Z - c.Z)) / denominator;
                if (u < -.00001f || v < -.00001f || u + v > 1.00001f) continue;
                support = Mathf.Max(support ?? float.MinValue, u * a.Y + v * b.Y + (1 - u - v) * c.Y);
            }
            support ??= AgentBAct1HeightField.CollisionGround(point.X, point.Z);
            var expected = support.Value - footing.GetMeta("groundContactDepth").AsSingle();
            if (Mathf.Abs(point.Y - expected) > .005f)
                throw new InvalidOperationException($"Backdrop footing is not grounded to rendered triangles: {footing.GetPath()}");
            footingCount++;
        }
        GD.Print($"winter-backdrop-contact: {footingCount} roots checked against rendered apron / ridges / physical terrain triangles");
        foreach (var bank in FindDescendants(core).OfType<MeshInstance3D>().Where(node => node.HasMeta("snowBankHeight")))
        {
            var vertices = bank.Mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            foreach (var vertex in vertices)
            {
                var point = bank.GlobalTransform * vertex;
                var road = AgentBAct1HeightField.RoadInfo(point.X, point.Z);
                var rise = point.Y - AgentBAct1HeightField.CollisionGround(point.X, point.Z);
                if (!point.IsFinite() || (road.Distance <= road.HalfWidth && Mathf.Abs(rise) > .05f))
                    throw new InvalidOperationException($"Snow bank obstructs the visible route: {bank.GetPath()}");
            }
        }
        foreach (var mesh in FindDescendants(core).OfType<MeshInstance3D>().Where(mesh => mesh.IsVisibleInTree() && mesh.Mesh is not null))
        {
            var name = mesh.Name.ToString().ToLowerInvariant();
            if (!name.Contains("stone") && !name.Contains("rock") && !name.Contains("boulder")) continue;
            var center = mesh.GlobalTransform * mesh.Mesh.GetAabb().GetCenter();
            var road = AgentBAct1HeightField.RoadInfo(center.X, center.Z);
            if (center.Z > -86f && road.Distance < Math.Min(road.HalfWidth, 2.0))
                GD.Print($"winter-road-debris: {mesh.GetPath()} center={center}");
        }
        if (System.Environment.GetEnvironmentVariable("URMAN_CAPTURE_PLACEMENT_AUDIT") == "1")
        {
            var intrusions = new List<object>();
            foreach (var mesh in FindDescendants(core).OfType<MeshInstance3D>().Where(mesh => mesh.IsVisibleInTree()
                && mesh.Mesh is not null && mesh.Name.ToString().Contains("Fence", StringComparison.OrdinalIgnoreCase)))
            {
                var bounds = mesh.GlobalTransform * mesh.Mesh.GetAabb();
                if (bounds.Position.Y > AgentBAct1HeightField.CollisionGround(bounds.GetCenter().X, bounds.GetCenter().Z) + 2) continue;
                var worst = 0f; var sample = Vector3.Zero;
                for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
                foreach (var vertex in mesh.Mesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                {
                    var point = mesh.ToGlobal(vertex);
                    var road = AgentBAct1HeightField.RoadInfo(point.X, point.Z);
                    var depth = (float)(road.HalfWidth - road.Distance);
                    if (depth <= worst) continue;
                    worst = depth; sample = point;
                }
                if (worst > .1f) intrusions.Add(new { owner = mesh.GetPath().ToString(), intrusion_m = worst,
                    x = sample.X, y = sample.Y, z = sample.Z });
            }
            File.WriteAllText(Path.Combine(outputDirectory, "fence_road_intrusions.json"), JsonSerializer.Serialize(intrusions, ReceiptJsonOptions));
        }
        var maxRoadGap = 0f;
        var minRoadGap = float.MaxValue;
        var worstRoadPoint = Vector3.Zero;
        var roadVertexCount = 0;
        foreach (var roadMesh in FindDescendants(core.GetNode<Node3D>("AgentBExteriorWorld/AgentB_TerrainRoadKit"))
            .OfType<MeshInstance3D>().Where(mesh => mesh.IsVisibleInTree() && mesh.Name.ToString().StartsWith("Road_", StringComparison.Ordinal)))
        {
            var roadArrays = roadMesh.Mesh.SurfaceGetArrays(0);
            var roadVertices = roadArrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var roadIndices = roadArrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            var centers = Enumerable.Range(0, roadIndices.Length / 3).Select(index =>
                (roadVertices[roadIndices[index * 3]] + roadVertices[roadIndices[index * 3 + 1]] + roadVertices[roadIndices[index * 3 + 2]]) / 3f);
            var roadTransform = roadMesh.GlobalTransform;
            foreach (var vertex in roadVertices.Concat(centers))
            {
                var point = roadTransform * vertex;
                var query = PhysicsRayQueryParameters3D.Create(point + Vector3.Up, point - Vector3.Up, 1);
                var hit = core.GetWorld3D().DirectSpaceState.IntersectRay(query);
                if (hit.Count == 0 || hit["collider"].AsGodotObject() is not Node collider
                    || !collider.HasMeta("collisionOwner") || collider.GetMeta("collisionOwner").AsString() != "act1-exterior-terrain") continue;
                roadVertexCount++;
                var signedGap = point.Y - hit["position"].AsVector3().Y;
                minRoadGap = Mathf.Min(minRoadGap, signedGap);
                var gap = Mathf.Abs(signedGap);
                if (gap > maxRoadGap) { maxRoadGap = gap; worstRoadPoint = point; }
            }
        }
        GD.Print($"winter-road-collision: samples={roadVertexCount} max_gap={maxRoadGap:F4}m min_gap={minRoadGap:F4}m at {worstRoadPoint}");
        if (roadVertexCount == 0 || minRoadGap < -.001f)
            throw new InvalidOperationException($"Road intersects terrain or lacks probes: minimum gap {minRoadGap}");
        if (maxRoadGap > .051f)
            throw new InvalidOperationException($"Road presentation/collider gap exceeds 5cm: {maxRoadGap} at {worstRoadPoint}");
        var foliage = core.GetNode<Node3D>("AgentBExteriorWorld/AgentB_PlantedFoliage");
        var foliageGeometry = new List<object>();
        var exterior = core.GetNode<AgentBAct1ExteriorLayer>("AgentBExteriorWorld");
        foreach (var species in new[] { "Birch", "Linden", "BirdCherry", "Spruce" })
        foreach (var (prefix, low, high) in new[] { ("Winter", 2000, 6000), ("WinterLight", 600, 1500), ("WinterFar", 100, 400) })
        {
            var variant = $"{prefix}{species}_1";
            var mesh = exterior.FoliageMesh(variant, "village");
            var triangles = Enumerable.Range(0, mesh.GetSurfaceCount()).Sum(surface =>
            {
                var arrays = mesh.SurfaceGetArrays(surface);
                var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                return (indices.Length > 0 ? indices.Length : arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length) / 3;
            });
            if (triangles < low || triangles > high || mesh.GetSurfaceCount() > 3)
                throw new InvalidOperationException($"Winter geometry budget: {variant} {triangles} triangles, {mesh.GetSurfaceCount()} surfaces");
            foliageGeometry.Add(new { variant, triangles, surfaces = mesh.GetSurfaceCount() });
        }
        File.WriteAllText(Path.Combine(outputDirectory, "foliage_geometry.json"), JsonSerializer.Serialize(foliageGeometry, ReceiptJsonOptions));
        var foliageVerticesChecked = 0;
        void CheckFoliage(Mesh source, Transform3D transform, Node owner)
        {
            for (var surface = 0; surface < source.GetSurfaceCount(); surface++)
            foreach (var vertex in source.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
            {
                var point = transform * vertex;
                if (point.Y > 6f) continue;
                foliageVerticesChecked++;
                var road = AgentBAct1HeightField.RoadInfo(point.X, point.Z);
                if (road.Distance > road.HalfWidth) continue;
                if (point.Y < AgentBAct1HeightField.CollisionGround(point.X, point.Z) + 2.5f)
                    throw new InvalidOperationException($"Low foliage enters cleared road: {owner.GetPath()} at {point}");
            }
        }
        foreach (var mesh in FindDescendants(foliage).OfType<MeshInstance3D>().Where(mesh => mesh.IsVisibleInTree() && mesh.Mesh is ArrayMesh))
            CheckFoliage(mesh.Mesh, mesh.GlobalTransform, mesh);
        foreach (var group in FindDescendants(foliage).OfType<MultiMeshInstance3D>().Where(group => group.IsVisibleInTree()))
        {
            var multi = group.Multimesh;
            for (var instance = 0; instance < multi.InstanceCount; instance++)
                CheckFoliage(multi.Mesh, group.GlobalTransform * multi.GetInstanceTransform(instance), group);
        }
        GD.Print($"winter-road-foliage: checked={foliageVerticesChecked} low foliage vertices; trunks/boughs clear");
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController
            ?? throw new InvalidOperationException("Production first-person player is missing.");
        var camera = player.GetNode<Camera3D>("Head/Camera3D");
        var viewport = GetViewport();
        player.SetPhysicsProcess(false);
        camera.Current = true;

        var visibleMaterials = FindDescendants(core).OfType<MeshInstance3D>()
            .Where(mesh => mesh.IsVisibleInTree() && mesh.Mesh is not null)
            .SelectMany(mesh => Enumerable.Range(0, mesh.Mesh!.GetSurfaceCount())
                .Select(index => mesh.GetActiveMaterial(index)))
            .OfType<ShaderMaterial>().Distinct().ToArray();
        GD.Print($"act1-capture-materials: preset={player.GraphicsPreset} " +
            $"shader_materials={visibleMaterials.Length} " +
            $"textured={visibleMaterials.Count(material => material.GetShaderParameter("has_albedo_texture").AsBool())} " +
            $"low_quality={visibleMaterials.Count(material => material.GetShaderParameter("low_quality").AsBool())}");

        var allFrames = Frames.Concat(PerimeterFrames).ToList();
        var captures = new List<FrameReceipt>(allFrames.Count);
        if (System.Environment.GetEnvironmentVariable("URMAN_CAPTURE_MONO_SNOW") == "1")
        {
            foreach (var material in FindDescendants(main).OfType<MeshInstance3D>()
                .SelectMany(mesh => Enumerable.Range(0, mesh.Mesh?.GetSurfaceCount() ?? 0)
                    .Select(mesh.GetActiveMaterial)).OfType<ShaderMaterial>().Distinct())
            {
                if (!material.GetShaderParameter("snow_material").AsBool()) continue;
                material.SetShaderParameter("has_albedo_texture", false);
                material.SetShaderParameter("base_color", Color.FromHtml("e8edf0"));
                material.SetShaderParameter("variation", 0f);
                material.SetShaderParameter("snow_sparkle", 0f);
                material.SetShaderParameter("has_snow_micro", false);
                material.SetShaderParameter("vertex_pigment", false);
            }
        }
        var shift = new Vector3(float.TryParse(System.Environment.GetEnvironmentVariable("URMAN_CAPTURE_SHIFT_X"),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var shiftX) ? shiftX : 0f, 0f, 0f);
        var requestedFrames = System.Environment.GetEnvironmentVariable("URMAN_CAPTURE_FRAMES");
        var captureFrames = allFrames.Where(frame => string.IsNullOrEmpty(requestedFrames)
            ? frame.DiscoverySlug is null : requestedFrames.Split(',').Contains(frame.Id)).ToList();
        var buildingBounds = FindDescendants(core).OfType<MeshInstance3D>().Where(mesh => mesh.IsVisibleInTree()
            && mesh.Mesh is not null && new[] { "Roof", "Wall", "Facade" }.Any(token => mesh.Name.ToString().Contains(token, StringComparison.OrdinalIgnoreCase)))
            .Select(mesh => mesh.GlobalTransform * mesh.Mesh.GetAabb()).ToArray();
        var plantedBounds = FindDescendants(foliage).OfType<Node3D>().Where(node => node.HasMeta("plantPosition"))
            .Select(node => (Owner: node, Bounds: node.GlobalTransform * node.GetChild<MeshInstance3D>(0).Mesh.GetAabb())).ToArray();
        if (System.Environment.GetEnvironmentVariable("URMAN_CAPTURE_FOLIAGE_REFERENCES") == "1")
        foreach (var variant in new[] { "WinterBirch_1", "WinterLinden_1", "WinterBirdCherry_1" })
        {
            var candidates = FindDescendants(foliage).OfType<Node3D>()
                .Where(node => node.HasMeta("plantPosition") && node.GetMeta("plantVariant").AsString() == variant && node.GlobalPosition.Z > -40)
                .OrderBy(node => Math.Abs(node.GlobalPosition.X) + Math.Abs(node.GlobalPosition.Z));
            var found = false;
            foreach (var tree in candidates)
            {
                var near = tree.GetChild<MeshInstance3D>(0);
                var height = near.Mesh.GetAabb().Size.Y * tree.Scale.Y;
                var root = tree.GlobalPosition;
                foreach (var angle in new[] { 0f, Mathf.Pi * .5f, -Mathf.Pi * .5f, Mathf.Pi })
                {
                    var side = new Vector3(-Mathf.Sign(root.X), 0, .18f).Rotated(Vector3.Up, angle).Normalized();
                    var eye = root + side * Mathf.Max(2.2f, height) + Vector3.Up * height * .44f;
                    if (plantedBounds.Any(other => other.Owner != tree && other.Bounds.HasPoint(eye))) continue;
                    if (buildingBounds.Any(bounds => bounds.HasPoint(eye) || new[] { .1f, .48f, .9f }
                        .Any(level => bounds.IntersectsSegment(eye, root + Vector3.Up * height * level)))) continue;
                    if (new[] { .1f, .48f, .9f }.Any(level => core.GetWorld3D().DirectSpaceState.IntersectRay(
                        PhysicsRayQueryParameters3D.Create(eye, root + Vector3.Up * height * level, 1)).Count > 0)) continue;
                    captureFrames.Add(Frame($"foliage_{variant}", "main_street", "village_day", "arrival",
                        eye - Vector3.Up * 1.7f, root + Vector3.Up * height * .48f, "reference", "actual planted tree"));
                    GD.Print($"winter-foliage-reference: {variant} root={root} height={height} eye={eye}");
                    found = true; break;
                }
                if (found) break;
            }
            if (!found) throw new InvalidOperationException($"No unobstructed production foliage reference for {variant}");
        }
        if (System.Environment.GetEnvironmentVariable("URMAN_CAPTURE_SMOKE_REFERENCES") == "1")
        foreach (var smoke in core.GetNode<Node3D>("VillageLife").GetChildren().OfType<CpuParticles3D>())
        {
            Vector3? reference = null;
            foreach (var distance in new[] { 12f, 8f, 18f })
            {
                for (var angle = 0; angle < 12; angle++)
                {
                    var direction = new Vector3(Mathf.Cos(angle * Mathf.Tau / 12f), 0f, Mathf.Sin(angle * Mathf.Tau / 12f));
                    var point = smoke.GlobalPosition + direction * distance;
                    point.Y = AgentBAct1HeightField.CollisionGround(point.X, point.Z) + .05f;
                    var eye = point + Vector3.Up * 1.7f;
                    if (buildingBounds.Any(bounds => bounds.HasPoint(eye)
                        || bounds.IntersectsSegment(eye, smoke.GlobalPosition + Vector3.Up * .2f))) continue;
                    reference = point; break;
                }
                if (reference is not null) break;
            }
            if (reference is null) throw new InvalidOperationException($"No unobstructed chimney reference: {smoke.Name}");
            captureFrames.Add(Frame(smoke.Name + "_detail", "arrival", "village_day", "arrival",
                reference.Value, smoke.GlobalPosition + Vector3.Up * 1.4f,
                "reference", "player-height diagnostic of actual chimney top and plume"));
        }
        foreach (var spec in captureFrames)
        {
            main.SwitchZone(spec.LogicalZoneId, spec.SpawnPointId);
            await WaitForFramesAsync(SettleFrames);
            player.ApplyZoneSpawn(spec.PlayerPosition + shift, 0f);
            if (spec.DiscoverySlug is not null)
            {
                var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
                if (bridge is null || !await bridge.DispatchInteractionAsync("urman.chapter1:interaction/discover-" + spec.DiscoverySlug))
                    throw new InvalidOperationException("Discovery capture action failed: " + spec.DiscoverySlug);
                await ToSignal(GetTree().CreateTimer(.8), SceneTreeTimer.SignalName.Timeout);
            }
            camera.Current = true;
            camera.LookAt(spec.Target + shift, Vector3.Up);
            await WaitForFramesAsync(SettleFrames);
            await WaitForRenderedFrameAsync();

            if (spec.Id.StartsWith("ChimneySmoke", StringComparison.Ordinal))
            {
                var smoke = core.GetNode<Node3D>("VillageLife").GetNode<CpuParticles3D>(spec.Id.Replace("_detail", string.Empty));
                var sourceChimney = GetNode<MeshInstance3D>(smoke.GetMeta("chimneyOwner").AsString());
                GD.Print($"smoke-state: {smoke.Name} emitting={smoke.Emitting} speed={smoke.SpeedScale} bounds={smoke.CaptureAabb()} "
                    + $"emitter={smoke.GlobalPosition} actual_chimney_bounds={sourceChimney.GlobalTransform * sourceChimney.Mesh.GetAabb()} screen={camera.UnprojectPosition(smoke.GlobalPosition)}");
            }
            if (viewport.GetCamera3D() != camera)
            {
                throw new InvalidOperationException($"Production camera is not rendering the root viewport for {spec.Id}.");
            }

            // CAPTURE-002 contract: the requested pose must still be the
            // rendered pose at readback time, and diagnostics must expose the
            // actual camera state per frame so duplicate-hash defects have a
            // causal receipt instead of a silent retry.
            var actualForward = -camera.GlobalTransform.Basis.Z;
            var expectedForward = (spec.Target + shift - camera.GlobalPosition).Normalized();
            var aimDeviationDegrees = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(actualForward.Dot(expectedForward), -1f, 1f)));
            if (aimDeviationDegrees > 0.5f)
            {
                throw new InvalidOperationException(
                    $"Camera forward drifted {aimDeviationDegrees:F3} degrees from the requested target for {spec.Id} at readback time.");
            }

            var image = viewport.GetTexture().GetImage();
            if (image is null || image.IsEmpty())
            {
                throw new InvalidOperationException($"Root viewport returned an empty image for {spec.Id}.");
            }

            if (image.GetWidth() != CaptureWidth || image.GetHeight() != CaptureHeight)
            {
                throw new InvalidOperationException($"{spec.Id} rendered {image.GetWidth()}x{image.GetHeight()}, expected {CaptureWidth}x{CaptureHeight}.");
            }

            image.Convert(Image.Format.Rgba8);
            var fileName = $"{spec.Id}.png";
            var outputPath = Path.Combine(outputDirectory, fileName);
            if (File.Exists(outputPath))
            {
                throw new IOException($"Refusing to overwrite existing capture {outputPath}.");
            }

            if (image.SavePng(outputPath) != Error.Ok)
            {
                throw new IOException($"Could not save capture {outputPath}.");
            }

            var sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(outputPath))).ToLowerInvariant();
            var previousSha = captures.Count > 0 ? captures[^1].Sha256 : null;
            GD.Print(
                $"act1-core-capture frame={captures.Count + 1}/{allFrames.Count} id={spec.Id} active_zone={connectedWorld.ActiveZoneId} "
                + $"drawn_frame={Engine.GetFramesDrawn()} cam_pos=({camera.GlobalPosition.X:F2},{camera.GlobalPosition.Y:F2},{camera.GlobalPosition.Z:F2}) "
                + $"cam_fwd=({actualForward.X:F3},{actualForward.Y:F3},{actualForward.Z:F3}) aim_deviation_deg={aimDeviationDegrees:F3} "
                + $"sha256={sha256[..12]} same_as_prev={(previousSha is not null && previousSha == sha256).ToString().ToLowerInvariant()}");

            var environments = FindDescendants(main).OfType<WorldEnvironment>()
                .Where(node => node.Environment is not null).ToArray();
            if (environments.Length != 1)
                throw new InvalidOperationException($"Capture has {environments.Length} active environments, expected one.");
            var environment = environments[0].Environment;
            var sun = FindDescendants(main).OfType<DirectionalLight3D>().FirstOrDefault(light => light.IsVisibleInTree());
            captures.Add(new FrameReceipt
            {
                FrameId = spec.Id,
                Atmosphere = new Dictionary<string, object>
                {
                    ["owner"] = environments[0].GetPath().ToString(),
                    ["ambient_energy"] = environment.AmbientLightEnergy,
                    ["ambient_color"] = environment.AmbientLightColor.ToHtml(),
                    ["fog_density"] = environment.FogDensity,
                    ["fog_height_density"] = environment.FogHeightDensity,
                    ["ssao_radius"] = environment.SsaoRadius,
                    ["ssao_intensity"] = environment.SsaoIntensity,
                    ["glow"] = environment.GlowEnabled,
                    ["adjustments"] = environment.AdjustmentEnabled,
                    ["exposure"] = environment.TonemapExposure,
                    ["sun_energy"] = sun?.LightEnergy ?? 0f,
                    ["sun_rotation"] = ScalarVector.From(sun?.GlobalRotationDegrees ?? Vector3.Zero)
                },
                VisualZone = spec.VisualZone,
                LogicalZone = spec.LogicalZoneId,                ActiveZoneId = connectedWorld.ActiveZoneId,
                SpawnPointId = spec.SpawnPointId,
                Direction = spec.Direction,
                EvidenceKind = spec.EvidenceKind,
                Camera = new CameraReceipt
                {
                    GlobalPosition = ScalarVector.From(camera.GlobalPosition),
                    Target = ScalarVector.From(spec.Target + shift),
                    Fov = camera.Fov
                },
                OutputFile = fileName,
                OutputPath = Path.GetFullPath(outputPath),
                Width = image.GetWidth(),
                Height = image.GetHeight(),
                Sha256 = sha256
            });
        }

        if (System.Environment.GetEnvironmentVariable("URMAN_CAPTURE_SNOW_SWEEP") == "1")
            await CaptureSnowSweepAsync(main, player, camera, outputDirectory);
        if (System.Environment.GetEnvironmentVariable("URMAN_CAPTURE_BENCHMARK") == "1")
            await CapturePerformanceAsync(this, main, player, camera, outputDirectory);

        if (System.Environment.GetEnvironmentVariable("URMAN_CAPTURE_VILLAGE_LIFE") == "1")
            await CaptureVillageLifeAsync(main, player, camera, outputDirectory);

        var traversal = BuildTraversalReceipt();
        WriteReceipt(outputDirectory, presentationAudit, captures, traversal);
        GD.Print($"act1-full-route-core-world-capture: PASS mode=presentation-waypoint-audit (world-space straight-line distances between audit waypoints; not gameplay walk completion) frames={captures.Count} zones={presentationAudit.VisualZoneCount} traversal_waypoints={traversal.WaypointCount} output={outputDirectory}");

        // Release the production hierarchy synchronously before Godot exits.
        // A queued free leaves the many authored collision shapes alive until
        // after the process terminates, which makes the evidence wrapper
        // report a false-positive RID leak.
        main.Free();
        await WaitForFramesAsync(2);
        // The wrapper creates thousands of managed Shape3D wrappers; finalize
        // them before the capture process exits so renderer RIDs are released.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await WaitForFramesAsync(1);
        GetTree().Quit(0);
    }

    private async Task CaptureVillageLifeAsync(Main main, FirstPersonController player, Camera3D camera, string output)
    {
        main.SwitchZone("village_day", "arrival");
        await WaitForFramesAsync(180);
        var world = main.ConnectedWorld!;
        var life = world.GetNode<Node3D>("Act1CoreWorldGreybox/VillageLife");
        var cat = life.GetNode<Node3D>("YardCat");
        var resident = life.GetNode<Node3D>("ResidentAtFirewood");
        var smoke = life.GetChildren().OfType<CpuParticles3D>().ToArray();
        if (smoke.Length != 2 || FindDescendants(life).Any(node => node is CollisionObject3D or WorldEnvironment))
            throw new InvalidOperationException("Village life must have two chimneys and no physics / environment owner.");
        world.SetProcess(false);
        var accessibility = player.Accessibility;
        player.ApplyAccessibilitySettings(accessibility with { ReducedMotion = false });
        var rows = new List<object>();
        var seen = new HashSet<int>();
        var frameDirectory = Path.Combine(output, "cat_walk_frames");
        Directory.CreateDirectory(frameDirectory);
        var videoFrame = 0;
        var eventStarts = new List<object>();
        var previousEvent = life.GetMeta("event").AsInt32();
        var recording = false;
        var elapsed = 0f;
        for (var step = 0; step < 30 * 900 && (seen.Count < 3 || previousEvent != 0); step++)
        {
            // Advance the existing presentation owner at the delivery video's
            // fixed timestep; never force an event, pose or RNG result.
            var updateStart = Time.GetTicksUsec();
            world._Process(1.0 / 30.0);
            var updateMs = (Time.GetTicksUsec() - updateStart) / 1000.0;
            elapsed += 1f / 30f;
            var current = life.GetMeta("event").AsInt32();
            var eventTime = life.GetMeta("eventTime", 0f).AsSingle();
            if (current != 0 && current != previousEvent)
            {
                recording = !seen.Contains(current);
                if (recording)
                {
                    var target = current == 1 ? new Vector3(-27.6f, AgentBAct1HeightField.CollisionGround(-27.6f, 6.4f) + .25f, 6.4f)
                        : current == 2 ? new Vector3(0f, 17f, -5f) : resident.GlobalPosition + Vector3.Up;
                    var eye = current == 1 ? new Vector3(-27.6f, AgentBAct1HeightField.CollisionGround(-27.6f, 9.6f) + 1.7f, 9.6f)
                        : current == 2 ? new Vector3(0f, 1.7f, 18f) : resident.GlobalPosition + new Vector3(-8f, 1.7f, 7f);
                    if (current == 3)
                    {
                        var architecture = FindDescendants(world).OfType<MeshInstance3D>()
                            .Where(mesh => mesh.Mesh is not null && mesh.IsVisibleInTree()
                                && new[] { "Wall", "Facade", "Roof" }.Any(token => mesh.Name.ToString().Contains(token, StringComparison.Ordinal)))
                            .Select(mesh => mesh.GlobalTransform * mesh.Mesh.GetAabb()).ToArray();
                        var candidates = new[] { new Vector3(-8f, 0f, 7f), new Vector3(8f, 0f, 7f),
                            new Vector3(-8f, 0f, -7f), new Vector3(8f, 0f, -7f), new Vector3(0f, 0f, 8f) }
                            .Select(offset => resident.GlobalPosition + offset + Vector3.Up * 1.7f).ToArray();
                        eye = candidates.FirstOrDefault(candidate => !architecture.Any(bounds => bounds.HasPoint(candidate)
                            || bounds.IntersectsSegment(candidate, target)), Vector3.Zero);
                        if (eye == Vector3.Zero) throw new InvalidOperationException("No visible resident reference from the yard.");
                    }
                    player.ApplyZoneSpawn(eye - Vector3.Up * 1.7f, 0f);
                    camera.LookAt(target);
                    var referenceStart = Time.GetTicksUsec();
                    await WaitForRenderedFrameAsync();
                    // Includes the reference view change / render scheduling,
                    // excludes PNG readback and encoding; not steady-state FPS.
                    eventStarts.Add(new { kind = current, update_cpu_ms = updateMs,
                        first_visible_reference_ms = (Time.GetTicksUsec() - referenceStart) / 1000.0 });
                    using var before = GetViewport().GetTexture().GetImage();
                    before.SavePng(Path.Combine(output, $"life_{current}_before.png"));
                    rows.Add(new { kind = current, at_seconds = elapsed, eye = ScalarVector.From(camera.GlobalPosition),
                        target = ScalarVector.From(target), fov = camera.Fov });
                }
            }
            if (recording && current == 1)
            {
                await WaitForRenderedFrameAsync();
                using var image = GetViewport().GetTexture().GetImage();
                image.SavePng(Path.Combine(frameDirectory, $"frame_{videoFrame++:D3}.png"));
            }
            if (recording && current != 0 && !seen.Contains(current)
                && eventTime >= (current == 1 ? 2.5f : current == 2 ? 6f : 4.5f))
            {
                await WaitForRenderedFrameAsync();
                using var image = GetViewport().GetTexture().GetImage();
                image.SavePng(Path.Combine(output, $"life_{current}_during.png"));
                seen.Add(current);
            }
            if (current == 0 && previousEvent != 0)
            {
                var wait = life.GetMeta("nextWait").AsSingle();
                if (wait < 45f || wait > 120f) throw new InvalidOperationException("Ambient event gap out of bounds.");
                rows.Add(new { finished = previousEvent, at_seconds = elapsed, wait_seconds = wait });
                recording = false;
            }
            if (Mathf.Abs(cat.GlobalPosition.Y - AgentBAct1HeightField.CollisionGround(cat.GlobalPosition.X, cat.GlobalPosition.Z) + .01f) > .001f)
                throw new InvalidOperationException("Cat ground anchor diverged from physical terrain.");
            previousEvent = current;
        }
        if (seen.Count != 3) throw new InvalidOperationException("Did not observe all three ambient events.");
        foreach (var zone in new[] { "house_old_pc", "zirat_road", "kara_urman_night" })
        {
            main.SwitchZone(zone, zone == "house_old_pc" ? "entry" : zone == "zirat_road" ? "village_side" : "village_path");
            world._Process(1.0 / 30.0);
            if (life.Visible || life.GetMeta("motionAllowed").AsBool()) throw new InvalidOperationException($"Life leaked into {zone}");
        }
        main.SwitchZone("village_day", "arrival");
        player.ApplyAccessibilitySettings(accessibility with { ReducedMotion = true });
        world._Process(1.0 / 30.0);
        if (life.GetMeta("motionAllowed").AsBool() || smoke.Any(particle => particle.SpeedScale != 0f))
            throw new InvalidOperationException("Reduced motion did not stop village life.");
        player.ApplyAccessibilitySettings(accessibility with { ReducedMotion = false });
        player.SetModalOpen(true);
        world._Process(1.0 / 30.0);
        if (life.GetMeta("motionAllowed").AsBool()) throw new InvalidOperationException("Village life continued under modal.");
        player.SetModalOpen(false);
        player.ApplyAccessibilitySettings(accessibility);
        world.SetProcess(true);
        File.WriteAllText(Path.Combine(output, "village_life_receipt.json"), JsonSerializer.Serialize(new
        {
            mode = "existing presentation owner advanced at 30Hz; natural randomized events; no save writes",
            event_start_cost = eventStarts,
            simulated_seconds = elapsed, observed_kinds = seen.Order().ToArray(), cat_video_frames = videoFrame,
            checks = "two chimney tops; zero collision/environment owners; cat ground contact; gaps 45-120s; quiet zones; reduced motion; modal",
            resident_meshes = FindDescendants(resident).OfType<MeshInstance3D>().Where(mesh => mesh.IsVisibleInTree())
                .Select(mesh => new { name = mesh.Name.ToString(), center = ScalarVector.From((mesh.GlobalTransform * mesh.Mesh.GetAabb()).GetCenter()) }),
            chimneys = smoke.Select(particle => new { owner = particle.GetMeta("chimneyOwner").AsString(),
                position = ScalarVector.From(particle.GlobalPosition), amount = particle.Amount }), events = rows
        }, ReceiptJsonOptions));
        GD.Print($"village-life: PASS observed={seen.Count} simulated_seconds={elapsed:F2} cat_video_frames={videoFrame}");
    }

    private static PresentationAudit AuditPresentationOnlyLayer(Node3D core)
    {
        if (!core.HasMeta("presentationOnly") || !core.GetMeta("presentationOnly").AsBool())
        {
            throw new InvalidOperationException("Act1CoreWorldGreybox is not tagged presentationOnly=true.");
        }

        var expectedZones = new[]
        {
            "Arrival",
            "MainStreet",
            "BabaiEbiYard",
            "HouseExteriorApproach",
            "ConnectiveStreetReturn",
            "FapExterior",
            "ZiratMemoryField",
            "KaraForestEdge"
        };
        var directZones = core.GetChildren().OfType<Node3D>().Select(node => node.Name.ToString()).ToHashSet(StringComparer.Ordinal);
        if (!expectedZones.All(directZones.Contains))
        {
            throw new InvalidOperationException($"Act1CoreWorldGreybox direct visual zones are incomplete: expected={string.Join('|', expectedZones)} actual={string.Join('|', directZones)}.");
        }

        var exteriorLayer = core.GetNodeOrNull<Node3D>("AgentBExteriorWorld")
            ?? throw new InvalidOperationException("Act1CoreWorldGreybox is missing AgentBExteriorWorld traversal owner.");
        var expectedCollisionOwners = new HashSet<string>(StringComparer.Ordinal)
        {
            "act1-exterior-terrain",
            "act1-exterior-architecture",
            // Presentation blockers for the village mosque complex and the river band
            // (layer-2, mask-0).
            "mosque-blocker",
            "river-blocker",
            // Presentation blockers for authored kit walls, fences and outbuildings.
            // They are non-interactive layer-2 proxies whose only job is to stop the
            // player walking through visible geometry; the author reported that
            // fences and houses had no collision at all. Their contract is verified
            // right below, so this is a declared owner rather than an exemption.
            "authored-kit-blocker",
            // EX01 carryables: physical props the player can pick up, so their
            // bodies are traversal furniture with a declared owner of their own.
            "carryable-prop"
        };
        var declaredOwners = exteriorLayer.GetMeta("traversalCollisionOwners").AsString();
        if (!string.Equals(
                declaredOwners,
                "AgentB_TerrainCollision:act1-exterior-terrain|AgentB_ArchitectureCollision:act1-exterior-architecture",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "AgentBExteriorWorld traversal collision owner metadata drifted from the capture contract.");
        }

        var forbidden = FindDescendants(core)
            .Where(node => node is CollisionObject3D
                or CollisionShape3D
                or NavigationRegion3D
                or NavigationLink3D
                or NavigationObstacle3D
                or InteractionTarget)
            .Where(node => !IsDeclaredTraversalCollision(node, exteriorLayer, expectedCollisionOwners))
            .ToArray();
        if (forbidden.Length > 0)
        {
            throw new InvalidOperationException($"Act1CoreWorldGreybox contains forbidden gameplay nodes outside the declared traversal owner: {string.Join('|', forbidden.Select(node => $"{node.GetType().Name}@{node.GetPath()}"))}.");
        }

        var kitBlockers = FindDescendants(core).OfType<StaticBody3D>()
            .Where(body => body.HasMeta("collisionOwner")
                && body.GetMeta("collisionOwner").AsString() is "authored-kit-blocker" or "mosque-blocker" or "river-blocker")
            .ToArray();
        var kitBlockerShapes = 0;
        foreach (var blocker in kitBlockers)
        {
            if (blocker.CollisionLayer != 2u || blocker.CollisionMask != 0u)
            {
                throw new InvalidOperationException(
                    $"Authored kit blocker must stay a non-interactive layer-2 proxy: {blocker.GetPath()} "
                    + $"layer={blocker.CollisionLayer} mask={blocker.CollisionMask}.");
            }

            var placement = blocker.GetParentOrNull<Node3D>();
            var declared = placement is not null
                && ((placement.HasMeta("authoredKitBlockerCount") && placement.GetMeta("authoredKitBlockerCount").AsInt32() > 0)
                    || (placement.HasMeta("mosqueBlockerShapeCount") && placement.GetMeta("mosqueBlockerShapeCount").AsInt32() > 0)
                    || (placement.HasMeta("riverBlockerShapeCount") && placement.GetMeta("riverBlockerShapeCount").AsInt32() > 0));
            if (!declared)
            {
                throw new InvalidOperationException(
                    $"Authored kit blocker is not declared by its placement: {blocker.GetPath()}.");
            }

            kitBlockerShapes += blocker.GetChildCount();
        }

        GD.Print(
            $"act1-kit-blockers: verified_placements={kitBlockers.Length} verified_shapes={kitBlockerShapes} "
            + "layer=2 mask=0 declared_by_placement=true");

        var visualMeshCount = FindDescendants(core).Count(node => node is MeshInstance3D);
        if (visualMeshCount <= 0)
        {
            throw new InvalidOperationException("Act1CoreWorldGreybox has no presentation meshes.");
        }

        return new PresentationAudit
        {
            Name = core.Name.ToString(),
            PresentationOnly = true,
            VisualZoneCount = expectedZones.Length,
            VisualMeshCount = visualMeshCount,
            ForbiddenGameplayNodeCount = forbidden.Length
        };
    }

    private static bool IsDeclaredTraversalCollision(
        Node node,
        Node3D exteriorLayer,
        ISet<string> expectedCollisionOwners)
    {
        if (node is not CollisionObject3D and not CollisionShape3D)
        {
            return false;
        }

        var current = node;
        while (current is not null && current != exteriorLayer)
        {
            if (current.HasMeta("collisionOwner"))
            {
                var owner = current.GetMeta("collisionOwner").AsString();
                return expectedCollisionOwners.Contains(owner);
            }

            current = current.GetParent();
        }

        return false;
    }

    private static TraversalReceipt BuildTraversalReceipt()
    {
        var rows = new List<WaypointReceipt>(Waypoints.Count);
        var cumulative = 0f;
        for (var index = 0; index < Waypoints.Count; index++)
        {
            var waypoint = Waypoints[index];
            if (index > 0)
            {
                cumulative += HorizontalDistance(Waypoints[index - 1].Position, waypoint.Position);
            }

            rows.Add(new WaypointReceipt
            {
                Id = waypoint.Id,
                Position = ScalarVector.From(waypoint.Position),
                SegmentMeters = index == 0 ? 0f : HorizontalDistance(Waypoints[index - 1].Position, waypoint.Position),
                CumulativeMeters = cumulative,
                Reached = true
            });
        }

        return new TraversalReceipt
        {
            RouteId = "act1_arrival_to_kara_cliffhanger",
            Mode = "world-space presentation waypoint audit; no narrative transition or save mutation",
            StartWaypoint = rows[0].Id,
            EndpointWaypoint = rows[^1].Id,
            WaypointCount = rows.Count,
            TotalHorizontalMeters = cumulative,
            Waypoints = rows
        };
    }

    private static IEnumerable<Node> FindDescendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var nested in FindDescendants(child))
            {
                yield return nested;
            }
        }
    }

    // Static exterior camera anchors use the same surface as the player.
    // Indoor and already-authored absolute reference poses stay unchanged.
    private static Vector3 GroundedPosition(float x, float z) =>
        new(x, AgentBAct1HeightField.CollisionGround(x, z) + .05f, z);

    private static FrameSpec Frame(
        string id,
        string visualZone,
        string logicalZoneId,
        string spawnPointId,
        Vector3 playerPosition,
        Vector3 target,
        string direction,
        string evidenceKind, string? discoverySlug = null) =>
        new(id, visualZone, logicalZoneId, spawnPointId, playerPosition, target, direction, evidenceKind, discoverySlug);

    /// <summary>
    /// §12.7 perimeter sweep: standing positions along the settlement envelope,
    /// each looking outward across the ring on three headings plus straight up.
    /// Day frames cover all four edges; the south edge repeats in the night
    /// light state, because that is the Kara direction. This is a harness sweep
    /// on a stated 10 m grid — coarser than the plan's 5 m starting grid and
    /// visibly narrower than a human free-camera walk — so it proves whether an
    /// outward view reaches the terrain seam or open sky between the crowns at
    /// these positions, and nothing more.
    /// </summary>
    private static IReadOnlyList<FrameSpec> BuildPerimeterFrames()
    {
        var frames = new List<FrameSpec>();
        const float step = 10f;
        const float standOff = 1.7f;
        var innerMin = AgentBAct1ExteriorLayer.ForestRingInnerMin;
        var innerMax = AgentBAct1ExteriorLayer.ForestRingInnerMax;

        void Sweep(Vector2 from, Vector2 to, Vector2 outward, string visualZone,
            string logicalZone, string spawn, string lightLabel)
        {
            var length = from.DistanceTo(to);
            var along = (to - from) / length;
            var count = Math.Max(1, (int)MathF.Round(length / step));
            for (var index = 0; index <= count; index++)
            {
                var point = from + along * (length * index / count);
                var stand = point - outward * standOff;
                var eye = GroundedPosition(stand.X, stand.Y);
                var far = point + outward * 26f;
                foreach (var (heading, offset, height) in new (string, float, float)[]
                         {
                             ("out-left", -26f, 1.5f),
                             ("out", 0f, 1.5f),
                             ("out-right", 26f, 1.5f),
                             ("up", 5f, 15f)
                         })
                {
                    var lateral = new Vector2(-outward.Y, outward.X) * offset;
                    var target = new Vector3(far.X + lateral.X, eye.Y + height, far.Y + lateral.Y);
                    frames.Add(Frame(
                        $"perimeter_{lightLabel}_{visualZone}_{index}_{heading}",
                        visualZone, logicalZone, spawn, eye, target,
                        "perimeter", $"forest-ring outward {heading}"));
                }
            }
        }

        // Edges walk clockwise from the envelope's south-west corner so the
        // outward normal always points away from the village.
        Sweep(new(innerMin.X, innerMin.Y), new(innerMin.X, innerMax.Y), new(-1f, 0f), "zirat", "village_day", "arrival", "day");
        Sweep(new(innerMin.X, innerMax.Y), new(innerMax.X, innerMax.Y), new(0f, 1f), "arrival", "village_day", "arrival", "day");
        Sweep(new(innerMax.X, innerMax.Y), new(innerMax.X, innerMin.Y), new(1f, 0f), "fap_exterior", "village_day", "arrival", "day");
        Sweep(new(innerMax.X, innerMin.Y), new(innerMin.X, innerMin.Y), new(0f, -1f), "kara_approach", "village_day", "arrival", "day");
        Sweep(new(innerMax.X, innerMin.Y), new(innerMin.X, innerMin.Y), new(0f, -1f), "kara_approach", "kara_urman_night", "village_path", "night");
        return frames;
    }

    private static readonly IReadOnlyList<FrameSpec> PerimeterFrames = BuildPerimeterFrames();

    private static string RequireArgument(string prefix)
    {
        var argument = OS.GetCmdlineArgs().FirstOrDefault(value => value.StartsWith(prefix, StringComparison.Ordinal));
        if (argument is null || argument.Length == prefix.Length)
        {
            throw new InvalidOperationException($"Missing required command-line argument '{prefix}<value>'.");
        }

        return argument[prefix.Length..];
    }

    internal static async Task CapturePerformanceAsync(Node host, Main main, FirstPersonController player, Camera3D camera, string outputDirectory)
    {
        var viewport = host.GetViewport();
        player.SetPhysicsProcess(false);
        camera.MakeCurrent();
        main.SwitchZone("village_day", "arrival");
        player.ApplyZoneSpawn(new Vector3(-2.2f, .05f, 2.4f), 0f);
        camera.LookAt(new Vector3(0f, 1.45f, -15f), Vector3.Up);
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        var warmup = Time.GetTicksMsec();
        while (Time.GetTicksMsec() - warmup < 12000)
        {
            await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
            await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw(false);
        }
        var samples = new List<double>();
        var start = Time.GetTicksMsec();
        while (Time.GetTicksMsec() - start < 60000)
        {
            var tick = Time.GetTicksUsec();
            await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw(false);
            samples.Add((Time.GetTicksUsec() - tick) / 1000.0);
        }
        var sorted = samples.OrderBy(value => value).ToArray();
        var measurement = new
        {
            renderer = RenderingServer.GetVideoAdapterName(), preset = player.GraphicsPreset,
            width = viewport.GetVisibleRect().Size.X, height = viewport.GetVisibleRect().Size.Y, fov = camera.Fov,
            render_scale = viewport.Scaling3DScale, msaa = viewport.Msaa3D.ToString(),
            warmup_seconds = 12, sample_seconds = 60, sample_count = samples.Count,
            average_ms = samples.Average(), p95_ms = sorted[(int)((sorted.Length - 1) * .95)],
            max_ms = sorted[^1], fps = 1000.0 / samples.Average(),
            nodes = FindDescendants(main).Count(),
            mesh_instances = FindDescendants(main).OfType<MeshInstance3D>().Count(),
            draw_calls = Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),
            primitives = Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame),
            note = "Root viewport; vsync disabled; explicit hidden-window draw included in wall frame time."
        };
        File.WriteAllText(Path.Combine(outputDirectory, "performance.json"), JsonSerializer.Serialize(measurement, ReceiptJsonOptions));
        GD.Print("winter-performance: " + JsonSerializer.Serialize(measurement));
    }

    private async Task CaptureSnowSweepAsync(Main main, FirstPersonController player, Camera3D camera, string outputDirectory)
    {
        main.SwitchZone("village_day", "arrival");
        player.ApplyZoneSpawn(new Vector3(-2.2f, .05f, 2.4f), 0f);
        camera.LookAt(new Vector3(-1.2f, -.1f, -7f), Vector3.Up);
        // Material isolation: moving weather/branches are not shimmering.
        foreach (var particles in FindDescendants(main).OfType<CpuParticles3D>()) particles.Visible = false;
        PainterlyMaterialLibrary.SetWindMotion(false);
        for (var settle = 0; settle < 8; settle++) await WaitForRenderedFrameAsync();
        var directory = Path.Combine(outputDirectory, "snow_sweep_frames");
        Directory.CreateDirectory(directory);
        var view = camera.GlobalTransform;
        byte[]? stationary = null;
        const int frameCount = 90;
        for (var frame = 0; frame < frameCount; frame++)
        {
            var yaw = Mathf.Max(0, frame - 29) * .0015f;
            camera.GlobalTransform = new Transform3D(new Basis(Vector3.Up, yaw) * view.Basis, view.Origin);
            await WaitForRenderedFrameAsync();
            using var shot = GetViewport().GetTexture().GetImage();
            if (frame < 30)
            {
                using var region = shot.GetRegion(new Rect2I(CaptureWidth * 3 / 10, CaptureHeight * 3 / 4, CaptureWidth * 4 / 10, CaptureHeight / 5));
                var pixels = region.GetData();
                if (stationary is not null && !pixels.SequenceEqual(stationary))
                    throw new InvalidOperationException($"Stationary snow ROI changes at frame {frame} with wind/weather hidden.");
                stationary = pixels;
            }
            var path = Path.Combine(directory, $"snow_{frame:D3}.png");
            if (File.Exists(path) || shot.SavePng(path) != Error.Ok)
                throw new IOException($"Cannot write fresh snow sweep frame: {path}");
        }
        File.WriteAllText(Path.Combine(outputDirectory, "snow_sweep_receipt.json"), JsonSerializer.Serialize(new
        {
            frames = frameCount, fps = 30, width = CaptureWidth, height = CaptureHeight,
            stationary_frames = 30, stationary_snow_roi_identical = true,
            yaw_degrees = Mathf.RadToDeg(60 * .0015f), fov = camera.Fov,
            note = "First second stationary, then slow yaw. Weather and wind hidden only for material isolation."
        }, new JsonSerializerOptions { WriteIndented = true }));
        GD.Print($"winter-snow-sweep: PASS stationary30 + slow yaw60 frames, {directory}");
    }

    private async Task WaitForFramesAsync(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task WaitForRenderedFrameAsync()
    {
        // Hidden macOS viewports can skip drawing while ProcessFrame keeps
        // advancing. Settle transforms, then explicitly draw the real viewport
        // on the main thread before readback; stale pixels are not evidence.
        await WaitForFramesAsync(2);
        RenderingServer.ForceDraw(false);
    }

    private static float HorizontalDistance(Vector3 left, Vector3 right) =>
        new Vector2(left.X - right.X, left.Z - right.Z).Length();

    private static void WriteReceipt(
        string outputDirectory,
        PresentationAudit presentationAudit,
        IReadOnlyList<FrameReceipt> captures,
        TraversalReceipt traversal)
    {
        var receiptPath = Path.Combine(outputDirectory, ReceiptFileName);
        var receipt = new Receipt
        {
            SchemaVersion = 1,
            Kind = "urman.godot_act1_full_route_core_world_capture",
            CapturedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            RendererExpectation = "Godot 4.7.1 .NET Forward+ / real Metal 3D rendering device",
            ProductionScene = "res://scenes/main.tscn",
            RootViewport = true,
            SubViewportUsed = false,
            CaptureProcessCount = 1,
            Viewport = new ViewportReceipt { Width = CaptureWidth, Height = CaptureHeight },
            CoreLayer = presentationAudit,
            Frames = captures.ToList(),
            Traversal = traversal
        };

        var temporaryPath = Path.Combine(Path.GetTempPath(), $"urman-act1-core-receipt-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(receipt, ReceiptJsonOptions) + global::System.Environment.NewLine);
            File.Move(temporaryPath, receiptPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }

    private sealed record FrameSpec(
        string Id,
        string VisualZone,
        string LogicalZoneId,
        string SpawnPointId,
        Vector3 PlayerPosition,
        Vector3 Target,
        string Direction,
        string EvidenceKind, string? DiscoverySlug = null);

    private sealed record WaypointSpec(string Id, Vector3 Position);

    private sealed class Receipt
    {
        [JsonPropertyName("schema_version")] public int SchemaVersion { get; set; }
        [JsonPropertyName("kind")] public string Kind { get; set; } = string.Empty;
        [JsonPropertyName("captured_at_utc")] public string CapturedAtUtc { get; set; } = string.Empty;
        [JsonPropertyName("renderer_expectation")] public string RendererExpectation { get; set; } = string.Empty;
        [JsonPropertyName("production_scene")] public string ProductionScene { get; set; } = string.Empty;
        [JsonPropertyName("root_viewport")] public bool RootViewport { get; set; }
        [JsonPropertyName("subviewport_used")] public bool SubViewportUsed { get; set; }
        [JsonPropertyName("capture_process_count")] public int CaptureProcessCount { get; set; }
        [JsonPropertyName("viewport")] public ViewportReceipt Viewport { get; set; } = new();
        [JsonPropertyName("core_layer")] public PresentationAudit CoreLayer { get; set; } = new();
        [JsonPropertyName("frames")] public List<FrameReceipt> Frames { get; set; } = [];
        [JsonPropertyName("frame_count")] public int FrameCount => Frames.Count;
        [JsonPropertyName("traversal")] public TraversalReceipt Traversal { get; set; } = new();
    }

    private sealed class ViewportReceipt
    {
        [JsonPropertyName("width")] public int Width { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }
    }

    private sealed class PresentationAudit
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("presentation_only")] public bool PresentationOnly { get; set; }
        [JsonPropertyName("visual_zone_count")] public int VisualZoneCount { get; set; }
        [JsonPropertyName("visual_mesh_count")] public int VisualMeshCount { get; set; }
        [JsonPropertyName("forbidden_gameplay_node_count")] public int ForbiddenGameplayNodeCount { get; set; }
    }

    private sealed class FrameReceipt
    {
        [JsonPropertyName("atmosphere")] public Dictionary<string, object> Atmosphere { get; set; } = new();
        [JsonPropertyName("frame_id")] public string FrameId { get; set; } = string.Empty;
        [JsonPropertyName("visual_zone")] public string VisualZone { get; set; } = string.Empty;
        [JsonPropertyName("logical_zone")] public string LogicalZone { get; set; } = string.Empty;
        [JsonPropertyName("active_zone_id")] public string ActiveZoneId { get; set; } = string.Empty;
        [JsonPropertyName("spawn_point_id")] public string SpawnPointId { get; set; } = string.Empty;
        [JsonPropertyName("direction")] public string Direction { get; set; } = string.Empty;
        [JsonPropertyName("evidence_kind")] public string EvidenceKind { get; set; } = string.Empty;
        [JsonPropertyName("camera")] public CameraReceipt Camera { get; set; } = new();
        [JsonPropertyName("output_file")] public string OutputFile { get; set; } = string.Empty;
        [JsonPropertyName("output_path")] public string OutputPath { get; set; } = string.Empty;
        [JsonPropertyName("width")] public int Width { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }
        [JsonPropertyName("sha256")] public string Sha256 { get; set; } = string.Empty;
    }

    private sealed class CameraReceipt
    {
        [JsonPropertyName("global_position")] public ScalarVector GlobalPosition { get; set; } = new();
        [JsonPropertyName("fov")] public float Fov { get; set; }
        [JsonPropertyName("target")] public ScalarVector Target { get; set; } = new();
    }

    private sealed class ScalarVector
    {
        [JsonPropertyName("x")] public float X { get; set; }
        [JsonPropertyName("y")] public float Y { get; set; }
        [JsonPropertyName("z")] public float Z { get; set; }

        public static ScalarVector From(Vector3 value) => new() { X = value.X, Y = value.Y, Z = value.Z };
    }

    private sealed class TraversalReceipt
    {
        [JsonPropertyName("route_id")] public string RouteId { get; set; } = string.Empty;
        [JsonPropertyName("mode")] public string Mode { get; set; } = string.Empty;
        [JsonPropertyName("start_waypoint")] public string StartWaypoint { get; set; } = string.Empty;
        [JsonPropertyName("endpoint_waypoint")] public string EndpointWaypoint { get; set; } = string.Empty;
        [JsonPropertyName("waypoint_count")] public int WaypointCount { get; set; }
        [JsonPropertyName("total_horizontal_meters")] public float TotalHorizontalMeters { get; set; }
        [JsonPropertyName("waypoints")] public List<WaypointReceipt> Waypoints { get; set; } = [];
    }

    private sealed class WaypointReceipt
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("position")] public ScalarVector Position { get; set; } = new();
        [JsonPropertyName("segment_meters")] public float SegmentMeters { get; set; }
        [JsonPropertyName("cumulative_meters")] public float CumulativeMeters { get; set; }
        [JsonPropertyName("reached")] public bool Reached { get; set; }
    }

}
