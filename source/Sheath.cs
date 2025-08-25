using AttributeRenderingLibrary;
using CombatOverhaul.Armor;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.Common;
using Vintagestory.GameContent;

namespace QuiversAndSheaths;

public class SheathStats
{
    public string RightHandVariant { get; set; } = "right_slot";
    public string LeftHandVariant { get; set; } = "left_slot";
    public string RightHandStateVariant { get; set; } = "right_slot_state";
    public string LeftHandStateVariant { get; set; } = "left_slot_state";
    public string EmptyStateCode { get; set; } = "empty";
    public string FullStateCode { get; set; } = "full";
    public string RightWeaponMetalVariant { get; set; } = "right_metal";
    public string RightWeaponLeatherVariant { get; set; } = "right_leather";
    public string RightWeaponWoodVariant { get; set; } = "right_wood";
    public string LeftWeaponMetalVariant { get; set; } = "left_metal";
    public string LeftWeaponLeatherVariant { get; set; } = "left_leather";
    public string LeftWeaponWoodVariant { get; set; } = "left_wood";
}

public class SheathableStats
{
    public string InSheathVariantCode { get; set; } = "default";
    public string MetalVariantCode { get; set; } = "copper";
    public string LeatherVariantCode { get; set; } = "plain";
    public string WoodVariantCode { get; set; } = "oak";
}

