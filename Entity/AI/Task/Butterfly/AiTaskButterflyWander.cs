﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class AiTaskButterflyWander : AiTaskBase
    {
        public Vec3d MainTarget;

        float moveSpeed = 0.03f;
        float wanderChance = 0.015f;
        float maxHeight = 7f;
        float? preferredLightLevel;

        float wanderDuration;
        float desiredYaw;
        float desiredflyHeightAboveGround;
        float desiredYMotion;

        float minTurnAnglePerSec;
        float maxTurnAnglePerSec;
        float curTurnRadPerSec;


        public AiTaskButterflyWander(EntityAgent entity) : base(entity)
        {

        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);
            
            if (taskConfig["movespeed"] != null)
            {
                moveSpeed = taskConfig["movespeed"].AsFloat(0.03f);
            }

            if (taskConfig["wanderChance"] != null)
            {
                wanderChance = taskConfig["wanderChance"].AsFloat(0.015f);
            }

            if (taskConfig["maxHeight"] != null)
            {
                maxHeight = taskConfig["maxHeight"].AsFloat(7f);
            }

            if (taskConfig["preferredLightLevel"] != null)
            {
                preferredLightLevel = taskConfig["preferredLightLevel"].AsFloat(-99);
                if (preferredLightLevel < 0) preferredLightLevel = null;
            }


            if (entity?.Properties?.Server?.Attributes != null)
            {
                minTurnAnglePerSec = (float)entity.Properties.Server?.Attributes.GetTreeAttribute("pathfinder").GetFloat("minTurnAnglePerSec", 250);
                maxTurnAnglePerSec = (float)entity.Properties.Server?.Attributes.GetTreeAttribute("pathfinder").GetFloat("maxTurnAnglePerSec", 450);
            }
            else
            {
                minTurnAnglePerSec = 250;
                maxTurnAnglePerSec = 450;
            }
        }

        public override bool ShouldExecute()
        {
            return true;
        }


        public override void StartExecute()
        {
            base.StartExecute();

            wanderDuration = 0.5f + (float)entity.World.Rand.NextDouble() * 5;
            desiredYaw = (float)(entity.ServerPos.Yaw + 2 * GameMath.TWOPI * (entity.World.Rand.NextDouble() - 0.5));

            desiredflyHeightAboveGround = 1 + 4 * (float)entity.World.Rand.NextDouble() + 4 * (float)(entity.World.Rand.NextDouble() * entity.World.Rand.NextDouble());
            ReadjustFlyHeight();

            entity.Controls.Forward = true;
            curTurnRadPerSec = minTurnAnglePerSec + (float)entity.World.Rand.NextDouble() * (maxTurnAnglePerSec - minTurnAnglePerSec);
            curTurnRadPerSec *= GameMath.DEG2RAD * 50 * moveSpeed;
        }

        public override bool ContinueExecute(float dt)
        {
            if (entity.OnGround || entity.World.Rand.NextDouble() < 0.03)
            {
                ReadjustFlyHeight();
            }


            wanderDuration -= dt;
            
            float yawDist = GameMath.AngleRadDistance(entity.ServerPos.Yaw, desiredYaw);
            entity.ServerPos.Yaw += GameMath.Clamp(yawDist, -curTurnRadPerSec * dt, curTurnRadPerSec * dt);
            entity.ServerPos.Yaw = entity.ServerPos.Yaw % GameMath.TWOPI;



            double cosYaw = Math.Cos(entity.ServerPos.Yaw);
            double sinYaw = Math.Sin(entity.ServerPos.Yaw);
            entity.Controls.WalkVector.Set(sinYaw, desiredYMotion, cosYaw);
            entity.Controls.WalkVector.Mul(moveSpeed);
            

            if (entity.Swimming)
            {
                entity.Controls.WalkVector.Y = -2 * moveSpeed;
            }

            if (entity.CollidedHorizontally)
            {
                wanderDuration -= 10 * dt;
            }


            return wanderDuration > 0;
        }

        private void ReadjustFlyHeight()
        {
            int terrainYPos = entity.World.BlockAccessor.GetTerrainMapheightAt(entity.LocalPos.AsBlockPos);
            double preferredHeight = terrainYPos + desiredflyHeightAboveGround;

            double curOffset = entity.LocalPos.Y - preferredHeight;

            if (curOffset < 1) desiredYMotion = moveSpeed * (float)(0.75 + rand.NextDouble() * 0.5);
            if (curOffset > 1) desiredYMotion = -moveSpeed * (float)(0.75 + rand.NextDouble() * 0.5);
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            
        }
        
    }
}
