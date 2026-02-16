using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace GemCuttingAndJewellery
{
    public class GemCuttingAndJewelleryModSystem : ModSystem
    {

        // Called on server and client
        // Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterItemClass(Mod.Info.ModID + ".dopstick", typeof(Items.ItemDopstick));
            api.RegisterItemClass(Mod.Info.ModID + ".lapdisk", typeof(Items.ItemLapDisk));
            api.RegisterItemClass(Mod.Info.ModID + ".uncutgem", typeof(Items.ItemUncutGem));
            api.RegisterItemClass(Mod.Info.ModID + ".handlens", typeof(Items.ItemHandLens));
            api.RegisterBlockClass(Mod.Info.ModID + ".facetingtable", typeof(Blocks.BlockFacetingTable));
            api.RegisterBlockClass(Mod.Info.ModID + ".mpbasefacetingtable", typeof(Blocks.Mechanical.BlockMPBaseFacetingTable));
            api.RegisterBlockEntityClass(Mod.Info.ModID + ".blockentityfacetingtable", typeof(BlockEntities.BlockEntityFacetingTable));
            api.RegisterBlockEntityBehaviorClass(Mod.Info.ModID + ".blockentitympbasefacetingtable", typeof(BlockEntities.Mechanical.BEBehaviorMPBaseFacetingTable));
            //api.RegisterBlockEntityBehaviorClass("Animatable", typeof(BEBehaviorAnimatable));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            Mod.Logger.Notification("Hello from template mod server side: " + Lang.Get("gemcuttingandjewellery:hello"));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            
            Mod.Logger.Notification("Hello from template mod client side: " + Lang.Get("gemcuttingandjewellery:hello"));
        }

    }
}
