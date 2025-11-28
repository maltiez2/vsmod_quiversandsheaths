using Vintagestory.API.Common;

namespace QuiversAndSheaths;

public sealed class QuiversAndSheathsSystem : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        api.RegisterCollectibleBehaviorClass("QuiversAndSheaths:ShapeTexturesFromAttributes", typeof(ShapeTexturesFromAttributes));
        api.RegisterCollectibleBehaviorClass("QuiversAndSheaths:ShapeReplacement", typeof(ShapeReplacement));
        api.RegisterCollectibleBehaviorClass("QuiversAndSheaths:Sheath", typeof(SheathBehavior));
        api.RegisterCollectibleBehaviorClass("QuiversAndSheaths:Quiver", typeof(QuiverBehavior));
        api.RegisterCollectibleBehaviorClass("QuiversAndSheaths:VariantFromSlot", typeof(VariantFromSlotBehavior));
    }
}