using GemCuttingAndJewellery.Systems;
using GemCuttingAndJewellery.Systems.Enums;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Common;
using Vintagestory.Common.Database;
using Vintagestory.GameContent;
using Vintagestory.Server;
using Vintagestory.ServerMods;

namespace GemCuttingAndJewellery.Items
{
    internal class ItemUncutGem : ItemGem
    {
        public SimpleParticleProperties? grindingParticles;
        public int[]? GemParticleColors;
        protected ICoreClientAPI? capi;
        private Dictionary<string, ModularMeshData>? modularMeshCache;

        public static readonly int[][] CubeCornerGroups = new int[][]
        {
            new int[] { 0, 15,  21 }, // corner 0,0,0
            new int[] { 1, 14,  18 }, // corner 1,0,0
            new int[] { 2,  5,  17 }, // corner 1,1,0
            new int[] { 3,  4,  22 }, // corner 0,1,0
            new int[] { 6,  9,  16 }, // corner 0,0,1
            new int[] { 7,  8,  23 }, // corner 1,0,1
            new int[] {10, 13,  19 }, // corner 1,1,1
            new int[] {11, 12,  20 }, // corner 0,1,1
        };

        private Dictionary<string, MultiTextureMeshRef>? meshRefCache;
        private Dictionary<string, MeshData>? meshCache;
        private int meshIndex;
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            meshCache = ObjectCacheUtil.GetOrCreate(api, "gem-" + this.Code, () => new Dictionary<string, MeshData>());
            meshRefCache = ObjectCacheUtil.GetOrCreate(api, "gemRef-" + this.Code, () => new Dictionary<string, MultiTextureMeshRef>());
            meshIndex = ObjectCacheUtil.GetOrCreate(api, "gemIndex-" + this.Code, () => 0);

            if ((capi = api as ICoreClientAPI) != null)
            {
                grindingParticles = new SimpleParticleProperties()
                {
                    MinQuantity = 0.2f,
                    MinPos = new Vec3d(0.15, 0, 0.15),
                    AddPos = new Vec3d(0, 0.01,0),
                    MinVelocity = new Vec3f(0, 0, 0),
                    LifeLength = 0.5f,
                    GravityEffect = 0.75f,
                    MinSize = 0.04f,
                    MaxSize = 0.2f,
                    ParticleModel = EnumParticleModel.Cube
                };
                GemParticleColors = capi!.ItemTextureAtlas.GetRandomColors(getOrCreateTexPos(this.Textures["gem"].Base));
            }
            
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {   
            if (itemstack.Attributes.HasAttribute("size"))
            {
                renderinfo.ModelRef = GetOrCreateMeshRef(itemstack);
                float size = (float)(Math.Log(itemstack.Attributes.GetFloat("size") + 2) / 3);
                switch (target)
                {
                    case EnumItemRenderTarget.HandTp:
                        renderinfo.Transform.Scale = size;
                        renderinfo.Transform.Translation = new FastVec3f(-0.60f / size, 0, -0.44f / size);
                        break;
                    case EnumItemRenderTarget.Ground:
                        renderinfo.Transform.Scale = size * 5;
                        renderinfo.Transform.Translation = new FastVec3f(-0.12f / size, 0, -0.08f / size);
                        break;

                }
            }
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }

