using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace GemCuttingAndJewellery.Items
{
    internal class ItemDopstick : Item
    {
        private Dictionary<string, MultiTextureMeshRef> meshCache = new Dictionary<string, MultiTextureMeshRef>();

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if(itemstack.Attributes.GetItemstack("gem") != null)
            {
                ItemStack gem = itemstack.Attributes.GetItemstack("gem");

                gem.ResolveBlockOrItem(capi.World);
                if (gem == null)
                {
                    capi.Logger.Error("Gem attribute is not an itemstack for dopstick, cannot create custom mesh");
                    return;
                }

                string cacheKey = gem.Item.Code.ToShortString();

                if (!itemstack.Attributes.GetBool("meshref"))
                {
                    if (!meshCache.ContainsKey(cacheKey))
                    {
                        MeshData combinedMesh;
                        capi.Tesselator.TesselateItem(this, out combinedMesh, capi.Tesselator.GetTextureSource(this));

                        if (gem?.Item == null)
                        {
                            capi.Logger.Warning("Gem item is null, cannot tesselate");
                            return;
                        }
                        MeshData overlayMesh;
                        capi.Tesselator.TesselateItem(gem.Item, out overlayMesh, capi.Tesselator.GetTextureSource(gem.Item));
                        combinedMesh.AddMeshData(overlayMesh);
                        meshCache[cacheKey] = capi.Render.UploadMultiTextureMesh(combinedMesh);
                    }
                    itemstack.Attributes.SetBool("meshref", true);
                }
                renderinfo.ModelRef = meshCache[cacheKey];
            }
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }

        public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, GridRecipe byRecipe)
        {
            ItemStack? gem = null;
            foreach (ItemSlot slot in allInputslots)
            {
                if (slot.Itemstack?.Item is ItemUncutGem)
                {
                    gem = slot.Itemstack;
                    outputSlot.Itemstack.Attributes.SetItemstack("gem", gem);

                    outputSlot.Itemstack.Attributes.SetString("gemCode", gem.Item.Code.ToShortString());

                    ITreeAttribute gemAttrs =  outputSlot.Itemstack.Attributes.GetOrAddTreeAttribute("gemData");
                    gemAttrs.SetFloat("size", gem.Attributes.GetFloat("size", 0));
                    gemAttrs.SetFloat("quality", gem.Attributes.GetFloat("quality", 0));

                    outputSlot.Itemstack.Attributes.SetBool("meshGen", false);
                    break;
                }
            }

            base.OnCreatedByCrafting(allInputslots, outputSlot, byRecipe);
        }

        public override void GetHeldItemInfo(ItemSlot inslot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            ItemStack gem = inslot.Itemstack.Attributes.GetItemstack("gem");

            // Append gem information to the description
            if (gem != null) {
                gem.ResolveBlockOrItem(world);

                if (gem.Item != null) {
                    dsc.AppendLine("Gem: " + gem.GetName());
                    dsc.AppendLine("Gem Size: " + gem.Attributes.GetFloat("size", 0));
                    dsc.AppendLine("Gem Quality: " + gem.Attributes.GetFloat("quality", 0));
                } else {
                    dsc.AppendLine("Gem: Error");
                } 
            } else {
                dsc.AppendLine("Gem: None");
            }
                base.GetHeldItemInfo(inslot, dsc, world, withDebugInfo);
        }   

        public override void OnUnloaded(ICoreAPI api)
        {
            foreach(var mesh in meshCache.Values)
            {
                mesh?.Dispose();
            }
            meshCache.Clear();
            base.OnUnloaded(api);
        }
    }
}