public class SheathBehavior : ToolBag
{
    public SheathBehavior(CollectibleObject collObj) : base(collObj)
    {
    }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);

        Stats = properties.AsObject<SheathStats>();
    }

    public override List<ItemSlotBagContent?> GetOrCreateSlots(ItemStack bagstack, InventoryBase parentinv, int bagIndex, IWorldAccessor world)
    {
        if (parentinv is InventoryBasePlayer playerInventory && playerInventory.Player?.Entity != null && world.Api is ICoreServerAPI)
        {
            EntityPlayer player = playerInventory.Player.Entity;

            if (!ProcessedPlayers.Contains(player.EntityId))
            {
                playerInventory.SlotModified += slotIndex => OnSlotModified(playerInventory, player, slotIndex, bagIndex);
                ProcessedPlayers.Add(player.EntityId);
            }
        }

        return base.GetOrCreateSlots(bagstack, parentinv, bagIndex, world);
    }

    protected readonly List<long> ProcessedPlayers = [];
    protected SheathStats Stats = new();

    protected static InventoryBase? GetGearInventory(Entity entity)
    {
        return entity.GetBehavior<EntityBehaviorPlayerInventory>()?.Inventory;
    }
    protected InventoryPlayerBackPacks? GetBackpackInventory(EntityPlayer entity)
    {
        return entity.Player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName) as InventoryPlayerBackPacks;
    }

    protected virtual void OnSlotModified(InventoryBasePlayer backpackInventory, EntityPlayer player, int slotIndex, int bagIndex)
    {
        OnSlotModifiedOtherSlots(backpackInventory, player, slotIndex, bagIndex);

        InventoryBase? gearInventory = GetGearInventory(player);
        if (gearInventory == null) return;

        ItemSlot? sheathSlot = gearInventory
            .Where(slot => slot?.Itemstack?.Collectible?.Id == collObj.Id)
            .FirstOrDefault((ItemSlot?)null);
        if (sheathSlot?.Itemstack == null) return;

        ItemSlotToolHolder? weaponSlot = backpackInventory[slotIndex] as ItemSlotToolHolder;

        if (weaponSlot == null) return;

        if (weaponSlot.Empty)
        {
            string variantCode = weaponSlot.MainHand ? Stats.RightHandStateVariant : Stats.LeftHandStateVariant;
            Variants variants = Variants.FromStack(sheathSlot.Itemstack);
            if (variants.Get(variantCode) != Stats.EmptyStateCode)
            {
                variants.Set(variantCode, Stats.EmptyStateCode);
                variants.ToStack(sheathSlot.Itemstack);
                sheathSlot.MarkDirty();
            }
            return;
        }
        else
        {
            string variantCode = weaponSlot.MainHand ? Stats.RightHandStateVariant : Stats.LeftHandStateVariant;
            Variants variants = Variants.FromStack(sheathSlot.Itemstack);
            if (variants.Get(variantCode) != Stats.FullStateCode)
            {
                variants.Set(variantCode, Stats.FullStateCode);
                variants.ToStack(sheathSlot.Itemstack);
                sheathSlot.MarkDirty();
            }
        }

        if (weaponSlot.Itemstack?.Collectible?.Attributes != null)
        {
            SheathableStats stats = weaponSlot.Itemstack.Collectible.Attributes.AsObject<SheathableStats>();
            
            string variantCode = weaponSlot.MainHand ? Stats.RightHandVariant : Stats.LeftHandVariant;
            string metalVariantCode = weaponSlot.MainHand ? Stats.RightWeaponMetalVariant : Stats.LeftWeaponMetalVariant;
            string leatherVariantCode = weaponSlot.MainHand ? Stats.RightWeaponLeatherVariant : Stats.LeftWeaponLeatherVariant;
            string woodVariantCode = weaponSlot.MainHand ? Stats.RightWeaponWoodVariant : Stats.LeftWeaponWoodVariant;
            
            Variants variants = Variants.FromStack(sheathSlot.Itemstack);
            if (variants.Get(variantCode) != stats.InSheathVariantCode)
            {
                variants.Set(variantCode, stats.InSheathVariantCode);
                variants.ToStack(sheathSlot.Itemstack);
                sheathSlot.MarkDirty();
            }

            Variants weaponVariants = Variants.FromStack(weaponSlot.Itemstack);
            
            if (weaponVariants.Get(metalVariantCode) != null)
            {
                if (variants.Get(metalVariantCode) != weaponVariants.Get(metalVariantCode))
                {
                    variants.Set(metalVariantCode, weaponVariants.Get(metalVariantCode));
                    variants.ToStack(sheathSlot.Itemstack);
                    sheathSlot.MarkDirty();
                }
            }
            else
            {
                if (variants.Get(metalVariantCode) != stats.MetalVariantCode)
                {
                    variants.Set(metalVariantCode, stats.MetalVariantCode);
                    variants.ToStack(sheathSlot.Itemstack);
                    sheathSlot.MarkDirty();
                }
            }

            if (weaponVariants.Get(leatherVariantCode) != null)
            {
                if (variants.Get(leatherVariantCode) != weaponVariants.Get(leatherVariantCode))
                {
                    variants.Set(leatherVariantCode, weaponVariants.Get(leatherVariantCode));
                    variants.ToStack(sheathSlot.Itemstack);
                    sheathSlot.MarkDirty();
                }
            }
            else
            {
                if (variants.Get(leatherVariantCode) != stats.LeatherVariantCode)
                {
                    variants.Set(leatherVariantCode, stats.LeatherVariantCode);
                    variants.ToStack(sheathSlot.Itemstack);
                    sheathSlot.MarkDirty();
                }
            }

            if (weaponVariants.Get(woodVariantCode) != null)
            {
                if (variants.Get(woodVariantCode) != weaponVariants.Get(woodVariantCode))
                {
                    variants.Set(woodVariantCode, weaponVariants.Get(woodVariantCode));
                    variants.ToStack(sheathSlot.Itemstack);
                    sheathSlot.MarkDirty();
                }
            }
            else
            {
                if (variants.Get(woodVariantCode) != stats.WoodVariantCode)
                {
                    variants.Set(woodVariantCode, stats.WoodVariantCode);
                    variants.ToStack(sheathSlot.Itemstack);
                    sheathSlot.MarkDirty();
                }
            }
        }
    }

    protected virtual void OnSlotModifiedOtherSlots(InventoryBasePlayer backpackInventory, EntityPlayer player, int slotIndex, int bagIndex)
    {
        InventoryBase? gearInventory = GetGearInventory(player);
        if (gearInventory == null) return;

        ItemSlot? sheathSlot = gearInventory
            .Where(slot => slot?.Itemstack?.Collectible?.Id == collObj.Id)
            .FirstOrDefault((ItemSlot?)null);
        if (sheathSlot?.Itemstack == null) return;

        IEnumerable<string> variantCodes = backpackInventory
            .OfType<ItemSlotBagContentWithWildcardMatch>()
            .Where(slot => slot is not ItemSlotToolHolder)
            .Where(slot => slot.Config.SetVariants)
            .Select(slot => slot.Config.SlotVariant)
            .Distinct();

        foreach (string variantCode in variantCodes)
        {
            ItemSlotBagContentWithWildcardMatch quiverSlot = backpackInventory
                .OfType<ItemSlotBagContentWithWildcardMatch>()
                .First(slot => slot.Config.SlotVariant == variantCode);

            ItemSlotBagContentWithWildcardMatch? quiverNotEmptySlot = backpackInventory
                .OfType<ItemSlotBagContentWithWildcardMatch>()
                .Where(slot => !slot.Empty && slot.Config.SlotVariant == variantCode)
                .FirstOrDefault((ItemSlotBagContentWithWildcardMatch?)null);

            string stateVariantCode = quiverSlot.Config.SlotStateVariant;

            Variants? variants;

            if (quiverNotEmptySlot == null)
            {
                variants = Variants.FromStack(sheathSlot.Itemstack);
                if (variants.Get(stateVariantCode) != quiverSlot.Config.EmptyStateCode)
                {
                    variants.Set(stateVariantCode, quiverSlot.Config.EmptyStateCode);
                    variants.ToStack(sheathSlot.Itemstack);
                    sheathSlot.MarkDirty();
                }
                continue;
            }

            variants = Variants.FromStack(sheathSlot.Itemstack);
            if (variants.Get(stateVariantCode) != quiverSlot.Config.FullStateCode)
            {
                variants.Set(stateVariantCode, quiverSlot.Config.FullStateCode);
                variants.ToStack(sheathSlot.Itemstack);
                sheathSlot.MarkDirty();
            }

            if (quiverSlot.Itemstack?.Collectible?.Attributes == null) continue;

            string metalVariantCode = quiverSlot.Config.SlotMetalVariant;
            string leatherVariantCode = quiverSlot.Config.SlotLeatherVariant;
            string woodVariantCode = quiverSlot.Config.SlotWoodVariant;

            SheathableStats stats = quiverNotEmptySlot.Itemstack.Collectible.Attributes.AsObject<SheathableStats>();

            variants = Variants.FromStack(sheathSlot.Itemstack);
            if (variants.Get(variantCode) != stats.InSheathVariantCode)
            {
                variants.Set(variantCode, stats.InSheathVariantCode);
                variants.ToStack(sheathSlot.Itemstack);
                sheathSlot.MarkDirty();
            }

            Variants inQuiverVariants = Variants.FromStack(quiverSlot.Itemstack);

            if (inQuiverVariants.Get(metalVariantCode) != null)
            {
                if (variants.Get(metalVariantCode) != inQuiverVariants.Get(metalVariantCode))
                {
                    variants.Set(metalVariantCode, inQuiverVariants.Get(metalVariantCode));
                    variants.ToStack(sheathSlot.Itemstack);
                    sheathSlot.MarkDirty();
                }
            }
            else
            {
                if (variants.Get(metalVariantCode) != stats.MetalVariantCode)
                {
                    variants.Set(metalVariantCode, stats.MetalVariantCode);
                    variants.ToStack(sheathSlot.Itemstack);
                    sheathSlot.MarkDirty();
                }
            }

            if (inQuiverVariants.Get(leatherVariantCode) != null)
            {
                if (variants.Get(leatherVariantCode) != inQuiverVariants.Get(leatherVariantCode))
                {
                    variants.Set(leatherVariantCode, inQuiverVariants.Get(leatherVariantCode));
                    variants.ToStack(sheathSlot.Itemstack);
                    sheathSlot.MarkDirty();
                }
            }
            else
            {
                if (variants.Get(leatherVariantCode) != stats.LeatherVariantCode)
                {
                    variants.Set(leatherVariantCode, stats.LeatherVariantCode);
                    variants.ToStack(sheathSlot.Itemstack);
                    sheathSlot.MarkDirty();
                }
            }

            if (inQuiverVariants.Get(woodVariantCode) != null)
            {
                if (variants.Get(woodVariantCode) != inQuiverVariants.Get(woodVariantCode))
                {
                    variants.Set(woodVariantCode, inQuiverVariants.Get(woodVariantCode));
                    variants.ToStack(sheathSlot.Itemstack);
                    sheathSlot.MarkDirty();
                }
            }
            else
            {
                if (variants.Get(woodVariantCode) != stats.WoodVariantCode)
                {
                    variants.Set(woodVariantCode, stats.WoodVariantCode);
                    variants.ToStack(sheathSlot.Itemstack);
                    sheathSlot.MarkDirty();
                }
            }
        }
    }
}
