using GemCuttingAndJewellery.BlockEntities.Mechanical;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;

namespace GemCuttingAndJewellery.Blocks.Mechanical
{
    internal class BlockMPBaseFacetingTable : BlockMPBase
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {

            if (byPlayer.InventoryManager.ActiveHotbarSlot.Empty)
            {
                var be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
                var behavior = be?.GetBehavior<BEBehaviorMPBaseFacetingTable>();

                if (behavior != null)
                {
                    // Server-side logic
                    if (world.Side == EnumAppSide.Server)
                    {
                        // Do your server-side stuff
                        behavior.OnPlayerInteract(byPlayer);

                        // Mark dirty to sync to client
                        be.MarkDirty(true); // true = sync to clients
                    }

                    // Client-side logic (optional)
                    if (world.Side == EnumAppSide.Client)
                    {
                        // Client-side feedback (sounds, particles, etc.)
                    }

                    return true; // Handled
                }
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }



        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            string side = this.Variant["side"];
            if (side == null) return false;

            return side == face.Code;
        }
       
        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack? byItemStack = null)
        {
            base.OnBlockPlaced(world, blockPos, byItemStack);
            BEBehaviorMPBaseFacetingTable BE = world.BlockAccessor.GetBlock(blockPos).GetBEBehavior<BEBehaviorMPBaseFacetingTable>(blockPos);
            BlockFacing OutputFace = BE.OutFacingForNetworkDiscovery;
            BlockPos OutputBlockPos = blockPos.AddCopy(OutputFace);
            Block nblock = world.BlockAccessor.GetBlock(OutputBlockPos);
            BlockFacing OutputOpposite = OutputFace.Opposite;
            //api.Logger.Debug("out opposite " + OutputOpposite.ToString());
            if (nblock is IMechanicalPowerBlock block)
            {
                //determines if power should be given to block once placed.
                if (block != null && block.HasMechPowerConnectorAt(world, OutputBlockPos, OutputOpposite))
                {
                    block.DidConnectAt(world, OutputBlockPos, OutputOpposite);
                    this.WasPlaced(world, blockPos, OutputFace);
                }
            }
        }

        /*--------------------------------------------------------------------------------*/

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            var mpFt = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPBaseFacetingTable>();
            api.Logger.Debug(mpFt.shouldRenderPlate.ToString());
            if (mpFt != null && !BEBehaviorMPAxle.IsAttachedToBlock(world.BlockAccessor, mpFt.Block, pos))
            {
                foreach (var face in BlockFacing.HORIZONTALS)
                {
                    BlockPos npos = pos.AddCopy(face);
                    var block = world.BlockAccessor.GetBlock(npos) as IMechanicalPowerBlock;
                    if (!(block is BlockAngledGears blockAngledGears))
                        continue;
                    if (blockAngledGears.Facings.Contains(face) && blockAngledGears.Facings.Length == 1)
                    {
                        world.BlockAccessor.BreakBlock(npos, null);
                    }
                }
            }
            base.OnNeighbourBlockChange(world, pos, neibpos);
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
        }
    }
}
