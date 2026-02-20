using GemCuttingAndJewellery.BlockEntities.Mechanical;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace GemCuttingAndJewellery.BlockEntities.Renderer
{
    internal class PlateRenderer : IRenderer
    {
        public Matrixf ModelMat = new Matrixf();
        private AssetLocation[] sound = { new AssetLocation("gemcuttingandjewellery:sounds/grinder1"), new AssetLocation("gemcuttingandjewellery:sounds/grinder2"), new AssetLocation("gemcuttingandjewellery:sounds/grinder3") };
        private ICoreClientAPI api;
        private BlockPos pos;
        MeshRef meshref;
        BEBehaviorMPBaseFacetingTable table;

        public PlateRenderer(ICoreClientAPI coreClientAPI, BlockPos pos, MeshRef meshr, BEBehaviorMPBaseFacetingTable table)
        {
            this.api = coreClientAPI;
            this.pos = pos;
            meshref = meshr;
            this.table = table;
        }

        public double RenderOrder { get { return 0.5; } }
        public int RenderRange => 24;
        public void Dispose()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);

            //meshref.Dispose();
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (meshref == null) return;
            IRenderAPI rpi = api.Render;
            Vec3d camPos = api.World.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);
            prog.Tex2D = api.BlockTextureAtlas.AtlasTextures[0].TextureId;

            float[] modifier = table.GetModifiers();

            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
                .Translate(modifier[0], modifier[1], modifier[2])

                .Translate(10 / 16f, 0.5f, 0.5f)
                .RotateY(table.AngleRad * 4 * table.animationMult)
                .Translate(-10 / 16f, -0.5f, -0.5f)
                .Values
            ;
            if (table.Network !=  null && api.World.Rand.NextDouble() < table.Network.Speed / 10f)
            {
                api.World.PlaySoundAt(this.sound[(int)Math.Floor(api.World.Rand.NextDouble()*3)], pos, 0.4375, null, true, table.Network.Speed * table.GearedRatio * 8, table.Network.Speed * table.GearedRatio * 0.2f);
            }

            
            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
            rpi.RenderMesh(meshref);
            prog.Stop();
        }
    }
}
