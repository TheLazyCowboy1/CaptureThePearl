using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RainMeadow;
using RWCustom;
using UnityEngine;

namespace CaptureThePearl;

public class CTPGameMode : StoryGameMode
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
    public OnlinePhysicalObject[] teamPearls = new OnlinePhysicalObject[0];
    public PearlIndicator[] pearlIndicators = new PearlIndicator[0];
    public long[] pearlUntouchedTicks = new long[0];

    public bool gameSetup = false;
    public bool hasSpawnedIn = false; //this applies only to "myself", to decide whether to spawn in the team shelter or somewhere random

    public bool otherTeamsMuted = false; //whether players on other teams should be CURRENTLY muted

    public const float UNTENDED_PEARL_RESPAWN_TIME = 5f; //5 seconds of map open

    public CTPGameMode(Lobby lobby) : base(lobby)
    {
    }

    public void SanitizeCTP()
    {
        RainMeadow.RainMeadow.Debug("[CTP]: Sanitizing gamemode");

        PlayerTeams.Clear();
        TeamShelters = new string[0];
        TeamPoints = new int[0];
        teamPearls = new OnlinePhysicalObject[0];
        for (int i = 0; i < pearlIndicators.Length; i++) RemoveIndicator(i);
        pearlIndicators = new PearlIndicator[0];
        pearlUntouchedTicks = new long[0];
        gameSetup = false;
        hasSpawnedIn = false;
    }

    public void ClientSetup()
    {
        //apply game hooks!
        CTPGameHooks.ApplyHooks();

        teamPearls = new OnlinePhysicalObject[NumberOfTeams];
        pearlIndicators = new PearlIndicator[NumberOfTeams];
        pearlUntouchedTicks = new long[NumberOfTeams];

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

        SearchForPearls();
        SpawnPearls();
        RepositionPearls();

        for (int i = 0; i < NumberOfTeams; i++)
        {
            if (teamPearls[i] != null && pearlIndicators[i] == null)
                AddIndicator(teamPearls[i], i);
            else if (pearlIndicators[i] != null && (teamPearls[i] == null || pearlIndicators[i].apo != teamPearls[i].apo))
                RemoveIndicator(i);
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

    public void AddIndicator(OnlinePhysicalObject opo, int team)
    {
        var cam = opo.apo.world?.game?.cameras[0];
        var hud = cam?.hud;
        if (hud == null)
        {
            RainMeadow.RainMeadow.Error($"[CTP]: Couldn't find HUD for game containing {opo}");
            return;
        }
        if (pearlIndicators[team] != null) RemoveIndicator(team);
        pearlIndicators[team] = new PearlIndicator(hud, cam, opo.apo);
        hud.AddPart(pearlIndicators[team]);

        pearlUntouchedTicks[team] = 0;
    }
    public void RemoveIndicator(int team)
    {
        var ind = pearlIndicators[team];
        if (ind != null)
        {
            ind.slatedForDeletion = true;
        }
        pearlIndicators[team] = null;

        pearlUntouchedTicks[team] = 0;
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

    public void SearchForPearls()
    {
        if (spawnRequestPending) return; //I'm trying to spawn a pearl; ensure I don't discover it accidentally

        var player = GetMyPlayer();
        if (player == null) return;

        //remove pearls that can no longer be found
        for (int i = 0; i < NumberOfTeams; i++)
        {
            try
            {
                if (teamPearls[i] != null)
                {
                    //I don't think this case can ever happen...?
                    if (teamPearls[i].apo == null || !OnlineManager.recentEntities.ContainsValue(teamPearls[i])) //if the object isn't there, ensure it's marked as null
                    {
                        RainMeadow.RainMeadow.Debug($"[CTP]: Forgetting pearl for team {i}");
                        teamPearls[i].Deregister();
                        teamPearls[i] = null;
                        RemoveIndicator(i);
                    }
                    else if (teamPearls[i].isMine) //also remove pearls that are in an enemy den
                    {
                        int idx = PearlInEnemyShelter(teamPearls[i], i);
                        if (idx >= 0)
                        {
                            if (!lobby.isOwner) //inform host
                                lobby.owner.InvokeRPC(CTPRPCs.DestroyTeamPearl, teamPearls[i], (byte)i);
                            DestroyPearl(ref teamPearls[i]); //if it's in someone else's shelter... bye-bye!
                            RemoveIndicator(i);
                            TeamScored(idx, i);
                            //tell everyone that a point was scored!
                            foreach (var p in OnlineManager.players)
                            {
                                if (!p.isMe) p.InvokeOnceRPC(CTPRPCs.PointScored, (byte)idx, (byte)i);
                            }

                            //respawn the pearl
                            //SpawnPearls(true);
                        }
                        else //everything is fine with the pearl
                        {
                            //manage pearl timer
                            if (teamPearls[i].apo.realizedObject == null //if it's not realized
                                || teamPearls[i].apo.realizedObject.grabbedBy.Count > 0) //or is held by something
                                pearlUntouchedTicks[i] = 0; //reset timer
                            //else if (player.realizedObject != null && player.realizedObject.firstChunk.vel.sqrMagnitude <= 2f)
                            else if (player.realizedObject is Player realPlayer && realPlayer.input[0].mp)
                                pearlUntouchedTicks[i]++; //increment timer if the player's map is open
                        }
                    }
                    else
                        pearlUntouchedTicks[i] = 0;
                }
            }
            catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
        }

        //search for pearls within the world
        for (int i = 0; i < NumberOfTeams; i++)
        {
            if (teamPearls[i] == null)
            {
                //search through all active entities in the world for the pearl
                foreach (var entity in OnlineManager.recentEntities.Values)
                {
                    if (entity is OnlinePhysicalObject opo && opo.apo.type == AbstractPhysicalObject.AbstractObjectType.DataPearl
                        && PearlIdxToTeam((opo.apo as DataPearl.AbstractDataPearl).dataPearlType.index) == i)
                    {
                        RainMeadow.RainMeadow.Debug($"[CTP]: Found pearl for team {i}: {opo}");
                        teamPearls[i] = opo;
                        AddIndicator(opo, i);
                        break;
                    }
                }
            }
        }
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

        if (teamPearls[loser] != null)
            DestroyPearl(ref teamPearls[loser]);
    }

    private int PearlInEnemyShelter(OnlinePhysicalObject opo, int team)
    {
        int idx = Array.IndexOf(TeamShelters, opo.apo.Room.name);
        if (idx >= 0 && idx != team) //it's in a team den, but not its own team den!
            return idx;
        return -1;
    }
    public static void DestroyPearl(ref OnlinePhysicalObject opo)
    {
        RainMeadow.RainMeadow.Debug($"[CTP]: Destroying pearl {opo}");
        opo.apo.realizedObject?.AllGraspsLetGoOfThisObject(true);
        opo.apo.Abstractize(opo.apo.pos);
        opo.apo.Room?.RemoveEntity(opo.apo);
        opo.apo.Destroy();
        //opo.Deregister();
        opo.Deactivated(opo.primaryResource);
        opo = null;
    }
    public static void DestroyPearl(ref DataPearl.AbstractDataPearl apo)
    {
        RainMeadow.RainMeadow.Debug($"[CTP]: Destroying local pearl {apo}");
        apo.realizedObject?.AllGraspsLetGoOfThisObject(true);
        apo.Abstractize(apo.pos);
        apo.Room?.RemoveEntity(apo);
        apo.Destroy();
        apo = null;
    }

    private bool spawnRequestPending = false;
    public void SpawnPearls(bool spawnInOnlineRooms = false)
    {
        if (spawnRequestPending) return;

        //spawn pearls if they don't exist
        for (byte i = 0; i < NumberOfTeams; i++)
        {
            var pearl = teamPearls[i];
            if (pearl == null)
            {
                //RainMeadow.RainMeadow.Debug($"[CTP]: Spawn pearl {i}?");
                //try spawning the pearl
                try
                {
                    var player = GetMyPlayer();
                    if (player?.realizedObject is not Player realPlayer)
                        continue; //don't spawn it in if I'm not spawned in yet

                    var room = player.world.GetAbstractRoom(TeamShelters[i]);
                    if (!spawnInOnlineRooms && room.GetResource().owner != OnlineManager.mePlayer)
                        continue; //don't add the pearl if I don't own it!!

                    RainMeadow.RainMeadow.Debug($"[CTP]: Adding pearl for team {i}");
                    var abPearl = new DataPearl.AbstractDataPearl(player.world, AbstractPhysicalObject.AbstractObjectType.DataPearl, null,
                        new WorldCoordinate(room.index, room.size.x / 2, room.size.y / 2, 0), player.world.game.GetNewID(), room.index,
                        -1, null, new(DataPearl.AbstractDataPearl.DataPearlType.values.GetEntry(TeamToPearlIdx(i)), false));

                    room.AddEntity(abPearl);
                    abPearl.RealizeInRoom();
                    if (abPearl?.realizedObject == null) continue; //it was prevented from realizing, apparently!
                    abPearl.realizedObject.firstChunk.pos = realPlayer.firstChunk.pos; //spawn at my location, just to be safe

                    if (lobby.isOwner)
                    {
                        teamPearls[i] = abPearl.GetOnlineObject();
                        AddIndicator(teamPearls[i], i);
                    }
                    else //request permission from the host to register the team pearl
                    {
                        spawnRequestPending = true;
                        byte team = i;
                        RainMeadow.RainMeadow.Debug($"[CTP]: Requesting to register pearl {abPearl} for team {team}.");
                        lobby.owner.InvokeRPC(CTPRPCs.RegisterTeamPearl, abPearl.GetOnlineObject(), team)
                            .Then(result => //executes once the result is received
                            {
                                RainMeadow.RainMeadow.Debug("[CTP] Register request answered...");
                                try
                                {
                                    if (result is GenericResult.Ok && abPearl != null)
                                    {
                                        teamPearls[team] = abPearl.GetOnlineObject();
                                        AddIndicator(teamPearls[team], team);
                                        RainMeadow.RainMeadow.Debug($"[CTP]: Request accepted for registering pearl {abPearl} for team {team}.");
                                    }
                                    else if (abPearl != null) //rejected? then destroy it
                                    {
                                        var opo = abPearl.GetOnlineObject();
                                        if (opo == null)
                                            DestroyPearl(ref abPearl);
                                        else
                                            DestroyPearl(ref opo);
                                        RainMeadow.RainMeadow.Debug($"[CTP]: Request denied for registering pearl {abPearl} for team {team}.");
                                    }
                                    else
                                        RainMeadow.RainMeadow.Debug("The pearl I'm attempting to register is null.");
                                }
                                catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
                                spawnRequestPending = false;
                            });
                    }
                }
                catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
            }
        }
    }

    public void RepositionPearls()
    {
        //foreach (var pearl in teamPearls)
        for (int i = 0; i < teamPearls.Length; i++)
        {
            var pearl = teamPearls[i];
            try
            {
                //If the pearl is mine, yet destroyed //and in the same room
                if (pearl != null && pearl.isMine)
                {
                    if (pearl.apo?.realizedObject?.grabbedBy?.Count > 0 && !pearl.apo.realizedObject.grabbedBy[0].grabber.dead && pearl.apo.realizedObject.grabbedBy[0].grabber is Player)
                        continue; //don't reposition if it's in a PLAYER's hand

                    var tile = pearl?.apo?.realizedObject?.room?.GetTile(pearl.apo.pos);
                    if (pearl.apo.InDen || pearl.apo.realizedObject == null //null checks
                        || pearlUntouchedTicks[i] > UNTENDED_PEARL_RESPAWN_TIME * 40f //pearl hasn't been touched in 5 seconds
                        || pearl.apo.pos.Tile.y < 0 || pearl.apo.pos.Tile.x < 0 //min bounds checks
                        || pearl.apo.pos.Tile.y > pearl.apo.Room.size.y || pearl.apo.pos.Tile.x > pearl.apo.Room.size.x //max bounds checks
                        || tile == null || tile.Solid || tile.wormGrass) //tile type checks
                    //&& pearl.apo.Room.index == player.Room.index)
                    {
                        pearl.apo.InDen = false; //if a creature took it, move it out of the den

                        var player = GetMyPlayer();
                        if (player == null || player.realizedObject == null || player.state.dead)
                        {
                            //I am no longer responsible to manage this pearl; give management of it to someone else
                            //pearl.Release();
                            pearl.apo.world.game.GoToDeathScreen(); //just force the player to go to the death screen. Cheap solution, but works
                            continue;
                        }
                        if (pearl.apo.Room.index != player.Room.index)
                            continue; //don't try repositioning it if I'm not there to grab it

                        RainMeadow.RainMeadow.Debug($"[CTP]: Attempting to reposition pearl!");

                        //respawn the pearl at my location; it belongs to me!!
                        pearl.apo.pos = player.pos;
                        if (pearl.apo.realizedObject == null)
                            pearl.apo.RealizeInRoom();

                        pearl.apo.realizedObject.AllGraspsLetGoOfThisObject(true);
                        pearl.apo.realizedObject.firstChunk.pos = player.realizedObject.firstChunk.pos; //set it to my location
                        pearl.apo.realizedObject.firstChunk.vel = new(0, 0);

                        pearlUntouchedTicks[i] = 0; //reset pearl timer
                    }
                }
            }
            catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
        }
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
            if (idx < 0 || idx >= teamPearls.Length || (teamPearls[idx] != null && teamPearls[idx].apo != apo)) //this team pearl has already been registered!
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
                if (item is DataPearl && !teamPearls.Any(pearl => pearl.apo == item.abstractPhysicalObject))
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
