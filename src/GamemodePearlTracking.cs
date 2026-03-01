using RainMeadow;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CaptureThePearl;

public partial class CTPGameMode
{
    #region Setup
    public const int MAX_PROBLEMATIC_OPO_TIME = 40;

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
    public float untendedPearlRespawnTime = 5f; //5 seconds of map open

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
    #endregion

    #region Indicators
    public void ClearIndicators()
    {
        for (int i = 0; i < pearlIndicators.Length; i++) RemoveIndicator(i);
        RainMeadow.RainMeadow.Debug("[CTP]: Cleared all indicators");
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

        //pearlUntouchedTicks[team] = 0;
        RainMeadow.RainMeadow.Debug($"[CTP]: Removed pearl indicator for team {team}");
    }

    public void ManageIndicators()
    {
        for (int i = 0; i < TeamPearls.Length; i++)
        {
            if (TeamPearls[i] != null && pearlIndicators[i] == null)
                AddIndicator(TeamPearls[i], i);
            else if (pearlIndicators[i] != null && (TeamPearls[i] == null || pearlIndicators[i].apo != TeamPearls[i].apo || pearlIndicators[i].slatedForDeletion))
                RemoveIndicator(i);
            else if (TeamPearls[i] != null && pearlIndicators[i] != null)
            { //ensure the hud is actually loaded!
                var game = TeamPearls[i].apo?.world.game;
                if (game == null || game.cameras[0]?.hud == null)
                {
                    RainMeadow.RainMeadow.Error("[CTP]: Hud is not yet loaded; removing pearl indicators");
                    ClearIndicators();
                }
            }
        }
    }

    public override void GameShutDown(RainWorldGame game)
    {
        base.GameShutDown(game);

        ClearIndicators(); //the hud is being destroyed, so we'll have to re-add the indicators after respawning
    }
    #endregion

    #region PearlTesting
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

