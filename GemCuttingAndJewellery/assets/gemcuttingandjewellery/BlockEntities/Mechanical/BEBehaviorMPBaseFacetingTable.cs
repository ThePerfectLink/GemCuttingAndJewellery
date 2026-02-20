using Cairo;
using GemCuttingAndJewellery.BlockEntities.Renderer;
using GemCuttingAndJewellery.Items;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;

namespace GemCuttingAndJewellery.BlockEntities.Mechanical
{
    internal class BEBehaviorMPBaseFacetingTable(BlockEntity blockentity) : BEBehaviorMPBase(blockentity)
    {
        // Changed: cache key now includes block code to avoid stale/shared mesh reuse.
        // Why: a single global key can keep old plate meshes and make visual swaps appear stuck.
        private Dictionary<string, MeshRef>? MeshCache;

        private Vec3f center = new Vec3f(0.5f, 0.5f, 0.5f);
        ICoreClientAPI? capi;
        protected float[] modifiers = new float[3] { 0, 0, 0 };
        public AssetLocation? facetingtable;
        public AssetLocation? facetingtableVert;
        public AssetLocation? facetingtablePlate;


        public bool shouldRender;
        private string[]? materialPossibilities;
        public ItemStack? material;

        protected VerticalGearRenderer? verticalGearRenderer;
        protected PlateRenderer? plateRenderer;

        protected float resistance = 0.0005f;
        public int animationMult = 1;
        public override float AngleRad => GetAngleRad();
        public override float GetResistance() { return resistance; }

        float GetAngleRad()
        {
            if (network == null) return lastKnownAngleRad;

            if (isRotationReversed())
            {
                return (lastKnownAngleRad = (GameMath.TWOPI * 2) - (network.AngleRad * this.GearedRatio) % (GameMath.TWOPI * 2));
            }

            return (lastKnownAngleRad = (network.AngleRad * this.GearedRatio) % (GameMath.TWOPI * 2));
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            MeshCache = ObjectCacheUtil.GetOrCreate(Api, "plateMesh-" + Block.Code, () => new Dictionary<string, MeshRef>());
            materialPossibilities = properties["plate-materials"].AsArray<String>();
            this.facetingtable = AssetLocation.Create("block/facetingtablebase", this.Block.Code?.Domain);
            this.facetingtableVert = AssetLocation.Create("block/facetingtablemechanismvert", this.Block.Code?.Domain);
            this.facetingtablePlate = AssetLocation.Create("block/facetingtablemechanismvertplate", this.Block.Code?.Domain);
            if (this.Block.Attributes?["facetingtablebase"].Exists == true)
            {
                facetingtable = Block.Attributes["facetingtablebase"].AsObject<AssetLocation>();
                facetingtableVert = Block.Attributes["facetingtablemechanismvert"].AsObject<AssetLocation>();
                facetingtablePlate = Block.Attributes["facetingtablemechanismvertplate"].AsObject<AssetLocation>();
            }
            facetingtable.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
            facetingtableVert.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
            facetingtablePlate.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
            //rendering additional components
            if (api.Side == EnumAppSide.Client)
            {
                capi = api as ICoreClientAPI;
                if(capi != null && verticalGearRenderer == null)
                {
                    GenMeshes(facetingtablePlate);
                    verticalGearRenderer = new VerticalGearRenderer(capi, Blockentity.Pos, MeshCache["vert"], this);
                    capi?.Event.RegisterRenderer(verticalGearRenderer, EnumRenderStage.Opaque, "facetvert");
                    if (material != null) 
                    {
                        shouldRender = true;
                        MeshCache.TryGetValue(material.Item.Code.Path, out MeshRef? mesh);
                        plateRenderer = new PlateRenderer(capi!, Blockentity.Pos, mesh!, this);
                        capi!.Event.RegisterRenderer(plateRenderer, EnumRenderStage.Opaque, "facetplate");
                    }
                    else {
                        shouldRender = false;
                    }
                }
            }

            switch (Block.Variant["side"])
            {
                
                case "north":
                    this.AxisSign = new int[] { 0, 0, 1};
                    OutFacingForNetworkDiscovery = BlockFacing.NORTH;
                    animationMult = 1;
                    break;
                //BUG: West has inverse power when connecting to existing power, must replace power
                case "west":
                    this.AxisSign = new int[] { -1, 0, 0 };
                    OutFacingForNetworkDiscovery = BlockFacing.WEST;
                    animationMult = -1;
                    break;
                //BUG: South has inverse power when connecting to existing power, must replace power
                case "south":
                    this.AxisSign = new int[] { 0, 0, 1};
                    OutFacingForNetworkDiscovery = BlockFacing.SOUTH;
                    animationMult = -1;
                    break;
                default:
                    this.AxisSign = new int[] { -1, 0, 0};
                    OutFacingForNetworkDiscovery = BlockFacing.EAST;
                    animationMult = 1;
                    break;
            }
        }
        //constucts full shape
        protected virtual MeshData? GetBaseMesh()
        {
            if (facetingtable != null)
            {
                return ObjectCacheUtil.GetOrCreate(this.Api, this.Block.Code + "base", () =>
                {
                    Shape baseShape = this.Api.Assets.TryGet(this.facetingtable).ToObject<Shape>();
                    capi!.Tesselator.TesselateShape(this.Block, baseShape, out var mesh);
                    return mesh;
                });
            }
            else
            {
                return null;
            }
        }

