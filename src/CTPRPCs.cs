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
}
