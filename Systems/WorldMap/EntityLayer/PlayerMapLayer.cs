﻿using Cairo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{

    public class PlayerMapLayer : MarkerMapLayer
    {
        Dictionary<IPlayer, EntityMapComponent> MapComps = new Dictionary<IPlayer, EntityMapComponent>();
        ICoreClientAPI capi;
        Vec3d worldPos = new Vec3d();
        Vec2f viewPos = new Vec2f();

        LoadedTexture ownTexture;
        LoadedTexture otherTexture;

        public override string Title => "Players";
        public override EnumMapAppSide DataSide => EnumMapAppSide.Client;


        public PlayerMapLayer(ICoreAPI api, IWorldMapManager mapsink) : base(api, mapsink)
        {
            capi = (api as ICoreClientAPI);
        }

        private void Event_PlayerDespawn(IClientPlayer byPlayer)
        {
            EntityMapComponent mp = null;
            if (MapComps.TryGetValue(byPlayer, out mp))
            {
                if (mapSink.IsOpened) mapSink.RemoveMapData(mp);
                mp.Dispose();
                MapComps.Remove(byPlayer);
            }
        }

        private void Event_PlayerSpawn(IClientPlayer byPlayer)
        {
            if (capi.World.Config.GetBool("mapHideOtherPlayers", true) && byPlayer.PlayerUID != capi.World.Player.PlayerUID)
            {
                return;
            }

            if (mapSink.IsOpened && !MapComps.ContainsKey(byPlayer))
            {
                EntityMapComponent cmp = new EntityMapComponent(capi, otherTexture, byPlayer.Entity);
                MapComps[byPlayer] = cmp;
                mapSink.AddMapData(cmp);
            }
        }

        public override void OnLoaded()
        {
            if (capi != null)
            {
                // Only client side
                capi.Event.PlayerEntitySpawn += Event_PlayerSpawn;
                capi.Event.PlayerEntityDespawn += Event_PlayerDespawn;
            }
        }


        public override void OnMapOpenedClient()
        {
            int chunksize = api.World.BlockAccessor.ChunkSize;

            if (ownTexture == null)
            {
                ImageSurface surface = new ImageSurface(Format.Argb32, 32, 32);
                Context ctx = new Context(surface);
                ctx.SetSourceRGBA(0, 0, 0, 0);
                ctx.Paint();
                capi.Gui.Icons.DrawMapPlayer(ctx, 0, 0, 32, 32, new double[] { 0, 0, 0, 1 }, new double[] { 1, 1, 1, 1 });
                
                ownTexture = new LoadedTexture(capi, capi.Gui.LoadCairoTexture(surface, false), 16, 16);
                ctx.Dispose();
                surface.Dispose();
            }
            
            if (otherTexture == null)
            {
                ImageSurface surface = new ImageSurface(Format.Argb32, 32, 32);
                Context ctx = new Context(surface);
                ctx.SetSourceRGBA(0, 0, 0, 0);
                ctx.Paint();
                capi.Gui.Icons.DrawMapPlayer(ctx, 0, 0, 32, 32, new double[] { 0.3, 0.3, 0.3, 1 }, new double[] { 0.7, 0.7, 0.7, 1 });
                otherTexture = new LoadedTexture(capi, capi.Gui.LoadCairoTexture(surface, false), 16, 16);
                ctx.Dispose();
                surface.Dispose();
            }

            

            foreach (IPlayer player in capi.World.AllOnlinePlayers)
            {
                EntityMapComponent cmp = null;

                if (MapComps.TryGetValue(player, out cmp))
                {
                    cmp?.Dispose();
                    MapComps.Remove(player);
                }
                
                if (cmp != null)
                {
                    mapSink.RemoveMapData(cmp);
                }
                

                if (player.Entity == null)
                {
                    capi.World.Logger.Warning("Can't add player {0} to world map, missing entity :<", player.PlayerUID);
                    continue;
                }

                cmp = new EntityMapComponent(capi, player == capi.World.Player ? ownTexture : otherTexture, player.Entity);

                MapComps[player] = cmp;
                mapSink.AddMapData(cmp);
            }
        }

        public override void OnMapClosedClient()
        {
            Dispose();
            MapComps.Clear();
        }


        public override void Dispose()
        {
            foreach (var val in MapComps)
            {
                val.Value?.Dispose();
            }

            ownTexture?.Dispose();
            ownTexture = null;
            otherTexture?.Dispose();
            otherTexture = null;
        }



    }
}
