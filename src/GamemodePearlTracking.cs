using RainMeadow;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CaptureThePearl;

public partial class CTPGameMode
{
    //public TrackedPearl[] TrackedPearls = new TrackedPearl[0];
    public OnlinePhysicalObject[] TeamPearls = new OnlinePhysicalObject[0];
    //public OnlinePlayer pearlTrackerOwner = null;
    //public OnlinePlayer worldOwner => lobby.overworld?.owner ?? lobby.owner; //worldSession owner if available; lobby owner otherwise
    public OnlinePlayer WorldOwner => (lobby.overworld.worldSessions.TryGetValue(region, out WorldSession worldSes)
        ? worldSes.owner
        : lobby.overworld?.owner)
        ?? lobby.owner; //final fallback condition

    public PearlIndicator[] pearlIndicators = new PearlIndicator[0];
    public long[] pearlUntouchedTicks = new long[0];
    public bool[] blockedScores = new bool[0];

    //TODO: make this a config instead of a constant!
    public const float UNTENDED_PEARL_RESPAWN_TIME = 5f; //5 seconds of map open

    public void SanitizeTracker()
    {
        ClearIndicators();

        //TrackedPearls = new TrackedPearl[0];
        TeamPearls = new OnlinePhysicalObject[0];
        pearlIndicators = new PearlIndicator[0];
        //pearlTrackerOwner = null;
        pearlUntouchedTicks = new long[0];
        blockedScores = new bool[0];
    }
    public void SetupTrackerClientSide()
    {
        TeamPearls = new OnlinePhysicalObject[NumberOfTeams];
        pearlIndicators = new PearlIndicator[NumberOfTeams];

        pearlUntouchedTicks = new long[NumberOfTeams];
        blockedScores = new bool[NumberOfTeams];
    }