        private void GenMeshes(AssetLocation shapeLoc)
        {
            if (MeshCache!.Count > 0) { return; }
            foreach (string mat in materialPossibilities!)
            {
                if (MeshCache!.ContainsKey(mat)){ return; }
                Shape newShape = Api.Assets.TryGet(facetingtablePlate).ToObject<Shape>();

                if (newShape == null) return;
                newShape.Textures["material"] = $"game:block/metal/plate/{mat.Split('-')[1]}";

                // Changed: tessellate with ShapeTextureSource instead of block texture source.
                // Why: block-based tessellation can ignore this runtime texture override and render as lead.
                var texSource = new ShapeTextureSource(capi!, newShape, facetingtablePlate!.ToShortString());
                capi!.Tesselator.TesselateShape($"facetingtable-{mat}", newShape, out var mesh1, texSource);
                MeshCache[mat] = capi.Render.UploadMesh(mesh1);
            }
                
            if (!MeshCache.ContainsKey("vert"))
            {
                Shape shape = Api.Assets.TryGet(facetingtableVert).ToObject<Shape>();
                capi!.Tesselator.TesselateShape(this.Block, shape, out var mesh2);
                MeshCache["vert"] = capi.Render.UploadMesh(mesh2);
            }
        }

        //tesselates mechanism
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            MeshData baseMesh = this.GetBaseMesh()!;
            if (baseMesh != null) 
            {
                baseMesh = this.RotateMesh(baseMesh);
                mesher.AddMeshData(baseMesh);
                baseMesh.Dispose();
            }
            else
            {
                Api.World.Logger.Debug("capi is null!");
                return false;
            }
            return true;
        }
        // rotates initial meshes
        private MeshData RotateMesh(MeshData mesh)
        {
            mesh = mesh.Clone();

            switch (this.OutFacingForNetworkDiscovery.Code)
            {
                case "north":
                    mesh = mesh.Rotate(this.center, 0, -GameMath.PIHALF, 0);
                    modifiers = new float[3] { (-2f / 16f), 0, (2f / 16f) };
                    break;
                case "west":
                    mesh = mesh.Rotate(this.center, 0, 0, 0);
                    modifiers = new float[3] { 0, 0, 0};
                    break;
                case "south":
                    mesh = mesh.Rotate(this.center, 0, GameMath.PIHALF, 0);
                    modifiers = new float[3] { (-2f / 16f), 0, (-2f/16f)};
                    break;
                default:
                    mesh = mesh.Rotate(this.center, 0, GameMath.PI, 0);
                    modifiers = new float[3] { (-4f / 16f), 0, 0 };
                    break;
            }
            return mesh;
        }

        /*--------------------------------------------------------------------------*/

        // Make sure you're saving/loading state properly
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetItemstack("material", material);
            tree.SetBool("shouldRender", shouldRender);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            var oldRender = shouldRender;
            var oldMat = material;
            shouldRender = tree.GetBool("shouldRender");
            material = tree.GetItemstack("material");
            if (worldAccessForResolve.Side == EnumAppSide.Client)
            {
                if (shouldRender != oldRender)
                {
                    if (material != null && oldMat == null)
                    {
                        material.ResolveBlockOrItem(worldAccessForResolve);
                        if (MeshCache != null)
                        {
                            MeshCache.TryGetValue(material.Item.Code.Path, out MeshRef? mesh);
                            plateRenderer = new PlateRenderer(capi!, Blockentity.Pos, mesh!, this);
                            capi?.Event.RegisterRenderer(plateRenderer, EnumRenderStage.Opaque, "facetplate");
                        } 
                    } else if (material == null && oldMat != null)
                    {
                        plateRenderer!.Dispose();
                        plateRenderer = null;
                    }
                }
            }
        }

        public override void OnBlockRemoved()
        {
            verticalGearRenderer?.Dispose();
            verticalGearRenderer = null;
            if (plateRenderer != null && Api.Side == EnumAppSide.Client)
            {
                plateRenderer?.Dispose();
                plateRenderer = null;
            }
            base.OnBlockRemoved();
        }

        public override void OnBlockUnloaded()
        {
            verticalGearRenderer?.Dispose();
            verticalGearRenderer = null;
            if (plateRenderer != null && Api.Side == EnumAppSide.Client)
            {
                plateRenderer?.Dispose();
                plateRenderer = null;

            }
            foreach (var (_, mesh) in MeshCache!) {
                mesh.Dispose();
            }
            base.OnBlockUnloaded();
        }

        public float[] GetModifiers()
        {
            return modifiers;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);
            sb.AppendLine(string.Format(Lang.Get("Input: {0}", this.OutFacingForNetworkDiscovery.Code)));
        }
    }
}
