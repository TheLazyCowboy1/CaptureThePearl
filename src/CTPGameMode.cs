using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RainMeadow;
using RWCustom;
using UnityEngine;
using static CaptureThePearl.PearlTrackingData;

namespace CaptureThePearl;

public partial class CTPGameMode : StoryGameMode
{
    public const string GameModeName = "Capture the Pearl";
    public const string GameModeDescription = "Team up with other players to steal the opposing team's pearls and bring them\nback to your home shelter!";

    //Synced variables
    public Dictionary<OnlinePlayer, byte> PlayerTeams = new();

    public string[] TeamShelters = new string[0];
    public int[] TeamPoints = new int[0];

    //(settings)
    public byte NumberOfTeams = 2; //for now, make it always 2
    public int TimerLength = 10; //length in minutes
    public bool SpawnCreatures = true;
    public bool ShouldMuteOtherTeams = false; //should probably be true by default; synced among everyone
    public float ShelterRespawnCloseness = Plugin.Options.RespawnCloseness.Value;
    public float PearlHeldSpeed = Plugin.Options.PearlHeldSpeed.Value;
    public bool ArmPlayers = Plugin.Options.ArmPlayers.Value;

    //Non-synced variables

    public bool gameSetup = false;
    public bool hasSpawnedIn = false; //this applies only to "myself", to decide whether to spawn in the team shelter or somewhere random

    public bool otherTeamsMuted = false; //whether players on other teams should be CURRENTLY muted

    public CTPGameMode(Lobby lobby) : base(lobby)
    {
        pearlTrackerOwner = lobby.owner;
    }

    public void SanitizeCTP()
    {
        RainMeadow.RainMeadow.Debug("[CTP]: Sanitizing gamemode");

        SanitizeTracker();

        PlayerTeams.Clear();
        TeamShelters = new string[0];
        TeamPoints = new int[0];
        gameSetup = false;
        hasSpawnedIn = false;
    }

    public void ClientSetup()
    {
        //apply game hooks!
        CTPGameHooks.ApplyHooks();

        SetupTrackerClientSide();

        otherTeamsMuted = true;

        gameSetup = true;
    }

    public void SetupTeams()
    {
        RainMeadow.RainMeadow.Debug("[CTP]: Setting up teams!");

        //get team shelters
        List<string> tempShelters = new(NumberOfTeams);
        for (byte i = 0; i < NumberOfTeams; i++)
        {
            try
            {
                tempShelters.Add(Helpers.RandomShelterChooser.GetRespawnShelter(region, currentCampaign, tempShelters.ToArray(), Plugin.Options.TeamShelterCloseness.Value));
            }
            catch (Exception ex) //if there aren't enough shelters in the region for all the teams
            {
                RainMeadow.RainMeadow.Error(ex);
                RainMeadow.RainMeadow.Debug($"[CTP]: Capping team count to {i}");
                NumberOfTeams = i;
                break;
            }
        }
        TeamShelters = tempShelters.ToArray();

        TeamPoints = new int[NumberOfTeams];

        //assign player teams
        AssignPlayerTeams();
    }