    public void ClearIndicators()
    {
        for (int i = 0; i < pearlIndicators.Length; i++) RemoveIndicator(i);
    }
    public void AddIndicator(OnlinePhysicalObject opo, int team)
    {
        var game = opo.apo.world?.game;
        if (game == null)
        {
            RainMeadow.RainMeadow.Error($"[CTP]: Couldn't find game containing {opo}");
            return;
        }
        var cam = game.cameras[0];
        var hud = cam?.hud;
        if (hud == null)
        {
            //RainMeadow.RainMeadow.Error($"[CTP]: Couldn't find HUD for game containing {opo}");
            return;
        }
        if (pearlIndicators[team] != null) RemoveIndicator(team);
        pearlIndicators[team] = new PearlIndicator(hud, cam, opo.apo);
        hud.AddPart(pearlIndicators[team]);
        RainMeadow.RainMeadow.Debug($"[CTP]: Added pearl indicator for {opo} for team {team}");
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

    public void ManageIndicators()
    {
        for (int i = 0; i < TeamPearls.Length; i++)
        {
            if (TeamPearls[i] != null && pearlIndicators[i] == null)
                AddIndicator(TeamPearls[i], i);
            else if (pearlIndicators[i] != null && (TeamPearls[i] == null || pearlIndicators[i].apo != TeamPearls[i].apo))
                RemoveIndicator(i);
            else if (TeamPearls[i] != null && pearlIndicators[i] != null)
            { //ensure the hud is actually loaded!
                var game = TeamPearls[i].apo.world?.game;
                if (game == null) ClearIndicators();
                else if (game.cameras[0]?.hud == null) ClearIndicators();
            }
        }
    }

    public override void GameShutDown(RainWorldGame game)
    {
        base.GameShutDown(game);

        ClearIndicators(); //the hud is being destroyed, so we'll have to re-add the indicators after respawning
    }

    /// <summary>
    /// HOST ONLY
    /// </summary>
    public void TestForScore()
    {
        if (blockedScores.Length != TeamShelters.Length)
        {
            RainMeadow.RainMeadow.Error("[CTP]: Array length mismatches! (TestForScore)");
            return;
        }

        for (byte i = 0; i < blockedScores.Length; i++)
        {
            if (blockedScores[i] && TeamPearls[i] != null)// && TeamPearls[i].apo.realizedObject != null)
            {
                int idx = PearlInEnemyShelter(TeamPearls[i], i);
                if (idx < 0)
                {//allow this to score again if it's realized not in an enemy shelter
                    blockedScores[i] = false;
                    RainMeadow.RainMeadow.Debug($"[CTP]: Pearl {TeamPearls[i]} for team {i} is now eligible for scoring.");
                }
            }
        }

        for (byte i = 0; i < TeamPearls.Length; i++)
        {
            try
            {
                if (TeamPearls[i] != null)// && TeamPearls[i].isMine)
                {
                    int idx = PearlInEnemyShelter(TeamPearls[i], i);
                    if (idx >= 0)
                    {
                        if (!blockedScores[i])// && TeamPearls[i].apo.realizedObject != null)
                        {
                            //if (pearlTrackerOwner != OnlineManager.mePlayer) //inform host
                            //pearlTrackerOwner?.InvokeRPC(CTPRPCs.DestroyTeamPearl, TeamPearls[i], (byte)i);
                            //DestroyPearl(ref TeamPearls[i]); //if it's in someone else's shelter... bye-bye!
                            //RemoveIndicator(i);
                            TeamScored(idx, i);
                            //tell everyone that a point was scored!
                            foreach (var p in OnlineManager.players)
                            {
                                if (!p.isMe) p.InvokeOnceRPC(CTPRPCs.PointScored, (byte)idx, (byte)i);
                            }
                            blockedScores[i] = true; //prevent this from happening multiple times before it gets moved

                            //respawn the pearl
                            //SpawnPearls(true);
                            //RespawnTeamPearl(i);
                        }

                        TryDestroyPearl(i, true); //always destroy when in enemy shelters
                    }
                }
            }
            catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
        }
    }

    /// <summary>
    /// HOST ONLY
    /// </summary>
    public void SearchForPearls()
    {
        try
        {
            //remove pearls that don't actually exist
            for (int i = 0; i < TeamPearls.Length; i++)
            {
                if (TeamPearls[i] == null) continue;
                var apo = TeamPearls[i].apo;
                if (apo == null || !apo.Room.entities.Concat(apo.Room.entitiesInDens).Contains(apo)) //apo is null or apo is not in its own room
                {
                    RainMeadow.RainMeadow.Debug($"[CTP]: The pearl for team {i} doesn't actually exist!");
                    TeamPearls[i] = null; //the pearl doesn't actually exist
                }
            }
        }
        catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }

        try
        {
            WorldSession ws = worldSession;
            if (ws == null || ws.worldLoader != null && !ws.worldLoader.Finished) //wait until world is actually loaded, stupid
                return;

            World world = ws.world;
            if (world == null || world.abstractRooms == null)
            {
                RainMeadow.RainMeadow.Error("[CTP]: World is null!");
                return;
            }

            //go through every room in the world (slow maybe? yeah; probably)
            foreach (AbstractRoom room in world.abstractRooms)
            {
                if (room == null) continue;
                //go through every entity in the room
                foreach (AbstractWorldEntity abEnt in room.entities.Concat(room.entitiesInDens))
                {
                    if (abEnt is not DataPearl.AbstractDataPearl abPearl) continue;
                    if (!CanBeTeamPearl(abPearl)) //not a team pearl = destroy
                    {
                        DestroyPearl(abPearl);
                        continue;
                    }
                    int team = PearlIdxToTeam(abPearl.dataPearlType.index);
                    if (TeamPearls[team] == null)
                    {
                        TeamPearls[team] = abPearl.GetOnlineObject(); //need a team pearl = use this one
                        RainMeadow.RainMeadow.Debug($"[CTP]: Found a new pearl for team {team} in room {room.name}!");
                    }
                }
            }

            //try to spawn pearls that are needed
            for (byte i = 0; i < TeamPearls.Length; i++)
            {
                if (TeamPearls[i] != null) continue;
                TrySpawnPearl(i, world, true);
            }
        }
        catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }

    }

    /// <summary>
    /// Host OR by request
    /// </summary>
    public void TrySpawnPearl(byte team, World world, bool amHost)
    {
        RainMeadow.RainMeadow.Debug($"[CTP]: Trying to spawn pearl for team {team}");
        AbstractRoom room = world.GetAbstractRoom(TeamShelters[team]);

        OnlinePlayer owner = room.GetResource()?.owner ?? world.GetResource()?.owner ?? lobby.owner; //RoomSession owner first; otherwise WorldSession owner

        if (owner.isMe)
            SpawnPearl(team, world);
        else if (owner == null)
            RainMeadow.RainMeadow.Error("[CTP]: NO WORLD OWNER OR LOBBY OWNER OR ANYTHING WHAT HOW DID THIS HAPPEN");
        else if (amHost)
            owner.InvokeRPC(CTPRPCs.TrySpawnPearl, team);
        else
            RainMeadow.RainMeadow.Error($"[CTP]: Requested to spawn a pearl for team {team}, but I don't own the room and I am not the host!");
    }

    //Run only by request of the host
    public void SpawnPearl(byte team, World world)
    {
        if (TeamPearls[team] != null)
        {
            RainMeadow.RainMeadow.Error($"[CTP]: Requested to spawn pearl for team {team}, but that pearl already exists!!!");
            return;
        }

        AbstractRoom room = world.GetAbstractRoom(TeamShelters[team]);
        DataPearl.AbstractDataPearl abPearl = new(world, AbstractPhysicalObject.AbstractObjectType.DataPearl, null,
            PearlSpawnCoord(room), world.game.GetNewID(), room.index,
            -1, null, new(DataPearl.AbstractDataPearl.DataPearlType.values.GetEntry(TeamToPearlIdx(team)), false));

        room.AddEntity(abPearl);
        abPearl.RealizeInRoom(); //I'm not sure if this will work...

        //TrackedPearls[team].pearl = abPearl.GetOnlineObject();
        TeamPearls[team] = abPearl.GetOnlineObject();

        RainMeadow.RainMeadow.Debug($"[CTP]: Spawned pearl {TeamPearls[team]} for team {team} in {world.name}");
    }

    /// <summary>
    /// HOST ONLY
    /// </summary>
    public void TryDestroyPearl(byte team, bool amHost)
    {
        if (TeamPearls[team] == null)
        {
            RainMeadow.RainMeadow.Error($"[CTP]: Cannot destroy the pearl for team {team} because it is already destroyed.");
            return;
        }
        RainMeadow.RainMeadow.Debug($"[CTP]: Host trying to destroy pearl for team {team}.");

        if (TeamPearls[team].isMine)
        {
            DestroyPearl(TeamPearls[team].apo as DataPearl.AbstractDataPearl);
            //TeamPearls[team].Deregister();
            //TeamPearls[team].Deactivated(TeamPearls[team].primaryResource); //used a while ago in my old implementation
            TeamPearls[team] = null;

            //RemoveIndicator(team); //just let ManageIndicators deal with it
        }
        else if (amHost)
        {
            TeamPearls[team].owner.InvokeRPC(TryDestroyPearl, team);
        }
        else
            RainMeadow.RainMeadow.Error($"[CTP]: Requested to destroy pearl for team {team}, but I don't own it and I am not the host!");
    }

    private static WorldCoordinate PearlSpawnCoord(AbstractRoom room) => new WorldCoordinate(room.index, room.size.x / 2, room.size.y / 2, 0);

    public bool CanBeTeamPearl(DataPearl.AbstractDataPearl abPearl)
    {
        int team = PearlIdxToTeam(abPearl.dataPearlType.index);
        return team >= 0 && team < TeamPearls.Length //in range
            && (TeamPearls[team] == null || TeamPearls[team].apo == abPearl);
    }
    private int PearlInEnemyShelter(OnlinePhysicalObject opo, int team)
    {
        int idx = Array.IndexOf(TeamShelters, opo.apo.Room.name);
        if (idx >= 0 && idx != team) //it's in a team den, but not its own team den!
            return idx;
        return -1;
    }

    public static void DestroyPearl(DataPearl.AbstractDataPearl apo)
    {
        RainMeadow.RainMeadow.Debug($"[CTP]: Destroying local pearl {apo}");
        apo.realizedObject?.AllGraspsLetGoOfThisObject(true);
        apo.Abstractize(apo.pos);
        apo.LoseAllStuckObjects();
        apo.Room?.RemoveEntity(apo);
        apo.slatedForDeletion = true;
        apo.Destroy();
    }

    public void RepositionPearls()
    {
        var player = GetMyPlayer();
        if (player == null)
        {
            ClearIndicators(); //just in case
            return;
        }

        //EnsureTrackerExists(player.world);

        for (int i = 0; i < TeamPearls.Length; i++)
        {
            var pearl = TeamPearls[i];
            try
            {
                //manage pearl untouched timer
                if (pearl == null)
                    pearlUntouchedTicks[i] = 0;
                else if (pearlUntouchedTicks[i] >= 0) //everything is fine with the pearl
                {
                    //manage pearl timer
                    if (UNTENDED_PEARL_RESPAWN_TIME <= 0 //if the mechanic is disabled
                        || player == null || TeamPearls[i].apo.pos.room != player.pos.room //not in the same room
                        || (TeamPearls[i].apo.realizedObject != null && TeamPearls[i].apo.realizedObject.grabbedBy.Count > 0)) //or is held by something
                        pearlUntouchedTicks[i] = 0; //reset timer
                                                    //else if (player.realizedObject != null && player.realizedObject.firstChunk.vel.sqrMagnitude <= 2f)
                    else
                    {
                        if (player.realizedObject is Player realPlayer && realPlayer.input[0].mp)
                            pearlUntouchedTicks[i] += 5; //increment timer faster if the player's map is open
                        else
                            pearlUntouchedTicks[i]++;
                    }
                }

            //If the pearl is mine, yet destroyed //and in the same room
            if (pearl != null && pearl.isMine)
                {
                    /*if (pearlUntouchedTicks[i] < 0 || pearlUntouchedTicks[i] > (UNTENDED_PEARL_RESPAWN_TIME + 1) * 200f)
                    {//forced abstraction
                        pearl.apo.realizedObject?.AllGraspsLetGoOfThisObject(true);
                        pearl.apo.Abstractize(pearl.apo.pos);
                        pearl.apo.LoseAllStuckObjects();
                        pearl.apo.Room?.RemoveEntity(pearl.apo);
                        RainMeadow.RainMeadow.Debug($"[CTP] Manually abstractized newly acquired pearl {pearl} for team {i}");
                    }*/

                    if (pearlIndicators[i] == null && player.realizedObject == null)
                    {
                        RainMeadow.RainMeadow.Debug("[CTP]: Not yet loaded in!");
                        continue; //if I don't yet have a pearl indicator for it, I must not be loaded in yet
                    }
                    if (pearl.realized && pearl.apo.realizedObject?.grabbedBy != null && pearl.apo.realizedObject.grabbedBy.Count > 0
                        && !pearl.apo.realizedObject.grabbedBy[0].grabber.dead && pearl.apo.realizedObject.grabbedBy[0].grabber is Player)
                        continue; //don't reposition if it's in a PLAYER's hand
                    if (pearl.apo.pos.room != player.pos.room)
                        continue; //don't try repositioning it if I'm not there to grab it

                    //check that the pearl is visible and in the room
                    /*if (pearl.apo.realizedObject != null && !pearl.apo.realizedObject.room.physicalObjects.Any(l => l.Contains(pearl.apo.realizedObject)))
                    {
                        pearl.apo.Abstractize(pearl.apo.pos);
                        RainMeadow.RainMeadow.Debug("[CTP]: Abstractized pearl because it was not in the room object list");
                    }*/

                    bool moveNeeded = true;
                    string moveReason = "";
                    if (!pearl.realized || pearl.apo.realizedObject == null) moveReason = "null";
                    else if (pearl.apo.InDen) moveReason = "in a den";
                    else if (UNTENDED_PEARL_RESPAWN_TIME > 0 && pearlUntouchedTicks[i] > UNTENDED_PEARL_RESPAWN_TIME * 200f) moveReason = "due for a manual reposition.";
                    else if (pearl.apo.pos.Tile.y < 0 || pearl.apo.pos.Tile.x < 0
                        || pearl.apo.pos.Tile.y > pearl.apo.Room.size.y || pearl.apo.pos.Tile.x > pearl.apo.Room.size.x)
                        moveReason = "out of bounds";
                    else
                    {
                        //check the tile
                        var tile = pearl?.apo?.realizedObject?.room?.GetTile(pearl.apo.pos);
                        if (tile == null) moveReason = "in a null tile";
                        else if (tile.Solid) moveReason = "in a wall";
                        else if (tile.wormGrass) moveReason = "in worm grass";
                        else moveNeeded = false; //passed all checks!
                    }

                        
                    if (moveNeeded)
                    {
                        //pearl.apo.InDen = false; //if a creature took it, move it out of the den
                        if (pearl.apo.InDen)
                        {
                            pearl.apo.Room.MoveEntityOutOfDen(pearl.apo);
                            RainMeadow.RainMeadow.Debug($"[CTP] Moving pearl {pearl} out of den");
                        }
                        pearl.apo.InDen = false; //just to doubly ensure it's not in a den

                        if (player.realizedObject == null || player.state.dead)
                        {
                            //I am no longer responsible to manage this pearl; give management of it to someone else
                            RainMeadow.RainMeadow.Debug($"[CTP]: Being banished to sleep screen due to being judged too irresponsible to maintain pearl {pearl} for team {i}");
                            pearl.apo.world.game.GoToDeathScreen(); //just force the player to go to the death screen. Cheap solution, but works
                            continue;
                        }
                        else if (player.realizedCreature.inShortcut)
                            continue; //don't reposition the pearl while I'm in a shortcut

                        RainMeadow.RainMeadow.Debug($"[CTP]: Attempting to reposition team {i}'s pearl {pearl} because it is {moveReason}");

                        //respawn the pearl at my location; it belongs to me!!
                        //pearl.apo.pos = player.pos;
                        pearl.apo.realizedObject.AllGraspsLetGoOfThisObject(true);
                        pearl.apo.LoseAllStuckObjects();
                        pearl.apo.Move(player.pos);
                        /*if (!pearl.apo.Room.entities.Contains(pearl.apo))
                        {
                            pearl.apo.Room.AddEntity(pearl.apo); //ensure the pearl is in the room's entities list
                            RainMeadow.RainMeadow.Debug("[CTP]: Added pearl to room entity list");
                        }*/
                        if (pearl.apo.realizedObject == null)
                        {
                            pearl.apo.RealizeInRoom();
                            RainMeadow.RainMeadow.Debug("[CTP]: Realized pearl");
                        }

                        pearl.apo.realizedObject.firstChunk.pos = player.realizedObject.firstChunk.pos; //set it to my location
                        pearl.apo.realizedObject.firstChunk.vel = new(0, 0);

                        pearlUntouchedTicks[i] = 0; //reset pearl timer
                    }
                }
                //if the pearl exists only abstractly in my room, request it so I can realize it later
                /*else if (pearl != null && !pearl.realized && player?.realizedObject != null && pearl.apo.pos.room == player.pos.room)
                {
                    if (!pearl.isPending && !pearl.isTransfering)
                    {
                        RainMeadow.RainMeadow.Debug($"[CTP]: Requesting ownership of team {i}'s unmanaged pearl: {pearl}");
                        pearl.Request();
                        pearlUntouchedTicks[i] = -1; //represents that this needs to be abstractized when possible
                    }
                }*/
            }
            catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
        }
    }

    public WorldSession worldSession => lobby.overworld.worldSessions.TryGetValue(region, out WorldSession worldSes) ? worldSes : null;


    //deprecated; archived:
    /*
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
                if (TeamPearls[i] != null)
                {
                    //I don't think this case can ever happen...?
                    if (TeamPearls[i].apo == null || !OnlineManager.recentEntities.ContainsValue(TeamPearls[i])) //if the object isn't there, ensure it's marked as null
                    {
                        RainMeadow.RainMeadow.Debug($"[CTP]: Forgetting pearl for team {i}");
                        TeamPearls[i].Deregister();
                        TeamPearls[i] = null;
                        RemoveIndicator(i);
                    }
                    else if (TeamPearls[i].isMine) //also remove pearls that are in an enemy den
                    {
                        int idx = PearlInEnemyShelter(TeamPearls[i], i);
                        if (idx >= 0)
                        {
                            if (pearlTrackerOwner != OnlineManager.mePlayer) //inform host
                                pearlTrackerOwner?.InvokeRPC(CTPRPCs.DestroyTeamPearl, TeamPearls[i], (byte)i);
                            DestroyPearl(ref TeamPearls[i]); //if it's in someone else's shelter... bye-bye!
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
                            if (TeamPearls[i].apo.realizedObject == null //if it's not realized
                                || TeamPearls[i].apo.realizedObject.grabbedBy.Count > 0) //or is held by something
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
            if (TeamPearls[i] == null)
            {
                //search through all active entities in the world for the pearl
                foreach (var entity in OnlineManager.recentEntities.Values)
                {
                    if (entity is OnlinePhysicalObject opo && opo.apo.type == AbstractPhysicalObject.AbstractObjectType.DataPearl
                        && PearlIdxToTeam((opo.apo as DataPearl.AbstractDataPearl).dataPearlType.index) == i)
                    {
                        RainMeadow.RainMeadow.Debug($"[CTP]: Found pearl for team {i}: {opo}");
                        TeamPearls[i] = opo;
                        AddIndicator(opo, i);
                        break;
                    }
                }
            }
        }
    }


    private bool spawnRequestPending = false;
    public void SpawnPearls(bool spawnInOnlineRooms = false)
    {
        if (spawnRequestPending) return;

        //spawn pearls if they don't exist
        for (byte i = 0; i < NumberOfTeams; i++)
        {
            var pearl = TeamPearls[i];
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
                    if (!spawnInOnlineRooms && room.GetResource()?.owner != OnlineManager.mePlayer)
                        continue; //don't add the pearl if I don't own it!!

                    RainMeadow.RainMeadow.Debug($"[CTP]: Adding pearl for team {i}");
                    var abPearl = new DataPearl.AbstractDataPearl(player.world, AbstractPhysicalObject.AbstractObjectType.DataPearl, null,
                        new WorldCoordinate(room.index, room.size.x / 2, room.size.y / 2, 0), player.world.game.GetNewID(), room.index,
                        -1, null, new(DataPearl.AbstractDataPearl.DataPearlType.values.GetEntry(TeamToPearlIdx(i)), false));

                    room.AddEntity(abPearl);
                    abPearl.RealizeInRoom();
                    if (abPearl?.realizedObject == null) continue; //it was prevented from realizing, apparently!
                    abPearl.realizedObject.firstChunk.pos = realPlayer.firstChunk.pos; //spawn at my location, just to be safe

                    if (pearlTrackerOwner == OnlineManager.mePlayer)
                    {
                        TeamPearls[i] = abPearl.GetOnlineObject();
                        AddIndicator(TeamPearls[i], i);
                    }
                    else if (pearlTrackerOwner != null) //request permission from the host to register the team pearl
                    {
                        spawnRequestPending = true;
                        byte team = i;
                        RainMeadow.RainMeadow.Debug($"[CTP]: Requesting to register pearl {abPearl} for team {team}.");
                        pearlTrackerOwner.InvokeRPC(CTPRPCs.RegisterTeamPearl, abPearl.GetOnlineObject(), team)
                            .Then(result => //executes once the result is received
                            {
                                RainMeadow.RainMeadow.Debug("[CTP] Register request answered...");
                                try
                                {
                                    if (result is GenericResult.Ok && abPearl != null)
                                    {
                                        TeamPearls[team] = abPearl.GetOnlineObject();
                                        AddIndicator(TeamPearls[team], team);
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
    */
}