        public override void GetHeldItemInfo(ItemSlot inslot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {

            if (inslot.Itemstack.Attributes.HasAttribute("quality"))
            {
                dsc.AppendLine("Quality: " + inslot.Itemstack.Attributes.GetFloat("quality", 0) + "%");
            }
            if (inslot.Itemstack.Attributes.HasAttribute("size"))
            {
                dsc.AppendLine("Size: " + inslot.Itemstack.Attributes.GetFloat("size", 0) + " carats");
            }

            base.GetHeldItemInfo(inslot, dsc, world, withDebugInfo);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (!slot.Itemstack.Attributes.HasAttribute("size") && byEntity.LeftHandItemSlot.Itemstack != null && byEntity.LeftHandItemSlot.Itemstack.Item is ItemHandLens)
            {

                handling = EnumHandHandling.PreventDefault;
            }
            else
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            }

        }
        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.LeftHandItemSlot != null)
            {
                if (byEntity.LeftHandItemSlot.Itemstack != null)
                {
                    if (byEntity.LeftHandItemSlot.Itemstack.Item is ItemHandLens &&
                        (!slot.Itemstack.Attributes.HasAttribute("quality") || !slot.Itemstack.Attributes.HasAttribute("size")))
                    {
                        if (secondsUsed > 2)
                        {
                            EntityPlayer? player = byEntity as EntityPlayer;
                            if (player == null) return false;

                            var quality = slot.Itemstack.ItemAttributes["quality"];
                            var size = slot.Itemstack.ItemAttributes["size"];

                            Enum.TryParse(quality["dist"].AsString().ToUpper(), out EnumDistribution qualityEnum);
                            Enum.TryParse(size["dist"].AsString().ToUpper(), out EnumDistribution sizeEnum);

                            //Set random number generators
                            NatFloat itemQualityGen = new NatFloat(quality["avg"].AsFloat(), quality["var"].AsFloat(), qualityEnum);
                            NatFloat itemSizeGen = new NatFloat(size["avg"].AsFloat(), size["var"].AsFloat(), sizeEnum);

                            //Assign new floats from generators and make them readable
                            float itemQuality = itemQualityGen.nextFloat();
                            float itemSize = itemSizeGen.nextFloat();

                            //Take item from stack, mark stack dirty then give item attributes
                            ItemStack takenStack = slot.TakeOut(1);
                            slot.MarkDirty();
                            takenStack.Attributes.SetFloat("quality", (float)Math.Round(itemQuality * 100));
                            takenStack.Attributes.SetFloat("size", (float)Math.Round(itemSize * 100) / 100);
                            takenStack.Attributes.SetString("shape", meshIndex.ToString());
                            meshIndex = meshIndex + 1;
                            //Give item back to player or onto the ground
                            if (!player.Player.InventoryManager.TryGiveItemstack(takenStack))
                                api.World.SpawnItemEntity(takenStack, player.Pos.XYZ);

                            api.World.PlaySoundAt(new AssetLocation("sounds/player/coin" + (RandomNumberGenerator.GetInt32(6) + 1)), byEntity);
                            return false;
                        }
                    }
                    return true;
                }
            }
            return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        protected TextureAtlasPosition getOrCreateTexPos(AssetLocation texturePath)
        {
            TextureAtlasPosition texpos = capi!.BlockTextureAtlas[texturePath];

            if (texpos == null)
            {
                bool ok = capi.BlockTextureAtlas.GetOrInsertTexture(texturePath, out _, out texpos);

                if (!ok)
                {
                    capi.World.Logger.Warning("For render in fruit tree block " + this.Code + ", defined texture {1}, no such texture found.", texturePath);
                    return capi.BlockTextureAtlas.UnknownTexturePosition;
                }
            }

            return texpos;
        }
        public ModularMeshData GetOrCreateModularMesh(ItemStack gem)
        {
            string key = gem.Attributes.GetString("shape");
            if (!modularMeshCache!.ContainsKey(key))
            {
                capi!.Tesselator.TesselateItem(this, out MeshData rawMesh);
                TextureAtlasPosition texPos = getOrCreateTexPos(this.Textures["gem"].Base);
                modularMeshCache[key] = ModularMeshData.FromMeshData(rawMesh, texPos);
            }
            return modularMeshCache[key];
        }

        // Called when you need to actually render
        public void RecompileMesh(ItemStack gem)
        {
            string key = gem.Attributes.GetString("shape");
            MeshData compiled = modularMeshCache![key].ToMeshData();
            meshRefCache![key]?.Dispose();
            meshRefCache[key] = capi!.Render.UploadMultiTextureMesh(compiled);
            meshCache![key] = compiled;
        }

        public MultiTextureMeshRef GetOrCreateMeshRef(ItemStack gem) 
        {
            if (!meshCache!.ContainsKey(gem.Attributes.GetString("shape")))
            {
                api.Logger.Debug("tesselating mesh ref");
                capi!.Tesselator.TesselateItem(this, out MeshData mesh);
                meshRefCache![gem.Attributes.GetString("shape")] = capi.Render.UploadMultiTextureMesh(mesh);
                meshCache[gem.Attributes.GetString("shape")] = mesh;
            }
            return meshRefCache![gem.Attributes.GetString("shape")];
        }

        public MeshData GetMeshData(ItemStack gem)
        {
            if (!meshCache!.ContainsKey(gem.Attributes.GetString("shape")))
            {
                api.Logger.Debug("tesselating");
                capi!.Tesselator.TesselateItem(this, out MeshData mesh);
                meshRefCache![gem.Attributes.GetString("shape")] = capi.Render.UploadMultiTextureMesh(mesh);
                meshCache[gem.Attributes.GetString("shape")] = mesh;
            }
            return meshCache![gem.Attributes.GetString("shape")];
        }

        //public void ChangeMeshAndRef(ItemStack gem)
        //{
        //    float[] vertices = meshCache![gem.Attributes.GetString("shape")].xyz;   
        //    meshCache[gem.Attributes.GetString("shape")].xyz = vertices;
        //    meshRefCache![gem.Attributes.GetString("shape")].Dispose();
        //    meshRefCache[gem.Attributes.GetString("shape")] = capi!.Render.UploadMultiTextureMesh(meshCache[gem.Attributes.GetString("shape")]);
        //}


        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
        }
    }
}
