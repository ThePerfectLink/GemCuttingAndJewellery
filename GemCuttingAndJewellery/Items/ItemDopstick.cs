using GemCuttingAndJewellery.BlockEntities;
using GemCuttingAndJewellery.BlockEntities.Mechanical;
using GemCuttingAndJewellery.Blocks.Mechanical;
using GemCuttingAndJewellery.Systems;
using GemCuttingAndJewellery.Systems.Enums;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using static GemCuttingAndJewellery.Systems.ModularMeshData;

namespace GemCuttingAndJewellery.Items
{
    internal class ItemDopstick : Item
    {
        private Dictionary<string, MultiTextureMeshRef?> meshCache = new Dictionary<string, MultiTextureMeshRef?>();
        private AssetLocation[] sound = { new AssetLocation("gemcuttingandjewellery:sounds/grind1"), new AssetLocation("gemcuttingandjewellery:sounds/grind2"), new AssetLocation("gemcuttingandjewellery:sounds/grind3") };
        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (itemstack.Attributes.GetItemstack("gem") != null)
            {
                ItemStack gem = itemstack.Attributes.GetItemstack("gem");

                gem.ResolveBlockOrItem(capi.World);
                if (gem == null)
                {
                    capi.Logger.Error("Gem attribute is not an itemstack for dopstick, cannot create custom mesh");
                    return;
                }

                //string cacheKey = gem.Item.Code.ToShortString() + Math.Round(gem.Attributes.GetFloat("size"));
                string cacheKey = gem.Item.Code.ToShortString() + gem.Attributes.GetString("shape");

                if (!meshCache.ContainsKey(cacheKey))
                {
                    capi.Tesselator.TesselateItem(this, out MeshData combinedMesh, capi.Tesselator.GetTextureSource(this));

                    if (gem?.Item == null)
                    {
                        capi.Logger.Warning("Gem item is null, cannot tesselate");
                        return;
                    }
                    ItemUncutGem? item = gem.Item as ItemUncutGem;
                    MeshData overlayMesh = item!.GetMeshData(gem);
                    //capi.Tesselator.TesselateItem(gem.Item, out overlayMesh, capi.Tesselator.GetTextureSource(gem.Item));
                    //if(!renderedOnce.ContainsKey(cacheKey))
                    //{
                        float size = (float)(Math.Log(gem.Attributes.GetFloat("size", 1) + 2) / 3);
                        overlayMesh.ModelTransform(new ModelTransform() { Scale = size * 1.5f, Translation = new FastVec3f(0, (size * 1.5f + 3) / 2 - 2, 0) });
                        //renderedOnce[cacheKey] = true;
                    //}
                    combinedMesh.AddMeshData(overlayMesh);
                    meshCache[cacheKey] = capi.Render.UploadMultiTextureMesh(combinedMesh);
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
                    if (gem.Attributes.GetFloat("size") > 30)
                    {
                        outputSlot.Itemstack = null;
                        return;
                    }
                    outputSlot.Itemstack.Attributes.SetItemstack("gem", gem);
                    //outputSlot.Itemstack.Attributes.SetString("gemCode", gem.Item.Code.ToShortString());

                    break;
                }
            }
            base.OnCreatedByCrafting(allInputslots, outputSlot, byRecipe);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel != null && byEntity.World.BlockAccessor.GetBlock(blockSel.Position) != null && byEntity.World.BlockAccessor.GetBlock(blockSel.Position) is BlockMPBaseFacetingTable)
            {
                BEBehaviorMPBaseFacetingTable? beBe = byEntity.World.BlockAccessor.GetBlockEntity<BlockEntityFacetingTable>(blockSel.Position).GetBehavior<BEBehaviorMPBaseFacetingTable>();
                if (beBe.shouldRender && beBe.Network.Speed >= 0.1 && slot.Itemstack.Attributes.GetItemstack("gem") != null)
                {
                    slot.Itemstack.Attributes.SetBool("processing", true);
                    slot.Itemstack.Attributes.SetBlockPos("block", blockSel.Position);
                    slot.Itemstack.Attributes.SetFloat("elapsed", 0);
                    //slot.MarkDirty();
                    handling = EnumHandHandling.PreventDefault;
                    return;
                }
            }
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }


        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            
            if (slot.Itemstack.Attributes.GetBool("processing") && blockSel.Position == slot.Itemstack.Attributes.GetBlockPos("block"))
            {
                BlockEntityFacetingTable? be = byEntity.World.BlockAccessor.GetBlockEntity<BlockEntityFacetingTable>(blockSel.Position);
                BEBehaviorMPBaseFacetingTable ? beBe = be.GetBehavior<BEBehaviorMPBaseFacetingTable>();
                ItemStack gem = slot.Itemstack.Attributes.GetItemstack("gem");
                float speed = beBe.Network.Speed * beBe.GearedRatio;
                int direction = (int)Math.Clamp(beBe.Network.AngleRad, -1, 1);
                float quality = gem.Attributes.GetFloat("quality");
                float size = gem.Attributes.GetFloat("size");
                float elapsed = gem.Attributes.GetFloat("elapsed");
                if (byEntity.World is IServerWorldAccessor && byEntity is EntityPlayer ePlayer)
                {
                    gem.ResolveBlockOrItem(byEntity.World);
                    if (api.World.Rand.NextDouble() < beBe.Network.Speed / 2f) 
                    {
                        api.World.PlaySoundAt(sound[(int)Math.Floor(api.World.Rand.NextDouble() * 3)], blockSel.Position, 0.4375, byEntity as IPlayer, true, speed * 8, speed * 0.3f);
                    }


                    //grind down gemstone
                    //quality += (secondsUsed - elapsed) * 0.05f * speed;
                    //size -= (secondsUsed - elapsed) * 0.02f * speed;
                    //if (size < 20 && gem.Item.Code.FirstCodePart() == "largeuncutgem")
                    //{
                    //    ItemStack newGem = new ItemStack(api.World.GetItem(new AssetLocation("gemcuttingandjewellery:mediumuncutgem-" + gem.Item.Code.EndVariant())));
                    //    slot.Itemstack.Attributes.SetItemstack("gem", newGem);
                    //    gem = slot.Itemstack.Attributes.GetItemstack("gem");
                    //}
                    //else if (size < 10 && gem.Item.Code.FirstCodePart() == "mediumuncutgem")
                    //{
                    //    ItemStack newGem = new ItemStack(api.World.GetItem(new AssetLocation("gemcuttingandjewellery:smalluncutgem-" + gem.Item.Code.EndVariant())));
                    //    slot.Itemstack.Attributes.SetItemstack("gem", newGem);
                    //    gem = slot.Itemstack.Attributes.GetItemstack("gem");
                    //}
                    //ePlayer.Player.InventoryManager.BroadcastHotbarSlot();

                    //gem.Attributes.SetFloat("quality", Math.Min(((float)Math.Round(quality * 100) / 100), 100));
                    //gem.Attributes.SetFloat("size", (float)Math.Round(size * 100) / 100);
                    //slot.Itemstack.Attributes.GetFloat("elapsed", secondsUsed);
                    //slot.Itemstack.Attributes.SetItemstack("gem", gem);

                    //if (size < 0.5)
                    //{
                    //    slot.Itemstack.Attributes.SetItemstack("gem", null);
                       
                    //    return false;
                    //}

                    slot.MarkDirty();
                    return true;
                }
                else
                {
                    //Generate particles using variables from item
                    if (gem != null && gem.Item != null)
                    {
                        GrindGem(slot, GetToolMode(slot, byEntity as IPlayer, blockSel));
                        meshCache[gem.Item.Code.ToShortString() + gem.Attributes.GetString("shape")]?.Dispose();
                        meshCache.Remove(gem.Item.Code.ToShortString() + gem.Attributes.GetString("shape"));
                        ItemUncutGem gemItem = (ItemUncutGem)gem.Item;
                        if (gemItem.grindingParticles != null && gemItem.GemParticleColors != null)
                        {
                            
                            gemItem.grindingParticles.Color = gemItem.GemParticleColors[api.World.Rand.Next(25)];
                            double dist2 = byEntity.Pos.DistanceTo(blockSel.Position.ToVec3d().Add(blockSel.HitPosition)) - 0.3;
                            gemItem.grindingParticles.AddVelocity = new Vec3f(direction * -speed * 2, speed, direction*speed*2);
                            gemItem.grindingParticles.AddQuantity = speed * 2;
                            gemItem.grindingParticles.MinPos = byEntity.Pos.XYZ.Add(0, byEntity.LocalEyePos.Y, 0).Ahead(dist2, byEntity.Pos.Pitch, byEntity.Pos.Yaw);
                            byEntity.World.SpawnParticles(gemItem.grindingParticles);
                        }
                    }
                    return true;
                }
            } 
            return false;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (api.World.Side == EnumAppSide.Server)
            {
                slot.Itemstack.ResolveBlockOrItem(byEntity.World);
                slot.Itemstack.Attributes.SetBool("processing", false);
                slot.Itemstack.Attributes.SetItemstack("block", null);
                slot.MarkDirty();
            }

            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
        }  
        public override void GetHeldItemInfo(ItemSlot inslot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            ItemStack gem = inslot.Itemstack.Attributes.GetItemstack("gem");

            // Append gem information to the description
            if (gem != null) {
                gem.ResolveBlockOrItem(world);

                if (gem.Item != null) {
                    dsc.AppendLine("Gem: " + gem.GetName());
                    dsc.AppendLine("Gem Quality: " + gem.Attributes.GetFloat("quality", 0) + "%");
                    dsc.AppendLine("Gem Size: " + gem.Attributes.GetFloat("size", 0) +" carats");
 
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
            foreach (var mesh in meshCache.Values)
            {
                mesh?.Dispose();
            }
            meshCache.Clear();

            base.OnUnloaded(api);
        }

        public void GrindGem( ItemSlot slot, int toolMode)
        {
            if (slot.Itemstack.Attributes.GetItemstack("gem") is ItemStack gem && gem.Attributes.GetString("shape") != null)
            {
                if (gem.Item is not ItemUncutGem item) return;

                ModularMeshData modular = item.GetOrCreateModularMesh(gem);

                // toolMode maps to either a face or a corner
                // Every 5th mode is a face selector, the rest are corners
                if (toolMode % 5 == 0)
                {
                    int faceIndex = toolMode / 5;
                    if (faceIndex < modular.Faces.Count)
                        GrindFace(modular, modular.Faces[faceIndex], gem, item);
                }
                else
                {
                    // Corner index within the face
                    int faceIndex = toolMode / 5;
                    int cornerIndex = (toolMode % 5) - 1;
                    if (faceIndex < modular.Faces.Count)
                    {
                        Face face = modular.Faces[faceIndex];
                        if (cornerIndex < face.Vertices.Length)
                            GrindCorner(modular, face.Vertices[cornerIndex], gem, item);
                    }
                }

                // Recompile and re-upload the mesh after any grind operation
                item.RecompileMesh(gem);
            }
        }

        public void GrindFace(ModularMeshData modular, ModularMeshData.Face face, ItemStack gem, ItemUncutGem item)
        {
            // Move all 4 vertices of the face inward along the face normal
            foreach (var vertex in face.Vertices.Distinct()) // Distinct avoids moving degenerate dupes twice
            {
                vertex.Position -= face.Normal * 0.01f;
            }

            // Recalculate normals for all affected faces
            foreach (var vertex in face.Vertices.Distinct())
                foreach (var adjacentFace in vertex.AdjacentFaces)
                    adjacentFace.RecalculateNormal();
        }

        public void GrindCorner(ModularMeshData modular, ModularMeshData.Vertex corner, ItemStack gem, ItemUncutGem item)
        {
            // Collect the edge directions away from this corner across all adjacent faces
            Vec3f averageInward = Vec3f.Zero;
            foreach (var face in corner.AdjacentFaces)
            {
                foreach (var v in face.Vertices.Distinct())
                {
                    if (v != corner)
                    {
                        Vec3f edge = v.Position - corner.Position;
                        edge.Normalize();
                        averageInward += edge;
                    }
                }
            }
            averageInward.Normalize();

            // Move the corner inward
            corner.Position += averageInward * 0.01f;

            // Recalculate normals for all faces touching this corner
            foreach (var face in corner.AdjacentFaces)
                face.RecalculateNormal();

            // Add or update the clipping triangle face
            // Get the 3 unique corners of the clip face — one from each adjacent face
            var clipVertices = corner.AdjacentFaces
                .Select(f => f.Vertices.Distinct().FirstOrDefault(v => v != corner))
                .Where(v => v != null)
                .Distinct()
                .Take(3)
                .ToList();

            if (clipVertices.Count == 3)
            {
                // Check if a clip face already exists for this corner
                // A clip face is one whose vertices are all adjacent to this corner's faces
                // but don't include the corner itself
                var existingClip = modular.Faces.FirstOrDefault(f =>
                    f.Vertices.Distinct().All(v => clipVertices.Contains(v)));

                if (existingClip != null)
                {
                    // Just update — vertices are already references so positions are live
                    existingClip.RecalculateNormal();
                }
                else
                {
                    TextureAtlasPosition texPos = item.getOrCreateTexPos(item.Textures["gem"].Base);
                    api.Logger.Debug($"{gem.Item.Code}: {texPos}");
                    modular.AddClipFace(clipVertices[0]!, clipVertices[1]!, clipVertices[2]!, texPos);
                }
            }
        }

        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, int toolMode)
        {
            slot.Itemstack.Attributes.SetInt("toolMode", toolMode);
        }
        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection)
        {
            return slot.Itemstack.Attributes.GetInt("toolMode");
        }

        public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {
            if (slot.Itemstack.Attributes.GetItemstack("gem") is not ItemStack gem
                || gem.Attributes.GetString("shape") == null)
                return base.GetToolModes(slot, forPlayer, blockSel);

            if (gem.Item is not ItemUncutGem item)
                return base.GetToolModes(slot, forPlayer, blockSel);

            ModularMeshData modular = item.GetOrCreateModularMesh(gem);

            // 1 face selector + 4 corner selectors per face
            SkillItem[] modes = new SkillItem[modular.Faces.Count * 5];

            for (int f = 0; f < modular.Faces.Count; f++)
            {
                Face face = modular.Faces[f];
                int baseMode = f * 5;

                modes[baseMode] = new SkillItem()
                {
                    Linebreak = true,
                    Enabled = true,
                    Name = $"Face {f} (Normal: {face.Normal.X:F2}, {face.Normal.Y:F2}, {face.Normal.Z:F2})"
                };

                for (int c = 0; c < 4; c++)
                {
                    Vec3f pos = face.Vertices[c].Position;
                    modes[baseMode + 1 + c] = new SkillItem()
                    {
                        Enabled = true,
                        Name = $"Corner {c}: ({pos.X:F2}, {pos.Y:F2}, {pos.Z:F2})"
                    };
                }
            }

            return modes;
        }
    }
}
