using AttributeRenderingLibrary;
using CombatOverhaul.Armor;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace QuiversAndSheaths;

public class VariantFromSlotConfig
{
    public Dictionary<string, string> SlotsToVariants { get; set; } = [];
    public string TargetVariant { get; set; } = "";
}

public class VariantFromSlotBehavior : CollectibleBehavior, IGearSlotModifiedListener
{
    public VariantFromSlotBehavior(CollectibleObject collObj) : base(collObj)
    {
    }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);

        Config = properties.AsObject<VariantFromSlotConfig>();
    }

    public virtual void OnSlotModified(ItemSlot slot, ArmorInventory inventory, EntityPlayer player)
    {
        GearSlot? sheathSlot = slot as GearSlot;
        if (sheathSlot?.Itemstack == null) return;

        string slotType = sheathSlot.SlotType;
        string variantValue = Config.SlotsToVariants[slotType];
        Variants variants = Variants.FromStack(sheathSlot.Itemstack);

        if (variants.Get(Config.TargetVariant) == variantValue) return;

        variants.Set(Config.TargetVariant, variantValue);
        variants.ToStack(sheathSlot.Itemstack);
        sheathSlot.MarkDirty();
    }

    protected VariantFromSlotConfig Config = new();
}