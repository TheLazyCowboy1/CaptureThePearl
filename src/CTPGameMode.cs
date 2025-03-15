using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RainMeadow;
using UnityEngine;

namespace CaptureThePearl;

public class CTPGameMode : StoryGameMode
{
    public const string GameModeName = "Capture the Pearl";
    public const string GameModeDescription = "Team up with other players to steal the opposing team's pearls and bring them back to your home shelter!";

    public Dictionary<OnlinePlayer, byte> PlayerTeams = new();
    public string[] TeamShelters = new string[0];

    public byte NumberOfTeams = 2; //for now, make it always 2

    public bool setupTeams = false;
    public bool hasSpawnedIn = false; //this applies only to "myself", to decide whether to spawn in the team shelter or somewhere random

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

    public void SanitizeCTP()
    {
        PlayerTeams.Clear();
        setupTeams = false;
        hasSpawnedIn = false;
    }

    public void SetupTeams()
    {
        RainMeadow.RainMeadow.Debug("[CTP]: Setting up teams!");

        //get team shelters
        List<string> tempShelters = new(NumberOfTeams);
        for (int i = 0; i < NumberOfTeams; i++)
            tempShelters.Add(Helpers.RandomShelterChooser.GetRespawnShelter(region, currentCampaign, tempShelters.ToArray(), Plugin.Options.TeamShelterCloseness.Value));
        TeamShelters = tempShelters.ToArray();

        //assign player teams
        AssignPlayerTeams();

        setupTeams = true;
    }

    public void AssignPlayerTeams()
    {
        //remove players that have quit
        var playerKeys = PlayerTeams.Keys;
        foreach (var player in playerKeys)
        {
            if (!OnlineManager.players.Contains(player))
                PlayerTeams.Remove(player);
        }

        //check if we can skip all these over-complicated checks
        if (PlayerTeams.Count == OnlineManager.players.Count)
            return;

        //add players that are not currently in the team list

        //shuffle player list, so we don't get the same teams every time
        List<OnlinePlayer> tempPlayers = OnlineManager.players.ToList(); //make it distinct
        List<OnlinePlayer> shuffledPlayers = new(OnlineManager.players.Count);
        while (tempPlayers.Count > 0)
        {
            int idx = UnityEngine.Random.Range(0, tempPlayers.Count);
            shuffledPlayers.Add(tempPlayers[idx]);
            tempPlayers.RemoveAt(idx);
        }

        foreach (var player in shuffledPlayers)
        {
            if (!PlayerTeams.ContainsKey(player))
            {
                //get team counts
                int[] teamCounts = new int[NumberOfTeams];
                int minCount = Int32.MaxValue;
                for (byte i = 0; i < NumberOfTeams; i++)
                {
                    teamCounts[i] = PlayerTeams.Count(kvp => kvp.Value == i);
                    minCount = Math.Min(minCount, teamCounts[i]);
                }
                //get the list of teams that are valid options (team has the least number of players)
                List<byte> validTeams = new(NumberOfTeams);
                for (byte i = 0; i < NumberOfTeams; i++)
                {
                    if (teamCounts[i] == minCount)
                        validTeams.Add(i);
                }
                //actually add the player to a random valid team
                PlayerTeams.Add(player, validTeams[UnityEngine.Random.Range(0, validTeams.Count)]);
            }
        }

        RainMeadow.RainMeadow.Debug($"[CTP]: Players teams: {string.Join("; ", PlayerTeams.Select(kvp => kvp.ToString()))}");
    }

    public override ProcessManager.ProcessID MenuProcessId()
    {
        return Plugin.CTPMenuProcessID;
    }

    public override void LobbyTick(uint tick)
    {
        base.LobbyTick(tick);

        readyForGate = ReadyForGate.Closed; //ensure players can NEVER cross through gates

        if (!lobby.isOwner) //prevent joining the game unless I am added to a team
            changedRegions = !PlayerTeams.ContainsKey(OnlineManager.mePlayer);

        //add other players to the team if they join in; ensures team list is up to date
        if (isInGame)
        {
            AssignPlayerTeams();
        }
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

    public override void ResourceAvailable(OnlineResource onlineResource)
    {
        base.ResourceAvailable(onlineResource);
        if (onlineResource is Lobby)
            onlineResource.AddData(new CTPLobbyData());
    }

    public override void PreGameStart()
    {
        base.PreGameStart();

        //apply hooks here?
        CTPGameHooks.ApplyHooks();

        if (lobby.isOwner && !setupTeams)
            SetupTeams();
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
