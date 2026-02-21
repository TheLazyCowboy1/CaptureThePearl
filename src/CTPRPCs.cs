using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RainMeadow;

namespace CaptureThePearl;

public static class CTPRPCs
{
    [RPCMethod]
    public static void PointScored(byte team, byte loser)
    {
        if (CTPGameMode.IsCTPGameMode(out var gamemode))
            gamemode.TeamScored(team, loser);
    }

    [RPCMethod]
    public static void GameFinished()
    {
        if (CTPGameMode.IsCTPGameMode(out var gamemode))
            gamemode.EndGame();
    }

    [RPCMethod(runDeferred = true)] //defer just in case there's some sort of weird race condition where it tries to spawn before it's destroyed?
    public static void SpawnPearl(byte team)
    {
        if (CTPGameMode.IsCTPGameMode(out var gamemode))
            gamemode.SpawnPearl(team, gamemode.worldSession.world);
    }

    /* Deprecated
    [RPCMethod]
    //Received only by the host
    public static void RegisterTeamPearl(RPCEvent rpc, OnlinePhysicalObject opo, byte team)
    {
        RainMeadow.RainMeadow.Debug($"[CTP]: Received request to register pearl {opo} for team {team}.");
        if (!CTPGameMode.IsCTPGameMode(out var gamemode) || opo?.apo == null)
        {
            rpc.from.QueueEvent(new GenericResult.Fail(rpc));
            return;
        }
        if (team >= gamemode.TeamPearls.Length || gamemode.TeamPearls[team] != null)
        {
            //this pearl is already registered!!
            rpc.from.QueueEvent(new GenericResult.Fail(rpc));
            return;
        }
        gamemode.TeamPearls[team] = opo;
        gamemode.AddIndicator(opo, team);
        rpc.from.QueueEvent(new GenericResult.Ok(rpc));
        RainMeadow.RainMeadow.Debug($"[CTP]: Accepted request to register pearl {opo} for team {team}.");
    }

    [RPCMethod]
    //Received only by the host
    public static void DestroyTeamPearl(OnlinePhysicalObject opo, byte team)
    {
        RainMeadow.RainMeadow.Debug($"[CTP]: Received request to destroy pearl {opo} for team {team}.");
        if (CTPGameMode.IsCTPGameMode(out var gamemode))
        {
            if (team < gamemode.TeamPearls.Length && gamemode.TeamPearls[team] == opo)
            {
                //CTPGameMode.DestroyPearl(ref gamemode.TeamPearls[team]);
                gamemode.RemoveIndicator(team);
            }
        }
    }
    */
}
