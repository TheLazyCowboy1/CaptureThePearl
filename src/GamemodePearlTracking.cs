using RainMeadow;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CaptureThePearl;

public partial class CTPGameMode
{
    //public TrackedPearl[] TrackedPearls = new TrackedPearl[0];
    public OnlinePhysicalObject[] TeamPearls = new OnlinePhysicalObject[0];
    public OnlinePlayer pearlTrackerOwner = null;

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
        pearlTrackerOwner = null;
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

    //only called by WorldSession owner
    public void SetupTrackedPearls()
    {
        if (TeamPearls.Length != TeamShelters.Length)
        {
            TeamPearls = new OnlinePhysicalObject[TeamShelters.Length];
            //TrackedPearls = new TrackedPearl[TeamShelters.Length];
            //for (byte i = 0; i < TeamShelters.Length; i++)
                //TrackedPearls[i] = new(TeamShelters[i], i);
        }

        var player = GetMyPlayer();
        if (player != null)
        {
            for (byte i = 0; i < TeamPearls.Length; i++)
            {
                if (TeamPearls[i] == null)
                {
                    try
                    {
                        SpawnPearl(i, player.world);
                    }
                    catch (Exception ex)
                    {
                        RainMeadow.RainMeadow.Error($"[CTP]: Error spawning pearl for team {i}:");
                        RainMeadow.RainMeadow.Error(ex);
                    }
                }
            }
        }
    }

    public void TestForScore()
    {
        if (blockedScores.Length != TeamShelters.Length)
        {
            RainMeadow.RainMeadow.Error("[CTP]: Array length mismatches! (TestForScore)");
            return;
        }

        for (byte i = 0; i < blockedScores.Length; i++)
        {
            if (blockedScores[i] && TeamPearls[i] != null && TeamPearls[i].apo.realizedObject != null)
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
                if (TeamPearls[i] != null && TeamPearls[i].isMine)
                {
                    int idx = PearlInEnemyShelter(TeamPearls[i], i);
                    if (idx >= 0 && TeamPearls[i].apo.realizedObject != null && !blockedScores[i])
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
                        RespawnTeamPearl(i);
                    }
                    else if (pearlUntouchedTicks[i] >= 0) //everything is fine with the pearl
                    {
                        var player = GetMyPlayer();
                        //manage pearl timer
                        if (UNTENDED_PEARL_RESPAWN_TIME <= 0 //if the mechanic is disabled
                            && (player == null || TeamPearls[i].apo.pos.room != player.pos.room //not in the same room
                            || (TeamPearls[i].apo.realizedObject != null && TeamPearls[i].apo.realizedObject.grabbedBy.Count > 0))) //or is held by something
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
                }
                else if (pearlUntouchedTicks[i] > 0) pearlUntouchedTicks[i] = 0;
            }
            catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
        }
    }

    //Run by world-owner now, not by individual players
    public void SpawnPearl(byte team, World world)
    {
        var room = world.GetAbstractRoom(TeamShelters[team]);
        var abPearl = new DataPearl.AbstractDataPearl(world, AbstractPhysicalObject.AbstractObjectType.DataPearl, null,
            PearlSpawnCoord(room), world.game.GetNewID(), room.index,
            -1, null, new(DataPearl.AbstractDataPearl.DataPearlType.values.GetEntry(TeamToPearlIdx(team)), false));

        room.AddEntity(abPearl);
        //abPearl.RealizeInRoom(); //I'm not sure if this will work...

        //TrackedPearls[team].pearl = abPearl.GetOnlineObject();
        TeamPearls[team] = abPearl.GetOnlineObject();

        RainMeadow.RainMeadow.Debug($"[CTP]: Spawned pearl {TeamPearls[team]} for team {team} in {world.name}");
    }

    public void MovePearl(byte team, WorldCoordinate coord)
    {
        if (team >= 0 && team < TeamPearls.Length && TeamPearls[team] != null)
        {
            var pearl = TeamPearls[team].apo;TeamPearls[team].beingMoved = true; //this overrides Rain Meadow's sanity checks
            pearl.realizedObject?.AllGraspsLetGoOfThisObject(true);
            pearl.LoseAllStuckObjects();
            pearl.Abstractize(coord);
            RainMeadow.RainMeadow.Debug($"[CTP]: Moved team {team}'s pearl to {coord}");
            TeamPearls[team].beingMoved = false;
        }
    }
    public void RespawnTeamPearl(byte team)
    {
        var pearl = TeamPearls[team].apo;
        var coord = PearlSpawnCoord(pearl.world.GetAbstractRoom(TeamShelters[team]));

        MovePearl(team, coord);
        if (!TeamPearls[team].primaryResource.isOwner)
        {
            RainMeadow.RainMeadow.Debug($"[CTP]: Requesting world owner {TeamPearls[team].primaryResource.owner} to move the pearl to {coord}");
            TeamPearls[team].primaryResource.owner.InvokeRPC(CTPRPCs.MovePearl, team, coord);
        }
        RainMeadow.RainMeadow.Debug($"[CTP]: Respawned pearl {TeamPearls[team]} for team {team}");
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

        for (int i = 0; i < TeamPearls.Length; i++)
        {
            var pearl = TeamPearls[i];
            try
            {
                //If the pearl is mine, yet destroyed //and in the same room
                if (pearl != null && pearl.isMine)
                {
                    if (pearlUntouchedTicks[i] < 0 || pearlUntouchedTicks[i] > (UNTENDED_PEARL_RESPAWN_TIME + 1) * 200f)
                    {//forced abstraction
                        pearl.apo.Abstractize(pearl.apo.pos);
                        RainMeadow.RainMeadow.Debug($"[CTP] Manually abstractized newly acquired pearl {pearl} for team {i}");
                    }

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

                        pearl.apo.realizedObject.AllGraspsLetGoOfThisObject(true);
                        pearl.apo.realizedObject.firstChunk.pos = player.realizedObject.firstChunk.pos; //set it to my location
                        pearl.apo.realizedObject.firstChunk.vel = new(0, 0);

                        pearlUntouchedTicks[i] = 0; //reset pearl timer
                    }
                }
                //if the pearl exists only abstractly in my room, request it so I can realize it later
                else if (pearl != null && !pearl.realized && player?.realizedObject != null && pearl.apo.pos.room == player.pos.room)
                {
                    if (!pearl.isPending && !pearl.isTransfering)
                    {
                        RainMeadow.RainMeadow.Debug($"[CTP]: Requesting ownership of team {i}'s unmanaged pearl: {pearl}");
                        pearl.Request();
                        pearlUntouchedTicks[i] = -1; //represents that this needs to be abstractized when possible
                    }
                }
            }
            catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
        }
    }


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