                        TryDestroyPearl(TeamPearls[i], true); //always destroy when in enemy shelters
                    }
                }
            }
            catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
        }
    }

    private Dictionary<AbstractPhysicalObject, int> SusAPOs = new(4);
    private Dictionary<OnlinePhysicalObject, int> SusOPOs = new(4);
    /// <summary>
    /// HOST ONLY
    /// </summary>
    public void SearchForPearls()
    {

        try
        {
            WorldSession ws = worldSession;
            if (ws == null || ws.activeEntities == null)
            {
                RainMeadow.RainMeadow.Debug("[CTP]: Can't search for pearls: Awaiting world session...");
                return;
            }
            if ((ws.worldLoader != null && !ws.worldLoader.Finished))
            { //wait until world is actually loaded, stupid
                RainMeadow.RainMeadow.Debug("[CTP]: Can't search for pearls: Awaiting world loader...");
                return;
            }

            World world = ws.world;
            if (world == null || world.abstractRooms == null)
            {
                RainMeadow.RainMeadow.Error("[CTP]: Can't search for pearls: World is null!");
                return;
            }

            var player = GetMyPlayer();
            if (player?.realizedObject == null || (player.realizedObject.room == null && !player.realizedCreature.inShortcut))
            {
                RainMeadow.RainMeadow.Error("[CTP]: Can't search for pearls: Player is null!");
                return;
            }

            //remove pearls that don't actually exist
            for (int i = 0; i < TeamPearls.Length; i++)
            {
                if (TeamPearls[i] == null) continue;
                if (!ApoActuallyExists(TeamPearls[i].apo, world)) //apo is null or apo is not in its own room
                {
                    RainMeadow.RainMeadow.Debug($"[CTP]: The pearl for team {i} doesn't actually exist!");
                    TeamPearls[i] = null; //the pearl doesn't actually exist
                }
            }

            //go through every room in the world (slow maybe? yeah; probably)
            List<AbstractPhysicalObject> newSusAPOs = new();
            foreach (AbstractRoom room in world.abstractRooms)
            {
                if (room == null) continue;
                //go through every entity in the room
                foreach (AbstractWorldEntity abEnt in room.entities.Concat(room.entitiesInDens))
                {
                    if (abEnt.slatedForDeletion || abEnt is not DataPearl.AbstractDataPearl abPearl) continue;
                    if (!CanBeTeamPearl(abPearl)) //not a team pearl = destroy
                    {
                        newSusAPOs.Add(abPearl);
                        if (SusAPOs.TryGetValue(abPearl, out int counter))
                        {
                            if (counter >= MAX_PROBLEMATIC_OPO_TIME)
                            {
                                RainMeadow.RainMeadow.Debug($"[CTP]: Trying to destroy local pearl {abPearl} in room {abPearl.Room?.name}!");
                                TryDestroyPearl(abPearl);
                                SusAPOs[abPearl] = 0;
                            }
                            else
                                SusAPOs[abPearl] = counter + 1;
                        }
                        else
                            SusAPOs.Add(abPearl, 0);
                        continue;
                    }
                    int team = PearlIdxToTeam(abPearl.dataPearlType.index);
                    if (TeamPearls[team] == null)
                    {
                        TeamPearls[team] = abPearl.GetOnlineObject(); //need a team pearl = use this one
                        RainMeadow.RainMeadow.Debug($"[CTP]: Found a new local pearl for team {team} in room {room.name}!");
                    }
                }
            }
            foreach (AbstractPhysicalObject apo in SusAPOs.Keys.Except(newSusAPOs).ToArray())
                SusAPOs.Remove(apo); //if the apo wasn't "sus" this time, remove it from the list

            //go through roomSession and worldSession entities
            List<OnlinePhysicalObject> newSusOPOs = new();
            //foreach (var ent in ws.roomSessions.Values.SelectMany(rs => rs?.activeEntities ?? new(0)).Concat(ws.activeEntities).ToArray()) //go through rs first, then ws
            foreach (var ent in OnlineManager.recentEntities.Values.ToArray()) //ToArray as a Lazy way to make it a distinct list
            {
                if (ent is OnlinePhysicalObject opo && !opo.isPending && opo.apo is DataPearl.AbstractDataPearl abPearl)
                {
                    if (SusAPOs.ContainsKey(abPearl))
                        continue; //if we're already sus of this, don't examine it

                    if (!CanBeTeamPearl(abPearl) || !ApoActuallyExists(abPearl, world)) //not a team pearl = destroy
                    {
                        newSusOPOs.Add(opo);
                        if (SusOPOs.TryGetValue(opo, out int counter))
                        {
                            if (counter >= MAX_PROBLEMATIC_OPO_TIME)
                            {
                                RainMeadow.RainMeadow.Debug($"[CTP]: Trying to destroy online pearl {opo} in room {abPearl.Room?.name}!");
                                TryDestroyPearl(opo, true);
                                SusOPOs[opo] = 0; //give it some time before attempting to destroy again
                            }
                            else
                                SusOPOs[opo] = counter + 1;
                        }
                        else
                            SusOPOs.Add(opo, 0);
                        continue;
                    }
                    int team = PearlIdxToTeam(abPearl.dataPearlType.index);
                    if (TeamPearls[team] == null)
                    {
                        TeamPearls[team] = opo; //need a team pearl = use this one
                        RainMeadow.RainMeadow.Debug($"[CTP]: Found a new online pearl for team {team} in room {abPearl.Room?.name}!");
                    }
                }
            }
            foreach (OnlinePhysicalObject opo in SusOPOs.Keys.Except(newSusOPOs).ToArray())
                SusOPOs.Remove(opo); //if the opo wasn't "sus" this time, remove it from the list
            newSusOPOs.Clear();

            //try to spawn pearls that are needed
            for (byte i = 0; i < TeamPearls.Length; i++)
            {
                if (TeamPearls[i] != null) continue;
                TrySpawnPearl(i, world, true);
            }


            TestForScore();
        }
        catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }

    }

    private static bool ApoActuallyExists(AbstractPhysicalObject apo, World world = null)
        => apo != null && !apo.slatedForDeletion && (world == null ? apo.world != null : apo.world == world) && apo.Room != null && (apo.Room.entities.Contains(apo) || apo.Room.entitiesInDens.Contains(apo));
    #endregion

    #region PearlSpawning
    /// <summary>
    /// Host OR by request
    /// </summary>
    public bool TrySpawnPearl(byte team, World world, bool amHost)
    {
        RainMeadow.RainMeadow.Debug($"[CTP]: Trying to spawn pearl for team {team}");
        AbstractRoom room = world.GetAbstractRoom(TeamShelters[team]);

        OnlinePlayer owner = room.GetResource()?.owner ?? world.GetResource()?.owner ?? lobby.owner; //RoomSession owner first; otherwise WorldSession owner

        if (owner.isMe)
        {
            SpawnPearl(team, world);
            return true;
        }
        else if (owner == null)
            RainMeadow.RainMeadow.Error("[CTP]: NO WORLD OWNER OR LOBBY OWNER OR ANYTHING WHAT HOW DID THIS HAPPEN");
        else if (amHost)
            owner.InvokeRPC(CTPRPCs.TrySpawnPearl, team);
        else
            RainMeadow.RainMeadow.Error($"[CTP]: Requested to spawn a pearl for team {team}, but I don't own the room and I am not the host!");
        return false;
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
        if (room.realizedRoom != null) //only force realize pearl if the room is already realized
            abPearl.RealizeInRoom(); //I'm not sure if this will work...

        //TrackedPearls[team].pearl = abPearl.GetOnlineObject();
        TeamPearls[team] = abPearl.GetOnlineObject();

        RainMeadow.RainMeadow.Debug($"[CTP]: Spawned pearl {TeamPearls[team]} for team {team} in {world.name}");
    }

    private static WorldCoordinate PearlSpawnCoord(AbstractRoom room) => new WorldCoordinate(room.index, room.size.x / 2, room.size.y / 2, 0);
    #endregion

    #region PearlDestroying
    /// <summary>
    /// HOST ONLY
    /// </summary>
    //public void TryDestroyPearl(byte team, bool amHost)
    public bool TryDestroyPearl(OnlinePhysicalObject opo, bool amHost)
    {
        if (opo == null)
        {
            RainMeadow.RainMeadow.Error($"[CTP]: Cannot destroy pearl {opo} because it does not exist");
            return false;
        }

        if (opo.isMine)
        {
            //DestroyPearl(opo.apo);
            //opo.Deactivated(opo.primaryResource);
            //opo.Release(); //I don't want management of this please
            if (opo.apo.realizedObject is PhysicalObject po)
            {
                foreach (Creature.Grasp grasp in po.grabbedBy.ToArray()) grasp.Release(); //because Meadow's implementation currently throws an error
            }
            opo.RemoveEntityFromGame(true); //THERE'S EXISTED A METHOD THIS WHOLE TIME AND I JUST DIDN'T KNOW ABOUT IT?????????
            opo.primaryResource.EntityLeftResource(opo); //properly remove entity from world resource (and all subresources)
            //opo.OnLeftResource(opo.primaryResource);
            //opo.Deactivated(opo.primaryResource); //just causes confusion between clients
            return true;
        }
        else if (amHost)
        {
            RainMeadow.RainMeadow.Debug($"[CTP]: Host trying to destroy pearl {opo}");
            opo.owner.InvokeRPC(CTPRPCs.TryDestroyPearl, opo)
                .Then(result =>
                {
                    if (result is GenericResult.Ok)
                    {
                        if (opo == null) return;

                        if (opo.apo != null) //backup
                            opo.apo.slatedForDeletion = true; //mark it as slated for deletion so that we don't consider it an active pearl anymore

                        OnlineResource r = opo.primaryResource;
                        if (r != null)
                        {
                            RainMeadow.RainMeadow.Error($"[CTP]: Allegedly destroyed pearl {opo} still in resource {r}!");
                            r.EntityLeftResource(opo); //get it out of here please please please
                        }
                        opo.Deregister(); //pretend it doesn't exist
                    }
                    else
                    {
                        opo.Request();
                        RainMeadow.RainMeadow.Debug($"[CTP]: Client failed to destroy pearl {opo}, so I'm requesting it to hopefully destroy it myself.");
                    }
                });
        }
        else
            RainMeadow.RainMeadow.Error($"[CTP]: Requested to destroy pearl {opo}, but I don't own it and I am not the host!");
        return false;
    }
    public void TryDestroyPearl(AbstractPhysicalObject pearl)
    {
        OnlinePhysicalObject opo = pearl.GetOnlineObject();
        if (opo == null)
            DestroyLocalPearl(pearl);
        else
            TryDestroyPearl(opo, true);
    }

    public static void DestroyLocalPearl(AbstractPhysicalObject apo)
    {
        RainMeadow.RainMeadow.Debug($"[CTP]: Destroying local pearl {apo}");
        if (apo.realizedObject != null)
        {
            apo.realizedObject.AllGraspsLetGoOfThisObject(true);
            apo.realizedObject.room?.CleanOutObjectNotInThisRoom(apo.realizedObject);
        }

        apo.Abstractize(apo.pos);
        apo.Destroy();
        apo.Room?.RemoveEntity(apo); //ensure it's not in the room
    }
    #endregion

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
                    if (untendedPearlRespawnTime <= 0 //if the mechanic is disabled
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
                    else if (untendedPearlRespawnTime > 0 && pearlUntouchedTicks[i] > untendedPearlRespawnTime * 200f) moveReason = "due for a manual reposition.";
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
