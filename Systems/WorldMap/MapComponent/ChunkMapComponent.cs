﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ChunkMapComponent : MapComponent
    {
        public float renderZ = 50;
        public Vec2i chunkCoord;
        public LoadedTexture Texture;

        Vec3d worldPos;
        Vec2f viewPos = new Vec2f();

        public ChunkMapComponent(ICoreClientAPI capi, Vec2i chunkCoord) : base(capi)
        {
            this.chunkCoord = chunkCoord;
            int chunksize = capi.World.BlockAccessor.ChunkSize;

            worldPos = new Vec3d(chunkCoord.X * chunksize, 0, chunkCoord.Y * chunksize);
        }


        public override void Render(GuiElementMap map, float dt)
        {
            map.TranslateWorldPosToViewPos(worldPos, ref viewPos);

            if (Texture.Disposed) throw new Exception("Fatal. Trying to render a disposed texture");

            capi.Render.Render2DTexture(
                Texture.TextureId,
                (int)(map.Bounds.renderX + viewPos.X),
                (int)(map.Bounds.renderY + viewPos.Y),
                (int)(Texture.Width * map.ZoomLevel),
                (int)(Texture.Height * map.ZoomLevel),
                renderZ
            );
        }

        public override void Dispose()
        {
            base.Dispose();
            Texture.Dispose();
        }

    }


}
