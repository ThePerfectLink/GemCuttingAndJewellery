using Vintagestory.API.Common;
using Vintagestory.API.Server;

#nullable disable

namespace GemCuttingAndJewellery.BlockEntities;
public class BlockEntityFacetingTable : BlockEntity
{
    private ICoreServerAPI sapi;
    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        this.sapi = api as ICoreServerAPI;
    }

}