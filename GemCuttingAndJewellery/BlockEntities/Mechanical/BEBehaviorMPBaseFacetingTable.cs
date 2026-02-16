using GemCuttingAndJewellery.BlockEntities.Renderer;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
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
    internal class BEBehaviorMPBaseFacetingTable : BEBehaviorMPBase
    {
        // Changed: cache key now includes block code to avoid stale/shared mesh reuse.
        // Why: a single global key can keep old plate meshes and make visual swaps appear stuck.
        private Dictionary<string, MeshRef> meshCache => ObjectCacheUtil.GetOrCreate(Api, "plateMesh-" + Block.Code, () => new Dictionary<string, MeshRef>());

        private Vec3f center = new Vec3f(0.5f, 0.5f, 0.5f);
        ICoreClientAPI? capi;
        protected float[] modifiers = new float[3] { 0, 0, 0 };
        public AssetLocation? facetingtable;
        public AssetLocation? facetingtableVert;
        public AssetLocation? facetingtablePlate;

        private string[] materialPossibilities = { "copper", "lead" };
        public string? material;

        public bool shouldRenderPlate;

        protected bool rotating;
        protected VerticalGearRenderer? verticalGearRenderer;
        protected PlateRenderer? plateRenderer;

    

        public Action? OnConnected;
        public Action? OnDisconnected;
        protected float resistance = 0.005f;
        public int animationMult = 1;
        public override float AngleRad => GetAngleRad();

        public BEBehaviorMPBaseFacetingTable(BlockEntity blockentity) : base(blockentity) { }

        protected virtual bool AddBase => true;

        public override float GetResistance() { return resistance; }

        private float GetAngleRad()
        {
            if (this.network != null)
            {
                return this.network.AngleRad;
            }
            return 0;
        }



        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            //resistance = properties["resistance"].AsFloat(0.1f);
            shouldRenderPlate = false;
            base.Initialize(api, properties);
            material = "lead";
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
                if(capi != null)
                {
                    GenMeshes(facetingtablePlate);
                    verticalGearRenderer = new VerticalGearRenderer(capi, Blockentity.Pos, meshCache["vert"], this);
                    capi?.Event.RegisterRenderer(verticalGearRenderer, EnumRenderStage.Opaque, "facetvert");
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
        protected virtual MeshData GetBaseMesh()
        {
            if (facetingtable != null)
            {
                return ObjectCacheUtil.GetOrCreate(this.Api, this.Block.Code + "base", () =>
                {
                    Shape baseShape = this.Api.Assets.TryGet(this.facetingtable).ToObject<Shape>();
                    capi!.Tesselator.TesselateShape(this.Block, baseShape, out var mesh);
                    capi.Render.UploadMesh(mesh);
                    return mesh;
                });
            }
            else
            {
                return new MeshData();
            }
        }

        private void GenMeshes(AssetLocation shapeLoc)
        {
            if (meshCache.Count > 0) {
                return; 
            }
            foreach (string mat in materialPossibilities)
            {
                // Changed: skip already-generated entries.
                // Why: avoids duplicate mesh uploads and keeps behavior deterministic on reload.
                if (meshCache.ContainsKey(mat))
                {
                    continue;
                }

                Shape newShape = Api.Assets.TryGet(facetingtablePlate).ToObject<Shape>().Clone();

                if (newShape == null) return;
                // Changed: apply plate texture per material before tessellation.
                // Why: this is what makes each cached mesh visually different.
                newShape.Textures["material"] = $"game:block/metal/plate/{mat}";

                // Changed: tessellate with ShapeTextureSource instead of block texture source.
                // Why: block-based tessellation can ignore this runtime texture override and render as lead.
                var texSource = new ShapeTextureSource(capi!, newShape, facetingtablePlate!.ToShortString());
                capi.Tesselator.TesselateShape($"facetingtable-plate-{mat}", newShape, out var mesh1, texSource);
                meshCache[mat] = capi.Render.UploadMesh(mesh1);
            }

            if (!meshCache.ContainsKey("vert"))
            {
            Shape shape = Api.Assets.TryGet(facetingtableVert).ToObject<Shape>();
            capi!.Tesselator.TesselateShape(this.Block, shape, out var mesh2);
            meshCache["vert"] = capi.Render.UploadMesh(mesh2);
        }
        }

        //tesselates mechanism
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (this.AddBase)
            {
                MeshData baseMesh;
                if (this.capi != null)
                {
                    baseMesh = this.GetBaseMesh();
                    baseMesh = this.RotateMesh(baseMesh);
                    if (baseMesh != null)
                    { mesher.AddMeshData(baseMesh); }
                }
                else
                {
                    Api.World.Logger.Debug("capi is null!");
                    return false;
                }
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

        public void OnPlayerInteract(IPlayer player)
        {
            // Changed: server-authoritative interaction handling.
            // Why: only server should mutate synced block entity state.
            if (Api.Side != EnumAppSide.Server)
            {
                return;
            }

            if (!shouldRenderPlate)
            {
                int currentIndex = Array.IndexOf(materialPossibilities, material);
                material = materialPossibilities[(currentIndex + 1) % materialPossibilities.Length];

            if (!shouldRenderPlate)
            {
                shouldRenderPlate = true;
                return;
            }

        }

        // Make sure you're saving/loading state properly
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBool("renderPlate", shouldRenderPlate);
            tree.SetString("material", material);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            bool wasRendered = shouldRenderPlate;
            shouldRenderPlate = tree.GetBool("renderPlate");
            material = tree.GetString("material", "lead");

            if (worldAccessForResolve.Side == EnumAppSide.Client)
            {

                if (shouldRenderPlate && (!wasRendered || plateRenderer == null))
                {
                    if (material == null || !meshCache.TryGetValue(material, out MeshRef meshRef))
                    {
                        meshRef = meshCache["lead"];
                    }
                    if (plateRenderer != null)
                    {
                        capi!.Event.UnregisterRenderer(plateRenderer, EnumRenderStage.Opaque);
                        plateRenderer.Dispose();
                        plateRenderer = null;
                    } else {
                        plateRenderer = new PlateRenderer(capi!, Blockentity.Pos, meshRef, this);
                    }

                    capi.Event.RegisterRenderer(plateRenderer, EnumRenderStage.Opaque, "facetplate");
                }
                else if (!shouldRenderPlate && plateRenderer != null)
                {
                    capi!.Event.UnregisterRenderer(plateRenderer, EnumRenderStage.Opaque);
                    plateRenderer.Dispose();
                    plateRenderer = null;
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

            base.OnBlockUnloaded();
        }

        public float[] GetModifiers()
        {
            return modifiers;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);
            var rotation = this.Block.Variant["side"];
            sb.AppendLine(string.Format(Lang.Get("Input: {0}", this.OutFacingForNetworkDiscovery.Code)));
        }
    }
}
