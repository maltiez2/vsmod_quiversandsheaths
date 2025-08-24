using AttributeRenderingLibrary;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace QuiversAndSheaths;

public class ShapeTexturesFromAttributes : CollectibleBehavior, IContainedMeshSource, IShapeTexturesFromAttributes, IAttachableToEntity
{
    public Dictionary<string, List<object>> NameByType { get; protected set; } = new();
    public Dictionary<string, List<object>> DescriptionByType { get; protected set; } = new();
    public Dictionary<string, CompositeShape> ShapeByType { get; protected set; } = new();
    public Dictionary<string, Dictionary<string, CompositeTexture>> TexturesByType { get; protected set; } = new();
    public Dictionary<string, OrderedDictionary<string, CompositeShape>> AttachedShapeBySlotCodeByType { get; protected set; } = new();
    public Dictionary<string, string> CategoryCodeByType { get; protected set; } = new();
    public Dictionary<string, string[]> DisableElementsByType { get; protected set; } = new();
    public Dictionary<string, string[]> KeepElementsByType { get; protected set; } = new();
    public bool AddOverlayPrefix { get; protected set; } = true;

    Dictionary<string, CompositeShape> IShapeTexturesFromAttributes.shapeByType => ShapeByType;
    Dictionary<string, Dictionary<string, CompositeTexture>> IShapeTexturesFromAttributes.texturesByType => TexturesByType;

    private IAttachableToEntity? _attachable;
    private ICoreAPI? _api;
    private ICoreClientAPI? _clientApi;

    public ShapeTexturesFromAttributes(CollectibleObject collObj) : base(collObj) { }

