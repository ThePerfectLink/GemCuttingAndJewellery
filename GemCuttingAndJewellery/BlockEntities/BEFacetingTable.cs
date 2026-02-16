using GemCuttingAndJewellery.BlockEntities.Mechanical;
using GemCuttingAndJewellery.Items;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;

#nullable disable

namespace GemCuttingAndJewellery.BlockEntities;
public class BlockEntityFacetingTable : BlockEntityContainer
{
    private ICoreServerAPI sapi;

    private readonly InventoryGeneric _inventory;
    public override InventoryBase Inventory => _inventory;
    public override string InventoryClassName => "facetingtable";
    private ItemSlot LapSlot => Inventory[0];

    private ICoreClientAPI _capi;

    public BlockEntityFacetingTable()
    {
        _inventory = new(1, null, null);
    }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        this.sapi = api as ICoreServerAPI;
        _capi = api as ICoreClientAPI;
    }

    public bool OnInteract(IPlayer byPlayer, BlockSelection blockSel, IWorldAccessor world)
    {
        var handslot = byPlayer.InventoryManager.ActiveHotbarSlot;
        var handStack = handslot.Itemstack;

        var be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
        var behavior = be?.GetBehavior<BEBehaviorMPBaseFacetingTable>();

        if (handslot.Empty && !LapSlot.Empty)
        {
            LapSlot.TryPutInto(Api.World, handslot, 1);
            behavior.shouldRender = false;
            behavior.material = null;
            MarkDirty();
            return true;

        }
        else if (handStack?.Collectible is ItemLapDisk && LapSlot.Empty)
        {
            behavior.material = handStack.Clone();
            behavior.shouldRender = true;
            handslot.TryPutInto(Api.World, LapSlot, quantity: 1);
            MarkDirty();
            return true;
        }
        else
        {
            MarkDirty(true);
        }
        return false;
    }
}