    public void AssignPlayerTeams()
    {
        if (!lobby.isOwner) return;

        //remove players that have quit
        var playerKeys = PlayerTeams.Keys.ToArray(); //make it a separate array
        foreach (var player in playerKeys)
        {
            if (!OnlineManager.players.Contains(player))
            {
                RainMeadow.RainMeadow.Debug($"[CTP]: Removing player {player}");
                PlayerTeams.Remove(player);
            }
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
                RainMeadow.RainMeadow.Debug($"[CTP]: Adding player {player}");
                //get team counts
                int[] teamCounts = new int[NumberOfTeams];
                int minCount = Int32.MaxValue;
                for (byte i = 0; i < NumberOfTeams; i++)
                {
                    teamCounts[i] = PlayerTeams.Count(kvp => kvp.Value == i) + TeamPoints[i]; //add TeamPoints, to favor losing teams with new players
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

        //ClientGameTick(); //done here as well as update, just to be extra safe

        if (gameSetup && lobby.isOwner) //isOwner should always be true
        {
            AssignPlayerTeams(); //add other players to the team if they join in; ensures team list is up to date

            //ScorePoints(); //instead handled by SearchForPearls
            //if (teamPearls.All(p => p == null))
                //SpawnPearls(true); //can cause desyncs
        }
    }

    public void ClientGameTick()
    {
        if (!gameSetup)
            return;
        if (TeamPoints.Length != TeamPearls.Length)
        {
            RainMeadow.RainMeadow.Error("[CTP]: Array length mismatches! (ClientGameTick)");
            return;
        }

        //SearchForPearls();
        //SpawnPearls();
        RepositionPearls();
        TestForScore();

        ManageIndicators();

        RespawnCreatures();
    }

    private int respawnCounter = 0;
    public void RespawnCreatures()
    {
        if (SpawnCreatures && pearlTrackerOwner == OnlineManager.mePlayer)
        {
            respawnCounter++;
            if (respawnCounter > 800) //once every 20 seconds
            {
                respawnCounter = 0;
                int respawned = 0;
                foreach (var ent in OnlineManager.recentEntities.Values)
                { //search for creatures that are mine, abstract, and dead
                    try
                    {
                        if (ent.isMine && ent is OnlineCreature oc && !oc.realized && oc.abstractCreature.state.dead)
                        {
                            if (oc.abstractCreature.state is not PlayerState) //don't revive players; that's stupid
                            {
                                oc.abstractCreature.state.alive = true; //revive!
                                if (oc.abstractCreature.state is HealthState hs)
                                    hs.health = 1f; //set it back to full health
                                respawned++;
                            }
                        }
                    }
                    catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
                }
                if (respawned > 0)
                    RainMeadow.RainMeadow.Debug($"[CTP]: Revived {respawned} creatures.");
            }
        }
    }

    public void EndGame()
    {
        RainMeadow.RainMeadow.Debug("[CTP]: Ending the game!");
        RainMeadow.RainMeadow.Debug("[CTP]: Points: " + string.Join(", ", TeamPoints.Select(p => p.ToString()).ToArray()));

        if (gameSetup)
        {
            //kill myself, like a good slugcat!
            var me = GetMyPlayer();
            if (me?.realizedObject is Player mePlayer && !mePlayer.dead)
                mePlayer.Die();

            //attempt to make an arena-style overlay... this probably won't go well...
            try
            {
                if (me?.world != null)
                {
                    var game = me.world.game;

                    //create phony arena sitting
                    var setup = new ArenaSetup.GameTypeSetup();
                    setup.InitAsGameType(ArenaSetup.GameTypeID.Sandbox);
                    var sitting = new ArenaSitting(setup, new MultiplayerUnlocks(game.rainWorld.progression, new()));
                    game.manager.arenaSitting = sitting; //...this might not go well...
                    int winningTeam = -1;
                    for (int i = 0; i < NumberOfTeams; i++)
                    {
                        var fakePlayer = new ArenaSitting.ArenaPlayer(i);
                        fakePlayer.playerClass = new(SlugcatStats.Name.values.GetEntry(i >= 3 ? i + 1 : i)); //skip Nightcat!
                        fakePlayer.wins = TeamPoints[i];
                        fakePlayer.sandboxWin = TeamPoints[i];
                        fakePlayer.score = TeamPoints[i];
                        fakePlayer.winner = true;
                        for (int j = 0; j < NumberOfTeams; j++) { if (i != j && TeamPoints[j] >= TeamPoints[i]) { fakePlayer.winner = false; break; } }
                        if (fakePlayer.winner)
                        {
                            winningTeam = i;
                            RainMeadow.RainMeadow.Debug($"[CTP]: Team {i} won!");
                        }
                        fakePlayer.alive = !TeamPoints.Any(t => t > TeamPoints[i]);
                        fakePlayer.timeAlive = TimerLength * 60 * 40; //idk if this works
                        sitting.players.Add(fakePlayer);
                    }

                    game.arenaOverlay = new Menu.ArenaOverlay(game.manager, sitting, sitting.players);
                    game.arenaOverlay.countdownToNextRound = Int32.MaxValue;
                    game.arenaOverlay.headingLabel.text = winningTeam >= 0 ? $"TEAM {winningTeam+1} WINS!" : "IT'S A DRAW!";
                    foreach (var box in game.arenaOverlay.resultBoxes)
                    {
                        box.playerNameLabel.text = Regex.Replace(box.playerNameLabel.text, "Player", "Team");
                    }
                    game.manager.sideProcesses.Add(game.arenaOverlay);
                }
            }
            catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
        }

        gameSetup = false;
        otherTeamsMuted = false;

        //SanitizeCTP();
    }

    private static int TeamToPearlIdx(byte team) => team + 2;
    public static byte PearlIdxToTeam(int idx) => (byte)(idx - 2);

    
    public Color GetTeamColor(int team)
    {
        if (team == 0) return Custom.hexToColor("EE0000");//red
        else if (team == 1) return Custom.hexToColor("0000EE");//blue
        else if (team == 2) return Custom.hexToColor("00EE00");//green
        else if (team == 3) return Custom.hexToColor("EEEE00");//yellow
        else if (team == 4) return Custom.hexToColor("DD00DD"); //purple //currently unused
        else if (team == 5) return Custom.hexToColor("00EEEE"); //cyan
        return Custom.hexToColor("EEEEEE");//white fallback if things go wrong
    }
    /// <summary>
    /// Lightens the color given in a standardized manner.
    /// </summary>
    /// <param name="color">The team color; accessible through gamemode.GetTeamColor().</param>
    /// <returns>The lightened team color.</returns>
    public static Color LighterTeamColor(Color color)
    {
        Color.RGBToHSV(color, out float h, out float s, out float v);
        s *= 0.8f;
        v = 1f;
        return Color.HSVToRGB(h, s, v);
    }

    public byte GetMyTeam() => PlayerTeams.TryGetValue(OnlineManager.mePlayer, out byte ret) ? ret : (byte)0;
    public bool OnMyTeam(OnlinePlayer player)
    {
        if (PlayerTeams.TryGetValue(OnlineManager.mePlayer, out byte mine) && PlayerTeams.TryGetValue(player, out byte his))
            return mine == his;
        return false;
    }

    public void TeamScored(int team, int loser)
    {
        //grant points
        TeamPoints[team]++;
        if (NumberOfTeams > 2)
        {
            TeamPoints[team]++; //+2 points
            TeamPoints[loser]--; //-1 point penalty
        }
        RainMeadow.RainMeadow.Debug($"[CTP]: Team {team} scored a point! Points: {TeamPoints[team]}");
        TeamScoredMessage(team);

        //if (teamPearls[loser] != null)
        //DestroyPearl(ref teamPearls[loser]);
        //if (pearlIndicators[loser] != null)
            //RemoveIndicator(loser);
    }

    
    private AbstractCreature GetMyPlayer()
    {
        //return (lobby.playerAvatars.Find(kvp => kvp.Key == OnlineManager.mePlayer).Value.FindEntity() as OnlinePhysicalObject).apo;
        return avatars.Find(c => c.isMine)?.apo as AbstractCreature;
    }

    public string GetTeamProperName(int team)
    {
        if (team == 0) return "Red";
        else if (team == 1) return "Blue";
        else if (team == 2) return "Green";
        else if (team == 3) return "Yellow";
        else if (team == 4) return "Purple"; //currently unused
        else if (team == 5) return "Cyan";
        else return "Unknown";
    }

    public void TeamScoredMessage(int team)
    {
        RainMeadow.RainMeadow.Debug($"[CTP] Sending team {GetTeamProperName(team)} scored message");
        ChatLogManager.LogMessage("", $"Team {GetTeamProperName(team)} has scored!");
        string scoreString = "";
        for (byte i = 0; i < NumberOfTeams; i++)
        {
            if (i > 0) scoreString += ", ";
            scoreString += $"{GetTeamProperName(i).Substring(0, 1)} : {TeamPoints[i]}";
        }
        ChatLogManager.LogMessage("", "  Scores: " + scoreString);
    }
    public void TeamHasAPearl(int team, int pearlIndex)
    {
        RainMeadow.RainMeadow.Debug($"[CTP] Sending team {GetTeamProperName(team)} has team {GetTeamProperName(pearlIndex)}'s pearl");

        if(team == pearlIndex) ChatLogManager.LogMessage("", $"Team {GetTeamProperName(team)} recovered their pearl!");
        else ChatLogManager.LogMessage("", $"Team {GetTeamProperName(team)} has team {GetTeamProperName(pearlIndex)}'s pearl!");
    }
    public void TeamLostAPearl(int pearlIndex)
    {
        //RainMeadow.RainMeadow.Debug($"[CTP] Sending team {GetTeamProperName(pearlIndex)}'s pearl is alone");
        return; //temporarily disabled
        ChatLogManager.LogMessage("", $"Team {GetTeamProperName(pearlIndex)}'s pearl is unoccupied!");
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

    //can be used to prevent spawning creatures
    public override bool ShouldLoadCreatures(RainWorldGame game, WorldSession worldSession)
    {
        return SpawnCreatures && base.ShouldLoadCreatures(game, worldSession);
        //return SpawnCreatures; //allows clients to also spawn creatures... might be a mess, idk
    }
    public override bool ShouldSyncAPOInWorld(WorldSession ws, AbstractPhysicalObject apo)
    {
        //if (apo is AbstractCreature ac)
            //return ac.state is PlayerState; //the only creature to sync in world is PLAYERS

        return base.ShouldSyncAPOInWorld(ws, apo);
    }
    public override bool ShouldSyncAPOInRoom(RoomSession rs, AbstractPhysicalObject apo)
    {
        //if (apo is AbstractCreature ac)
            //return ShouldRealizeCreature(ac) && base.ShouldSyncAPOInRoom(rs, apo);

        return base.ShouldSyncAPOInRoom(rs, apo);
    }
    //don't sync pearls that aren't team pearls
    public override bool ShouldRegisterAPO(OnlineResource resource, AbstractPhysicalObject apo)
    {
        if (apo.type == AbstractPhysicalObject.AbstractObjectType.DataPearl
            && apo is DataPearl.AbstractDataPearl ap)
        {
            int idx = PearlIdxToTeam(ap.dataPearlType.index);
            if (idx < 0 || idx >= TeamPearls.Length || (TeamPearls[idx] != null && TeamPearls[idx].apo != apo)) //this team pearl has already been registered!
                return false;
        }
        return base.ShouldRegisterAPO(resource, apo);
    }

    //Check if someone else has already realized this creature. If so, skip realizing/registering it for now.
    public bool ShouldRealizeCreature(AbstractCreature ac)
    {
        return true;
        if (ac.state is PlayerState) return true; //realize players, duh
        return !OnlineManager.recentEntities.Values.Any(
            ent => ent is OnlineCreature oc && oc.realized //find a realized creature
            && (oc.abstractCreature.spawnDen == ac.spawnDen || oc.abstractCreature.ID.spawner == ac.ID.spawner)
            //&& oc.TryGetData(out CreatureSpawnData data) && (data.spawner == ac.ID.spawner || data.spawnDen == ac.spawnDen) //that already occupies this spawner
            //&& oc.TryGetData(out CreatureSpawnData data) && (data.spawner == ac.ID.spawner) //that already occupies this spawner
            );
    }

    public override void FilterItems(Room room)
    {
        base.FilterItems(room);

        foreach (var list in room.physicalObjects)
        {
            foreach (var item in list)
            {
                //remove any DataPearls that aren't in the TeamPearls list
                if (item is DataPearl && !TeamPearls.Any(pearl => pearl.apo == item.abstractPhysicalObject))
                {
                    var apo = item.abstractPhysicalObject;
                    apo.GetOnlineObject()?.Deregister();
                    apo.Abstractize(new());
                    apo.Destroy();
                }
            }
        }
    }

    public override void ResourceAvailable(OnlineResource onlineResource)
    {
        base.ResourceAvailable(onlineResource);
        if (onlineResource is Lobby)
            onlineResource.AddData(new CTPLobbyData());
        else if (onlineResource is WorldSession)
            onlineResource.AddData(new PearlTrackingData());
    }

    public override void PlayerLeftLobby(OnlinePlayer player)
    {
        base.PlayerLeftLobby(player);

        if (PlayerTeams.ContainsKey(player)) PlayerTeams.Remove(player);
    }
    public override void NewPlayerInLobby(OnlinePlayer player)
    {
        base.NewPlayerInLobby(player);

        if (gameSetup) AssignPlayerTeams(); //immediately give the player a team if the game is already in progress
    }

    public override void PreGameStart()
    {
        base.PreGameStart();

        if (!gameSetup)
        {
            if (lobby.isOwner)
                SetupTeams();
            ClientSetup();
        }
    }


    /// <summary>
    /// Safely determines whether a CTP gamemode is the currently active game mode.
    /// </summary>
    /// <param name="gamemode">The CTPGameMode if active; null otherwise.</param>
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
