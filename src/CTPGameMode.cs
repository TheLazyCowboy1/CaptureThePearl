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
    public const string GameModeDescription = "Team up with other players to steal the opposing team's pearls and bring them\nback to your home shelter!";

    //Synced variables
    public Dictionary<OnlinePlayer, byte> PlayerTeams = new();

    public string[] TeamShelters = new string[0];
    public int[] TeamPoints = new int[0];

    //(settings)
    public byte NumberOfTeams = 2; //for now, make it always 2
    public int TimerLength = 10; //length in minutes
    public bool SpawnCreatures = true;

    //Non-synced variables
    public OnlinePhysicalObject[] teamPearls = new OnlinePhysicalObject[0];
    public PearlIndicator[] pearlIndicators = new PearlIndicator[0];

    public bool gameSetup = false;
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
        RainMeadow.RainMeadow.Debug("[CTP]: Sanitizing gamemode");
        PlayerTeams.Clear();
        TeamShelters = new string[0];
        TeamPoints = new int[0];
        teamPearls = new OnlinePhysicalObject[0];
        for (int i = 0; i < pearlIndicators.Length; i++) RemoveIndicator(i);
        pearlIndicators = new PearlIndicator[0];
        gameSetup = false;
        hasSpawnedIn = false;
    }

    public void ClientSetup()
    {
        //apply game hooks!
        CTPGameHooks.ApplyHooks();

        teamPearls = new OnlinePhysicalObject[NumberOfTeams];
        pearlIndicators = new PearlIndicator[NumberOfTeams];

        gameSetup = true;
    }

    public void SetupTeams()
    {
        RainMeadow.RainMeadow.Debug("[CTP]: Setting up teams!");

        //get team shelters
        List<string> tempShelters = new(NumberOfTeams);
        for (int i = 0; i < NumberOfTeams; i++)
            tempShelters.Add(Helpers.RandomShelterChooser.GetRespawnShelter(region, currentCampaign, tempShelters.ToArray(), Plugin.Options.TeamShelterCloseness.Value));
        TeamShelters = tempShelters.ToArray();

        TeamPoints = new int[NumberOfTeams];

        //assign player teams
        AssignPlayerTeams();
    }

    public void AssignPlayerTeams()
    {
        //remove players that have quit
        var playerKeys = PlayerTeams.Keys;
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

        ClientGameTick(); //done here as well as update, just to be extra safe

        if (gameSetup && lobby.isOwner) //isOwner should always be true
        {
            AssignPlayerTeams(); //add other players to the team if they join in; ensures team list is up to date
            
            //ScorePoints(); //instead handled by SearchForPearls
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
        }
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
        pearlIndicators[team] = new PearlIndicator(hud, cam, opo.apo);
        hud.AddPart(pearlIndicators[team]);
    }
    public void RemoveIndicator(int team)
    {
        var ind = pearlIndicators[team];
        if (ind != null)
        {
            ind.slatedForDeletion = true;
        }
        pearlIndicators[team] = null;
    }

    private static int TeamToPearlIdx(byte team) => team + 2;
    public static byte PearlIdxToTeam(int idx) => (byte)(idx - 2);

    //This is a poor place to have this function; or at least pearls should use it to decide their color!
    public Color GetTeamColor(int team)
    {
        float h = (float)team / (float)NumberOfTeams;
        if (NumberOfTeams == 2 && team == 1) h = 2f / 3f; //set it to fully blue; cyan doesn't look as nice
        return Color.HSVToRGB(h, 1f, 0.9f);
    }
    /// <summary>
    /// Lightens the color given in a standardized manner.
    /// </summary>
    /// <param name="color">The team color; accessible through gamemode.GetTeamColor().</param>
    /// <returns>The lightened team color.</returns>
    public static Color LigherTeamColor(Color color)
    {
        Color.RGBToHSV(color, out float h, out float s, out float v);
        s *= 0.8f;
        v = 1f;
        return Color.HSVToRGB(h, s, v);
    }

    public void SearchForPearls()
    {
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
                            DestroyPearl(ref teamPearls[i]); //if it's in someone else's shelter... bye-bye!
                            RemoveIndicator(i);
                            TeamScored(idx);
                            //tell everyone that a point was scored!
                            foreach (var p in OnlineManager.players)
                            {
                                if (!p.isMe) p.InvokeOnceRPC(CTPRPCs.PointScored, (byte)idx);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
        }

        var player = GetMyPlayer();
        if (player == null) return;

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

    public void TeamScored(int team)
    {
        //grant points
        TeamPoints[team]++;
        RainMeadow.RainMeadow.Debug($"[CTP]: Team {team} scored a point! Points: {TeamPoints[team]}");
        TeamScoredMessage(team);
    }

    private int PearlInEnemyShelter(OnlinePhysicalObject opo, int team)
    {
        int idx = Array.IndexOf(TeamShelters, opo.apo.Room.name);
        if (idx >= 0 && idx != team) //it's in a team den, but not its own team den!
            return idx;
        return -1;
    }
    private static void DestroyPearl(ref OnlinePhysicalObject opo)
    {
        RainMeadow.RainMeadow.Debug($"[CTP]: Destroying pearl {opo}");
        opo.apo.realizedObject?.AllGraspsLetGoOfThisObject(true);
        opo.apo.Abstractize(opo.apo.pos);
        opo.apo.Destroy();
        //opo.Deregister();
        opo.Deactivated(opo.primaryResource);
        opo = null;
    }

    public void SpawnPearls()
    {
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
                    if (room.GetResource().owner != OnlineManager.mePlayer)
                        continue; //don't add the pearl if I don't own it!!

                    RainMeadow.RainMeadow.Debug($"[CTP]: Adding pearl for team {i}");
                    var abPearl = new DataPearl.AbstractDataPearl(player.world, AbstractPhysicalObject.AbstractObjectType.DataPearl, null,
                        new WorldCoordinate(room.index, room.size.x / 2, room.size.y / 2, 0), player.world.game.GetNewID(), room.index,
                        -1, null, new(DataPearl.AbstractDataPearl.DataPearlType.values.GetEntry(TeamToPearlIdx(i)), false));

                    room.AddEntity(abPearl);
                    abPearl.RealizeInRoom();
                    abPearl.realizedObject.firstChunk.pos = realPlayer.firstChunk.pos; //spawn at my location, just to be safe

                    teamPearls[i] = abPearl.GetOnlineObject();
                    AddIndicator(teamPearls[i], i);
                }
                catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
            }
        }
    }

    public void RepositionPearls()
    {
        foreach (var pearl in teamPearls)
        {
            try
            {
                //If the pearl is mine, yet destroyed //and in the same room
                if (pearl != null && pearl.isMine)
                {
                    if (pearl.apo?.realizedObject?.grabbedBy?.Count > 0 && !pearl.apo.realizedObject.grabbedBy[0].grabber.dead)
                        continue; //don't reposition if it's in something's hand

                    var tile = pearl?.apo?.realizedObject?.room?.GetTile(pearl.apo.pos);
                    if (pearl.apo.InDen || pearl.apo.realizedObject == null || pearl.apo.realizedObject.firstChunk.pos.y < 0 || tile == null || tile.Solid)
                    //&& pearl.apo.Room.index == player.Room.index)
                    {
                        var player = GetMyPlayer();
                        if (player?.realizedObject == null || pearl.apo.Room.index != player.Room.index)
                            continue; //don't try repositioning it if I'm not there to grab it

                        RainMeadow.RainMeadow.Debug($"[CTP]: Attempting to reposition pearl!");
                        //respawn the pearl at my location; it belongs to me!!
                        pearl.apo.pos = player.pos;
                        pearl.apo.InDen = false; //if a creature took it, move it out of the den
                        if (pearl.apo.realizedObject == null)
                            pearl.apo.RealizeInRoom();
                        else
                            pearl.apo.realizedObject.AllGraspsLetGoOfThisObject(true);
                        pearl.apo.realizedObject.firstChunk.pos = player.realizedObject.firstChunk.pos; //set it to my location
                    }
                }
            }
            catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
        }
    }
    private AbstractPhysicalObject GetMyPlayer()
    {
        //return (lobby.playerAvatars.Find(kvp => kvp.Key == OnlineManager.mePlayer).Value.FindEntity() as OnlinePhysicalObject).apo;
        return avatars.Find(c => c.isMine)?.apo;
    }

    public void TeamScoredMessage(int team)
    {
        RainMeadow.RainMeadow.Debug($"[CTP] Sending team {team} scored message");
        ChatLogManager.LogMessage("", $"Team {team} has scored!");
        string scoreString = "";
        for (byte i = 0; i < NumberOfTeams; i++)
        {
            if (i > 0) scoreString += ", ";
            scoreString += "Team" + i + ": " + TeamPoints[i];
        }
        ChatLogManager.LogMessage("", "  Scores: " + scoreString);
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

    //Not working well at the moment... trying to get players on other teams to have their team color
    public override void Customize(Creature creature, OnlineCreature oc)
    {
        base.Customize(creature, oc);

        //Set players team colors, if on other team
        if (oc.TryGetData<SlugcatCustomization>(out var data))
        {
            if (PlayerTeams.TryGetValue(OnlineManager.mePlayer, out byte myTeam) && PlayerTeams.TryGetValue(oc.owner, out byte hisTeam))
            {
                if (myTeam != hisTeam)
                {
                    RainMeadow.RainMeadow.Debug($"[CTP]: Customizing team color for player {oc.owner}");
                    (RainMeadow.RainMeadow.creatureCustomizations.GetValue(creature, (c) => data) as SlugcatCustomization)
                        .bodyColor = GetTeamColor(PlayerTeams[oc.owner]);
                }
                else RainMeadow.RainMeadow.Debug($"[CTP]: Player on same team: {oc.owner}");
            }
            else
                RainMeadow.RainMeadow.Error($"[CTP]: Could not find player {oc.owner} in team list!!");
        }
        else if (creature is Player)
            RainMeadow.RainMeadow.Error($"Couldn't find customization data for player {creature}");
    }

    public override void PreGameStart()
    {
        base.PreGameStart();

        if (!gameSetup)
        {
            ClientSetup();
            if (lobby.isOwner)
                SetupTeams();
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
