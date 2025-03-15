using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RainMeadow;

namespace CaptureThePearl;

public class CTPGameMode : StoryGameMode
{
    public const string GameModeDescription = "Team up with other players to steal the opposing team's pearls and bring them back to your home shelter!";

    public CTPGameMode(Lobby lobby) : base(lobby)
    {
        friendlyFire = true;//maybe not though,,, //yeah, this will change later
        requireCampaignSlugcat = false;
        //figure out a way to make all players on the other team muted, but that should probably not be in the constructor
        //mutedPlayers.Add();

        //plan:
        //defaultDenPos will be a string that contains the two team den positions
        //myLastDenPos will be used to set the actual den position used for spawns/respawns
    }

    public override ProcessManager.ProcessID MenuProcessId()
    {
        return Plugin.CTPMenuProcessID;
    }

    public override void LobbyTick(uint tick)
    {
        base.LobbyTick(tick);

        readyForGate = ReadyForGate.Closed; //ensure players can NEVER cross through gates
    }

    public override bool AllowedInMode(PlacedObject item)
    {
        if (item.type == PlacedObject.Type.DataPearl || item.type == PlacedObject.Type.UniqueDataPearl)
            return false; //don't allow data pearls except the ones I spawn in!!
        return base.AllowedInMode(item);
    }

    public override bool PlayerCanOwnResource(OnlinePlayer from, OnlineResource onlineResource)
    {
        return true; //allows the host to transfer the world session to another player
    }

    public override void PreGameStart()
    {
        base.PreGameStart();

        //apply hooks here?
        CTPGameHooks.ApplyHooks();
    }

    //public override void PostGameStart() //might be useful?

    public override void GameShutDown(RainWorldGame game)
    {
        base.GameShutDown(game);

        //remove hooks here? NOPE; doesn't work, sadly...
        //CTPGameHooks.RemoveHooks(); //moved to OnlineManager.LeaveLobby()
    }


    /// <summary>
    /// Safely determines whether a CTP gamemode is the currently active game mode.
    /// </summary>
    /// <param name="gamemode">The CTPGameMode if active, or null otherwise.</param>
    /// <returns></returns>
    public static bool IsCTPGameMode(out CTPGameMode gamemode)
    {
        gamemode = null;
        if (OnlineManager.lobby?.gameMode is CTPGameMode ctpMode)
        {
            gamemode = ctpMode;
            return true;
        }
        return false;
    }
}