    public override void OnLoaded(ICoreAPI api)
    {
        _clientApi = api as ICoreClientAPI;
        _api = api;
        _attachable = IAttachableToEntity.FromAttributes(collObj);
    }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);

        if (properties != null)
        {
            NameByType = properties["name"].AsObject<Dictionary<string, List<object>>>();
            DescriptionByType = properties["description"].AsObject<Dictionary<string, List<object>>>();

            ShapeByType = properties["shape"].AsObject<Dictionary<string, CompositeShape>>();
            TexturesByType = properties["textures"].AsObject<Dictionary<string, Dictionary<string, CompositeTexture>>>();

            AttachedShapeBySlotCodeByType = properties["attachedShapeBySlotCode"].AsObject<Dictionary<string, OrderedDictionary<string, CompositeShape>>>();
            CategoryCodeByType = properties["categoryCode"].AsObject<Dictionary<string, string>>();
            DisableElementsByType = properties["disableElements"].AsObject<Dictionary<string, string[]>>();
            KeepElementsByType = properties["keepElements"].AsObject<Dictionary<string, string[]>>();
            AddOverlayPrefix = properties["addOverlayPrefix"].AsBool(true);
        }
    }

    public override void OnUnloaded(ICoreAPI api)
    {
        Dictionary<string, MultiTextureMeshRef> meshRefs = ObjectCacheUtil.TryGet<Dictionary<string, MultiTextureMeshRef>>(api, "AttributeRenderingLibrary_BehaviorShapeTexturesFromAttributes_MeshRefs");
        meshRefs?.Foreach(meshRef => meshRef.Value?.Dispose());
        ObjectCacheUtil.Delete(api, "AttributeRenderingLibrary_BehaviorShapeTexturesFromAttributes_MeshRefs");
    }

    public override void OnBeforeRender(ICoreClientAPI clientApi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
    {
        Dictionary<string, MultiTextureMeshRef> meshRefs = ObjectCacheUtil.GetOrCreate(clientApi, "AttributeRenderingLibrary_BehaviorShapeTexturesFromAttributes_MeshRefs", () => new Dictionary<string, MultiTextureMeshRef>());

        string key = GetMeshCacheKey(itemstack);

        if (!meshRefs.TryGetValue(key, out MultiTextureMeshRef meshref))
        {
            MeshData mesh = GenMesh(itemstack, clientApi.ItemTextureAtlas, null);
            meshref = clientApi.Render.UploadMultiTextureMesh(mesh);
            meshRefs[key] = meshref;
        }

        renderinfo.ModelRef = meshref;
        renderinfo.NormalShaded = true;

        base.OnBeforeRender(clientApi, itemstack, target, ref renderinfo);
    }

    public override void GetHeldItemName(StringBuilder sb, ItemStack itemStack)
    {
        if (NameByType == null || !NameByType.Any())
        {
            return;
        }

        Variants variants = Variants.FromStack(itemStack);
        variants.FindByVariant(NameByType, out List<object> _langKeys);

        string name = variants.GetName(_langKeys);
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        sb.Clear();
        sb.Append(name);
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        if (DescriptionByType == null || !DescriptionByType.Any())
        {
            return;
        }

        Variants variants = Variants.FromStack(inSlot.Itemstack);
        variants.FindByVariant(DescriptionByType, out List<object> _langKeys);
        variants.GetDescription(dsc, _langKeys);
        variants.GetDebugDescription(dsc, withDebugInfo);
    }

    public virtual MeshData GetOrCreateMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas)
    {
        MeshData mesh = RenderExtensions.GenEmptyMesh();

        Variants variants = Variants.FromStack(itemstack);
        variants.FindByVariant(ShapeByType, out CompositeShape ucshape);
        ucshape ??= itemstack.Item.Shape;

        if (ucshape == null) return mesh;

        CompositeShape rcshape = variants.ReplacePlaceholders(ucshape.Clone());
        rcshape.Base = rcshape.Base.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json");

        Shape? shape = _clientApi?.Assets.TryGet(rcshape.Base)?.ToObject<Shape>();
        if (shape == null) return mesh;

        UniversalShapeTextureSource stexSource = new(_clientApi, targetAtlas, shape, rcshape.Base.ToString());
        Dictionary<string, AssetLocation>? prefixedTextureCodes = null;
        string overlayPrefix = "";

        if (rcshape.Overlays != null && rcshape.Overlays.Length > 0)
        {
            overlayPrefix = GetMeshCacheKey(itemstack);
            prefixedTextureCodes = ShapeOverlayHelper.AddOverlays(_clientApi, AddOverlayPrefix ? overlayPrefix : "", variants, stexSource, shape, rcshape);
        }

        foreach ((string textureCode, CompositeTexture texture) in itemstack.Item.Textures)
        {
            stexSource.textures[textureCode] = texture;
        }

        ShapeOverlayHelper.BakeVariantTextures(_clientApi, stexSource, variants, TexturesByType, prefixedTextureCodes, AddOverlayPrefix ? overlayPrefix : "");

        _clientApi?.Tesselator.TesselateShape("ShapeTexturesFromAttributes behavior", shape, out mesh, stexSource, quantityElements: rcshape.QuantityElements, selectiveElements: rcshape.SelectiveElements);
        return mesh;
    }

    public virtual MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos)
    {
        return GetOrCreateMesh(itemstack, targetAtlas);
    }

    public virtual string GetMeshCacheKey(ItemStack itemstack)
    {
        return $"{itemstack.Collectible.Code}-{Variants.FromStack(itemstack)}";
    }



    void IAttachableToEntity.CollectTextures(ItemStack stack, Shape shape, string texturePrefixCode, Dictionary<string, CompositeTexture> intoDict)
    {
        foreach ((string textureCode, CompositeTexture texture) in stack.Item.Textures)
        {
            shape.Textures[textureCode] = texture.Baked.BakedName;
        }

        Dictionary<string, Dictionary<string, CompositeTexture>> texturesByType = new();

        if (stack.Collectible.GetCollectibleInterface<IShapeTexturesFromAttributes>() is IShapeTexturesFromAttributes fromAttributes)
        {
            texturesByType = fromAttributes.texturesByType;
        }

        Variants variants = Variants.FromStack(stack);
        ICoreClientAPI capi = _api as ICoreClientAPI;
        if (variants.FindByVariant(texturesByType, out Dictionary<string, CompositeTexture> _textures))
        {
            foreach ((string textureCode, CompositeTexture texture) in _textures)
            {
                CompositeTexture ctex = texture.Clone();
                ctex = variants.ReplacePlaceholders(ctex);
                if (!_api.Assets.Exists(ctex.Base.CopyWithPathPrefixAndAppendixOnce("textures/", ".png")))
                {
                    ctex.Base.Path = "unknown";
                    ctex.Base.Domain = "game";
                }
                ctex.Bake(_api.Assets);
                intoDict[textureCode] = ctex;
                shape.Textures[textureCode] = ctex.Baked.BakedName;
            }
        }
    }

    CompositeShape IAttachableToEntity.GetAttachedShape(ItemStack stack, string slotCode)
    {
        Variants variants = Variants.FromStack(stack);
        variants.FindByVariant(ShapeByType, out CompositeShape ucshape);
        ucshape ??= stack.Item.Shape;
        CompositeShape rcshape = variants.ReplacePlaceholders(ucshape.Clone());

        return rcshape;
    }

    string IAttachableToEntity.GetCategoryCode(ItemStack stack)
    {
        if (CategoryCodeByType == null || !CategoryCodeByType.Any())
        {
            return _attachable?.GetCategoryCode(stack);
        }

        Variants variants = Variants.FromStack(stack);
        variants.FindByVariant(CategoryCodeByType, out string categoryCode);
        return categoryCode;
    }

    string[] IAttachableToEntity.GetDisableElements(ItemStack stack)
    {
        if (DisableElementsByType == null || !DisableElementsByType.Any())
        {
            return _attachable?.GetDisableElements(stack);
        }

        Variants variants = Variants.FromStack(stack);
        variants.FindByVariant(DisableElementsByType, out string[] disableElements);
        return disableElements;
    }

    string[] IAttachableToEntity.GetKeepElements(ItemStack stack)
    {
        if (KeepElementsByType == null || !KeepElementsByType.Any())
        {
            return _attachable?.GetKeepElements(stack);
        }

        Variants variants = Variants.FromStack(stack);
        variants.FindByVariant(KeepElementsByType, out string[] keepElements);
        return keepElements;
    }

    string IAttachableToEntity.GetTexturePrefixCode(ItemStack stack)
    {
        string texturePrefixCode = stack.Collectible.GetCollectibleInterface<IContainedMeshSource>().GetMeshCacheKey(stack);
        return texturePrefixCode;
    }

    bool IAttachableToEntity.IsAttachable(Entity toEntity, ItemStack itemStack)
    {
        return true;
    }

    int IAttachableToEntity.RequiresBehindSlots { get; set; }
}
