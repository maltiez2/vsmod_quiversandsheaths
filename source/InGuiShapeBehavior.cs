using AttributeRenderingLibrary;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace QuiversAndSheaths;

public class ShapeReplacement : CollectibleBehavior, IContainedMeshSource, IShapeTexturesFromAttributes
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
    public EnumItemRenderTarget[] Targets { get; protected set; } = [];

    Dictionary<string, CompositeShape> IShapeTexturesFromAttributes.shapeByType => ShapeByType;
    Dictionary<string, Dictionary<string, CompositeTexture>> IShapeTexturesFromAttributes.texturesByType => TexturesByType;

    private IAttachableToEntity? _attachable;
    private ICoreAPI? _api;
    private ICoreClientAPI? _clientApi;

    public ShapeReplacement(CollectibleObject collObj) : base(collObj) { }

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

            Targets = properties["renderTargets"].AsObject<string[]>([]).Select(Enum.Parse<EnumItemRenderTarget>).ToArray();
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
        if (!Targets.Contains(target)) return;

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

    public virtual MeshData GetOrCreateMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas)
    {
        MeshData mesh = RenderExtensions.GenEmptyMesh();

        Variants variants = Variants.FromStack(itemstack);
        variants.FindByVariant(ShapeByType, out CompositeShape ucshape);
        ucshape ??= ShapeByType.Values.FirstOrDefault() ?? itemstack.Item.Shape;

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

        _clientApi?.Tesselator.TesselateShape("InGuiShape behavior", shape, out mesh, stexSource, quantityElements: rcshape.QuantityElements, selectiveElements: rcshape.SelectiveElements);
        return mesh;
    }

    public virtual MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos)
    {
        return GetOrCreateMesh(itemstack, targetAtlas);
    }

    public virtual string GetMeshCacheKey(ItemStack itemstack)
    {
        return $"{itemstack.Collectible.Code}-{Variants.FromStack(itemstack)}-gui";
    }
}
