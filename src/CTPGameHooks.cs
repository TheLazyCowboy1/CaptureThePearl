﻿using CaptureThePearl.Helpers;
using HUD;
using Watcher;
using Menu;
using Menu.Remix.MixedUI;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RainMeadow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using Color = UnityEngine.Color;
using Exception = System.Exception;

namespace CaptureThePearl;

/// <summary>
/// Hooks that are only active WHILE playing the game mode.
/// For example, no swallowing pearls.
/// </summary>
public static class CTPGameHooks
{
    public static bool HooksApplied = false;
    public static Hook playerDisplayHook;
    public static Hook chatColourHook;
    public static Hook spectateButtonHook;
    public static Hook newOPOHook;
    public static Hook resourceFeedHook;
    public static void ApplyHooks()
    {
        if (HooksApplied) return;
        RainMeadow.RainMeadow.Debug("[CTP]: Applying CTPGameHooks");

        On.RainWorldGame.Update += RainWorldGame_Update;

        //On.Player.Update += Player_Update;
        On.RainCycle.GetDesiredCycleLength += RainCycle_GetDesiredCycleLength;
        On.RainWorldGame.BlizzardHardEndTimer += RainWorldGame_BlizzardHardEndTimer;
        On.RainWorldGame.AllowRainCounterToTick += RainWorldGame_AllowRainCounterToTick;
        On.RainCycle.Update += RainCycle_Update;

        On.Player.CanBeSwallowed += Player_CanBeSwallowed;
        On.Player.Stun += Player_Stun;
        On.PlayerProgression.SaveToDisk += PlayerProgression_SaveToDisk;
        On.PlayerProgression.GetOrInitiateSaveState += PlayerProgression_GetOrInitiateSaveState;
        On.HUD.TextPrompt.Update += TextPrompt_Update;
        try
        { //Remove Meadow changing the game over text
            On.HUD.TextPrompt.UpdateGameOverString -= RainMeadow.RainMeadow.instance.TextPrompt_UpdateGameOverString;
        } catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }

        On.RainWorldGame.GoToDeathScreen += RainWorldGame_GoToDeathScreen;
        On.Menu.SleepAndDeathScreen.AddPassageButton += SleepAndDeathScreen_AddPassageButton;
        On.ShelterDoor.Close += ShelterDoor_Close;
        On.World.LoadMapConfig_Timeline += World_LoadMapConfig;
        On.HUD.Map.ShelterMarker.ctor += ShelterMarker_ctor;
        On.HUD.Map.ShelterMarker.Draw += ShelterMarker_Draw;
        On.ShortcutGraphics.Draw += ShortcutGraphics_Draw;
        On.HUD.Map.ctor += Map_ctor;
        On.HUD.Map.Draw += Map_Draw;
        try
        { //Remove Meadow preventing restarts
            On.Menu.KarmaLadderScreen.Update -= RainMeadow.RainMeadow.instance.KarmaLadderScreen_Update;
        } catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }

        //pearl colors
        On.DataPearl.UniquePearlMainColor += DataPearl_UniquePearlMainColor;
        On.DataPearl.UniquePearlHighLightColor += DataPearl_UniquePearlHighLightColor;
        On.DataPearl.ctor += DataPearl_ctor;
        On.DataPearl.AbstractDataPearl.ctor += AbstractDataPearl_ctor;

        On.Menu.ArenaOverlay.PlayerPressedContinue += ArenaOverlay_PlayerPressedContinue;
        On.Menu.PlayerResultBox.GrafUpdate += PlayerResultBox_GrafUpdate;
        //IL.PlayerGraphics.ApplyPalette += PlayerGraphics_ApplyPalette;

        On.WorldLoader.CreatingWorld += WorldLoader_CreatingWorld;
        On.Room.TrySpawnWarpPoint += Room_TrySpawnWarpPoint;

        On.Player.ctor += Player_ctor;
        On.Oracle.Update += Oracle_Update;
        On.SSOracleBehavior.UnconciousUpdate += SSOracleBehavior_UnconciousUpdate;
        try
        {
            IteratorUnconsciousHook = new(
                typeof(Oracle).GetProperty(nameof(Oracle.Consious)).GetGetMethod(),
                Oracle_Conscious_Get
                );
        } catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }

        On.GhostWorldPresence.SpawnGhost += GhostWorldPresence_SpawnGhost;
        //On.World.SpawnGhost += World_SpawnGhost;
        On.Watcher.SpinningTop.ctor += SpinningTop_ctor;
        On.GhostWorldPresence.ctor_World_GhostID_int += GhostWorldPresence_ctor_World_GhostID_int;
        if(ModManager.MSC) On.MoreSlugcats.MSCRoomSpecificScript.AddRoomSpecificScript += MSCRoomSpecificScript_AddRoomSpecificScript;
        chatColourHook = new Hook(typeof(ChatLogOverlay).GetMethod(nameof(ChatLogOverlay.UpdateLogDisplay)), ChatLogOverlay_UpdateLogDisplay);

        spectateButtonHook = new Hook(typeof(SpectatorOverlay).GetMethod(nameof(SpectatorOverlay.Update)), SpectatorOverlay_Update);

        playerDisplayHook = new Hook(typeof(OnlinePlayerDisplay).GetMethod(nameof(OnlinePlayerDisplay.Draw)), OnlinePlayerDisplay_Draw);
        On.PhysicalObject.Grabbed += PhysicalObject_Grabbed;
        On.Player.ReleaseGrasp += Player_ReleaseGrasp;

        //On.AbstractCreature.Realize += AbstractCreature_Realize;
        //On.AbstractCreature.RealizeInRoom += AbstractCreature_RealizeInRoom;
        //On.Creature.Update += Creature_Update;
        //resourceFeedHook = new(typeof(ResourceSubscription).GetConstructors()[0], ResourceSubscription_ctor);

        On.Weapon.HitThisObject += Weapon_HitThisObject;
        On.Player.Collide += Player_Collide;
        On.Player.SlugSlamConditions += Player_SlugSlamConditions;
        On.Player.ClassMechanicsArtificer += Player_ClassMechanicsArtificer;
        On.Player.CanMaulCreature += Player_CanMaulCreature;

        //newOPOHook = new(typeof(OnlinePhysicalObject).GetMethod(nameof(OnlinePhysicalObject.NewFromApo)), OnlinePhysicalObject_NewFromApo);

        HooksApplied = true;
    }

    public static void RemoveHooks()
    {
        if (!HooksApplied) return;
        RainMeadow.RainMeadow.Debug("[CTP]: Removing CTPGameHooks");

        On.RainWorldGame.Update -= RainWorldGame_Update;

        On.RainCycle.GetDesiredCycleLength -= RainCycle_GetDesiredCycleLength;
        On.RainWorldGame.BlizzardHardEndTimer -= RainWorldGame_BlizzardHardEndTimer;
        On.RainWorldGame.AllowRainCounterToTick -= RainWorldGame_AllowRainCounterToTick;
        On.RainCycle.Update -= RainCycle_Update;

        On.Player.CanBeSwallowed -= Player_CanBeSwallowed;
        On.Player.Stun -= Player_Stun;
        On.PlayerProgression.SaveToDisk -= PlayerProgression_SaveToDisk;
        On.PlayerProgression.GetOrInitiateSaveState -= PlayerProgression_GetOrInitiateSaveState;
        On.HUD.TextPrompt.Update -= TextPrompt_Update;

        On.HUD.TextPrompt.UpdateGameOverString += RainMeadow.RainMeadow.instance.TextPrompt_UpdateGameOverString;

        On.RainWorldGame.GoToDeathScreen -= RainWorldGame_GoToDeathScreen;
        On.Menu.SleepAndDeathScreen.AddPassageButton -= SleepAndDeathScreen_AddPassageButton;
        On.ShelterDoor.Close -= ShelterDoor_Close;
        On.World.LoadMapConfig_Timeline -= World_LoadMapConfig;
        On.HUD.Map.ShelterMarker.ctor -= ShelterMarker_ctor;
        On.HUD.Map.ShelterMarker.Draw -= ShelterMarker_Draw;
        On.ShortcutGraphics.Draw -= ShortcutGraphics_Draw;
        On.HUD.Map.ctor -= Map_ctor;
        On.HUD.Map.Draw -= Map_Draw;

        On.Menu.KarmaLadderScreen.Update += RainMeadow.RainMeadow.instance.KarmaLadderScreen_Update;

        On.DataPearl.UniquePearlMainColor -= DataPearl_UniquePearlMainColor;
        On.DataPearl.UniquePearlHighLightColor -= DataPearl_UniquePearlHighLightColor;
        On.DataPearl.ctor -= DataPearl_ctor;
        On.DataPearl.AbstractDataPearl.ctor -= AbstractDataPearl_ctor;

        On.Menu.ArenaOverlay.PlayerPressedContinue -= ArenaOverlay_PlayerPressedContinue;
        On.Menu.PlayerResultBox.GrafUpdate -= PlayerResultBox_GrafUpdate;
        //IL.PlayerGraphics.ApplyPalette -= PlayerGraphics_ApplyPalette;

        On.Player.ctor -= Player_ctor;
        On.Oracle.Update -= Oracle_Update;
        On.SSOracleBehavior.UnconciousUpdate -= SSOracleBehavior_UnconciousUpdate;
        IteratorUnconsciousHook?.Undo();

        On.WorldLoader.CreatingWorld -= WorldLoader_CreatingWorld;
        On.Room.TrySpawnWarpPoint -= Room_TrySpawnWarpPoint;
        On.GhostWorldPresence.SpawnGhost -= GhostWorldPresence_SpawnGhost;
        //On.World.SpawnGhost -= World_SpawnGhost;
        On.Watcher.SpinningTop.ctor -= SpinningTop_ctor;
        On.GhostWorldPresence.ctor_World_GhostID_int -= GhostWorldPresence_ctor_World_GhostID_int;
        //On.HUD.Map.ResetNotRevealedMarkers -= Map_ResetNotRevealedMarkers;
        if (ModManager.MSC) On.MoreSlugcats.MSCRoomSpecificScript.AddRoomSpecificScript -= MSCRoomSpecificScript_AddRoomSpecificScript;
        playerDisplayHook?.Undo();
        spectateButtonHook?.Undo();
        chatColourHook?.Undo();
        On.PhysicalObject.Grabbed -= PhysicalObject_Grabbed;
        On.Player.ReleaseGrasp -= Player_ReleaseGrasp;

        //On.AbstractCreature.Realize -= AbstractCreature_Realize;
        //On.AbstractCreature.RealizeInRoom -= AbstractCreature_RealizeInRoom;
        //On.Creature.Update -= Creature_Update;
        //resourceFeedHook?.Undo();

        On.Weapon.HitThisObject -= Weapon_HitThisObject;
        On.Player.Collide -= Player_Collide;
        On.Player.SlugSlamConditions -= Player_SlugSlamConditions;
        On.Player.ClassMechanicsArtificer -= Player_ClassMechanicsArtificer;
        On.Player.CanMaulCreature -= Player_CanMaulCreature;

        //newOPOHook?.Undo();

        HooksApplied = false;
    }
    #region Hooks, a lot of them

    //Don't spawn Watcher warp points
    private static WarpPoint Room_TrySpawnWarpPoint(On.Room.orig_TrySpawnWarpPoint orig, Room self, PlacedObject po, bool saveInRegionState, bool skipIfInRegionState, bool deathPersistent)
    {
        return null;
    }

    //Prevent the game from blacking out Watcher in CTP
    [Obsolete]
    private static void PlayerGraphics_ApplyPalette(ILContext il)
    {
        var c = new ILCursor(il);

        if (c.TryGotoNext(
            MoveType.After,
            x => x.MatchLdsfld<ModManager>(nameof(ModManager.Watcher))
            ))
        {
            c.Emit(Mono.Cecil.Cil.OpCodes.Pop); //pop that field
            //c.Emit(Mono.Cecil.Cil.OpCodes.Ldfld, SlugcatStats.Name.Night); //replace it with Nightcat again
            c.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I4_0); //replace with false
            RainMeadow.RainMeadow.Debug("[CTP]: IL hook succeeded.");
        }
        else
            RainMeadow.RainMeadow.Error("[CTP]: IL hook failed.");
    }

    //give each player a spear and a rock
    private static void Player_ctor(On.Player.orig_ctor orig, Player self, AbstractCreature abstractCreature, World world)
    {
        orig(self, abstractCreature, world);

        try
        {
            if (CTPGameMode.IsCTPGameMode(out var gamemode) && gamemode.ArmPlayers
                && self.IsLocal())
            {
                //spawn a spear
                var abSpear = new AbstractSpear(world, null, abstractCreature.pos, world.game.GetNewID(), false);
                abstractCreature.Room.AddEntity(abSpear);
                abSpear.RealizeInRoom();

                //spawn a rock
                var abRock = new AbstractPhysicalObject(world, AbstractPhysicalObject.AbstractObjectType.Rock, null, abstractCreature.pos, world.game.GetNewID());
                abstractCreature.Room.AddEntity(abRock);
                abRock.RealizeInRoom();

                //attempt to grab them
                self.Grab(abSpear.realizedObject, 0, 0, Creature.Grasp.Shareability.CanNotShare, 0, false, true);
                self.Grab(abRock.realizedObject, 1, 0, Creature.Grasp.Shareability.CanNotShare, 0, false, true);
            }
        }
        catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
    }

    //sync world state less often
    private delegate void ResourceSubscription_ctor_orig(ResourceSubscription self, OnlineResource resource, OnlinePlayer player);
    private static void ResourceSubscription_ctor(ResourceSubscription_ctor_orig orig, ResourceSubscription self, OnlineResource resource, OnlinePlayer player)
    {
        orig(self, resource, player);

        if (self.basecooldown > 0 && resource is WorldSession)
            self.basecooldown = 11; //sync WorldSession half as often as normal, to slightly reduce lag
    }

    private delegate OnlinePhysicalObject OnlinePhysicalObject_NewFromApo_ctor(AbstractPhysicalObject apo);
    private static OnlinePhysicalObject OnlinePhysicalObject_NewFromApo(OnlinePhysicalObject_NewFromApo_ctor orig, AbstractPhysicalObject apo)
    {
        if (!(apo is AbstractCreature ac))
            return orig(apo);

        var opo = orig(apo);
        opo.AddData(new CreatureSpawnData(ac));
        return opo;
    }

    private delegate void SpectatorOverlay_Update_orig(SpectatorOverlay self);
    private static void SpectatorOverlay_Update(SpectatorOverlay_Update_orig orig, SpectatorOverlay self)
    {
        orig(self);

        foreach (var button in self.PlayerButtons)
        {
            if (CTPGameMode.IsCTPGameMode(out var gamemode) && !gamemode.OnMyTeam(button.player))
                button.buttonBehav.greyedOut = true; //grey out spectate button for players on other team
        }
    }

    private static List<AbstractCreature> CreaturesToAbstractize = new();
    private static void AbstractCreature_Realize(On.AbstractCreature.orig_Realize orig, AbstractCreature self)
    {
        if (CTPGameMode.IsCTPGameMode(out var gamemode) && !gamemode.ShouldRealizeCreature(self))
            //return; //don't realize it
            CreaturesToAbstractize.Add(self);

        orig(self);
    }

    private static void Creature_Update(On.Creature.orig_Update orig, Creature self, bool eu)
    {
        orig(self, eu);

        if (CTPGameMode.IsCTPGameMode(out var gamemode))
        {
            var abCrit = self.abstractCreature;
            bool hasOPO = self.abstractPhysicalObject.GetOnlineObject() != null;
            bool shouldSync = gamemode.ShouldSyncAPOInRoom(null, abCrit);
            if (hasOPO && !shouldSync)
            {
                RainMeadow.RainMeadow.Debug($"[CTP]: Abstracting unsynced realized creature: {abCrit}");
                //self.abstractPhysicalObject.destroyOnAbstraction = true;
                if (abCrit.abstractAI != null) abCrit.abstractAI.RealAI = null; //stupid creature controllers!!!!!
                abCrit.Abstractize(self.abstractPhysicalObject.pos);
                abCrit.realizedCreature = null;
            }
            else if (!hasOPO && shouldSync)
            {
                RainMeadow.RainMeadow.Debug($"[CTP]: Syncing unsynced realized creature: {self.abstractCreature}");
                self.room.abstractRoom.GetResource()?.ApoEnteringRoom(self.abstractPhysicalObject, self.abstractPhysicalObject.pos);
            }
        }
    }

    private static void AbstractCreature_RealizeInRoom(On.AbstractCreature.orig_RealizeInRoom orig, AbstractCreature self)
    {
        if (CTPGameMode.IsCTPGameMode(out var gamemode) && !gamemode.ShouldRealizeCreature(self))
            return; //don't realize it

        //Ensure that the creature gets registered if it gets realized
        if (self.GetOnlineCreature() == null)
        {
            var rs = self.Room.GetResource();
            rs?.ApoEnteringRoom(self, self.pos);
        }

        orig(self);
    }

    //Show Players Everywhere randomly thrown in here 'cuz why not lol
    private static void Map_Draw(On.HUD.Map.orig_Draw orig, HUD.Map self, float timeStacker)
    {
        if (CTPGameMode.IsCTPGameMode(out var gamemode1))
        {
            //ensure pearl icons show up, even in other rooms
            foreach (var pearl in gamemode1.TeamPearls)
            {
                if (pearl == null) continue; //don't add myself
                try
                {
                    pearl.apo.world.game.GetStorySession.AddNewPersistentTracker(pearl.apo); //this automatically checks if it's already added
                }
                catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
                if (pearl != null)
                {
                    if (!self.mapData.objectTrackers.Any(t => pearl.apo.ID == t?.obj?.ID))
                    { //if it's not currently being tracked, add its tracker
                        self.addTracker(new (pearl.apo));
                    }
                }
            }

            //remove trackers for non-CTP pearls
            for (int i = self.mapData.objectTrackers.Count - 1; i >= 0; i--)
            {
                var tracker = self.mapData.objectTrackers[i];
                if (!gamemode1.TeamPearls.Any(p => p?.apo?.ID == tracker?.obj?.ID))
                    self.removeTracker(tracker); //if it's not in the team pearls list, remove it
            }
        }

        orig(self, timeStacker);

        if (CTPGameMode.IsCTPGameMode(out var gamemode))
        {
            if (self.fade <= 0)
                return; //if the map isn't being drawn, don't draw its symbols!
            if (self?.hud?.owner is not Player owner)
                return;
            if (owner?.room?.game is null)
                return;

            //step 3: remove all player sprites already added
            foreach (var symbol in self.creatureSymbols)
            {
                if (symbol.iconData.critType == CreatureTemplate.Type.Slugcat)
                    symbol.RemoveSprites();
            }

            //step 4: draw those avatars!
            foreach (var onlinePlayer in gamemode.PlayerTeams.Keys)
            {
                if (onlinePlayer.isMe) continue; //don't add myself
                try
                {
                    var player = gamemode.lobby.playerAvatars.Find(kvp => kvp.Key == onlinePlayer).Value.FindEntity(true) as OnlineCreature;
                    //var player = gamemode.avatars[i];
                    //if (player == owner.abstractCreature) continue; //redundant check

                    //if (player.realizedCreature is not Player realPlayer) continue;
                    if (!player.abstractCreature.pos.TileDefined) //player doesn't even have a tile location???
                    {
                        //Logger.LogWarning("No tile position for player!!! " + player.ToString());
                        continue;
                    }

                    //create symbol
                    var symbol = new CreatureSymbol(CreatureSymbol.SymbolDataFromCreature(player.abstractCreature), self.inFrontContainer);
                    symbol.Show(true);
                    symbol.lastShowFlash = 0f;
                    symbol.showFlash = 0f;

                    //set player colors here???
                    //symbol.myColor = player.GetData<SlugcatCustomization>().bodyColor;
                    symbol.myColor = gamemode.GetTeamColor(gamemode.PlayerTeams.TryGetValue(player.owner, out byte team) ? team : 0);
                    symbol.symbolSprite.alpha = 0.9f;

                    //shrink dead or indeterminate players (probably distant ones)
                    if (player.realizedCreature is null || player.realizedCreature.dead)
                    {
                        symbol.symbolSprite.scale = 0.8f;
                        symbol.symbolSprite.alpha = 0.7f;
                    }

                    //modify shadows
                    symbol.shadowSprite1.alpha = symbol.symbolSprite.alpha;
                    symbol.shadowSprite2.alpha = symbol.symbolSprite.alpha;
                    symbol.shadowSprite1.scale = symbol.symbolSprite.scale;
                    symbol.shadowSprite2.scale = symbol.symbolSprite.scale;

                    //draw in correct position
                    Vector2 drawPos = self.RoomToMapPos((player.realizedCreature is null) ? player.abstractCreature.pos.Tile.ToVector2() * 20f : player.realizedCreature.mainBodyChunk.pos, player.abstractCreature.Room.index, timeStacker);
                    symbol.Draw(timeStacker, drawPos);

                    //add to creatureSymbol list to get cleared!
                    self.creatureSymbols.Add(symbol);
                }
                catch { }// (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
            }
        }
    }

    private static void Player_ReleaseGrasp(On.Player.orig_ReleaseGrasp orig, Player self, int grasp)
    {
        var grabbed = (grasp >= 0 && grasp < self.grasps.Length) ? self.grasps[grasp]?.grabbed : null;
        orig(self, grasp);
        if (grabbed is DataPearl porl && CTPGameMode.IsCTPGameMode(out var mode))
        {
            var porlIdx = CTPGameMode.PearlIdxToTeam(porl.AbstractPearl.dataPearlType.index);
            mode.TeamLostAPearl(porlIdx);

            //un-modify speed //new = old - c*old + c == new - c = (1-c)old == (new - c) / (1 - c) = old
            self.slugcatStats.runspeedFac /= mode.PearlHeldSpeed;
            self.slugcatStats.runspeedFac = (self.slugcatStats.runspeedFac - 1 + mode.PearlHeldSpeed) / mode.PearlHeldSpeed;
            self.slugcatStats.poleClimbSpeedFac /= mode.PearlHeldSpeed;
            self.slugcatStats.poleClimbSpeedFac = (self.slugcatStats.poleClimbSpeedFac - 1 + mode.PearlHeldSpeed) / mode.PearlHeldSpeed;
            self.slugcatStats.corridorClimbSpeedFac /= mode.PearlHeldSpeed;
            self.slugcatStats.corridorClimbSpeedFac = (self.slugcatStats.corridorClimbSpeedFac - 1 + mode.PearlHeldSpeed) / mode.PearlHeldSpeed;
        }
    }
    private static void PhysicalObject_Grabbed(On.PhysicalObject.orig_Grabbed orig, PhysicalObject self, Creature.Grasp grasp)
    {
        orig(self, grasp);
        if (self is DataPearl porl && grasp.grabber is Player pl && CTPGameMode.IsCTPGameMode(out var mode))
        {
            if (OnlinePhysicalObject.map.TryGetValue(pl.abstractCreature, out var opo))
            {
                var onPl = opo.owner;
                var porlIdx = CTPGameMode.PearlIdxToTeam(porl.AbstractPearl.dataPearlType.index);
                if (porl.GetData().ShouldSendMessage(mode.PlayerTeams[onPl]))
                    mode.TeamHasAPearl(mode.PlayerTeams[onPl], porlIdx);
            }

            //modify speed //new = old + (1 - old) * c
            pl.slugcatStats.runspeedFac += (1 - pl.slugcatStats.runspeedFac) * (1 - mode.PearlHeldSpeed); //move towards Survivor stats
            pl.slugcatStats.runspeedFac *= mode.PearlHeldSpeed;
            pl.slugcatStats.poleClimbSpeedFac += (1 - pl.slugcatStats.poleClimbSpeedFac) * (1 - mode.PearlHeldSpeed); //move towards Survivor stats
            pl.slugcatStats.poleClimbSpeedFac *= mode.PearlHeldSpeed;
            pl.slugcatStats.corridorClimbSpeedFac += (1 - pl.slugcatStats.corridorClimbSpeedFac) * (1 - mode.PearlHeldSpeed); //move towards Survivor stats
            pl.slugcatStats.corridorClimbSpeedFac *= mode.PearlHeldSpeed;
        }
    }

    private delegate void UpdateLogDisplay_orig(ChatLogOverlay self);
    private static void ChatLogOverlay_UpdateLogDisplay(UpdateLogDisplay_orig orig, ChatLogOverlay self)
    {
        /*if (CTPGameMode.IsCTPGameMode(out var mode))
        {
            if (self.chatHud.chatLog.Count > 0)
            {
                var logsToRemove = new List<MenuObject>();

                // First, collect all the logs to remove
                foreach (var log in self.pages[0].subObjects)
                {
                    log.RemoveSprites();
                    logsToRemove.Add(log);
                }

                // Now remove the logs from the original collection
                foreach (var log in logsToRemove)
                {
                    self.pages[0].RemoveSubObject(log);
                }

                // username:color
                foreach (var playerAvatar in OnlineManager.lobby.playerAvatars.Select(kv => kv.Value))
                {
                    if (playerAvatar.FindEntity(true) is OnlinePhysicalObject opo)
                    {
                        if (!self.colorDictionary.ContainsKey(opo.owner.id.name) && opo.TryGetData<SlugcatCustomization>(out var customization))
                        {
                            self.colorDictionary.Add(opo.owner.id.name, customization.bodyColor);
                        }
                    }
                }

                float yOffSet = 0;
                foreach (var (username, message) in self.chatHud.chatLog)
                {
                    if (username is null or "")
                    {
                        // system message
                        var messageLabel = new MenuLabel(self, self.pages[0], message,
                            new Vector2((1366f - self.manager.rainWorld.options.ScreenSize.x) / 2f - 660f, 330f - yOffSet),
                            new Vector2(self.manager.rainWorld.options.ScreenSize.x, 30f), false);
                        messageLabel.label.alignment = FLabelAlignment.Left;
                        messageLabel.label.color = self.SYSTEM_COLOR;
                        self.pages[0].subObjects.Add(messageLabel);
                    }
                    else
                    {
                        float H = 0f;
                        float S = 0f;
                        float V = 0f;

                        var color = self.colorDictionary.TryGetValue(username, out var colorOrig) ? colorOrig : Color.white;
                        var colorNew = color;

                        Color.RGBToHSV(color, out H, out S, out V);
                        if (V < 0.8f) { colorNew = Color.HSVToRGB(H, S, 0.8f); }

                        var usernameLabel = new MenuLabel(self, self.pages[0], username,
                            new Vector2((1366f - self.manager.rainWorld.options.ScreenSize.x) / 2f - 660f, 330f - yOffSet),
                            new Vector2(self.manager.rainWorld.options.ScreenSize.x, 30f), false);
                        usernameLabel.label.alignment = FLabelAlignment.Left;
                        usernameLabel.label.color = colorNew;
                        self.pages[0].subObjects.Add(usernameLabel);

                        var usernameWidth = LabelTest.GetWidth(usernameLabel.label.text);
                        var messageLabel = new MenuLabel(self, self.pages[0], $": {message}",
                            new Vector2((1366f - self.manager.rainWorld.options.ScreenSize.x) / 2f - 660f + usernameWidth + 2f, 330f - yOffSet),
                            new Vector2(self.manager.rainWorld.options.ScreenSize.x, 30f), false);
                        messageLabel.label.alignment = FLabelAlignment.Left;

                        foreach (var onPl in OnlineManager.players)
                        {
                            if (onPl.id.name == username)
                            {
                                //find myself in playerteams
                                var teamNum = -1;
                                if (mode.PlayerTeams.ContainsKey(onPl)) teamNum = mode.PlayerTeams[onPl];

                                messageLabel.label.color = Color.Lerp(Color.white, mode.GetTeamColor(teamNum), 0.5f);
                                break;
                            }
                        }

                        self.pages[0].subObjects.Add(messageLabel);
                    }

                    yOffSet += 20f;
                }
            }
        }
        else orig(self);*/
        orig(self);

        if (CTPGameMode.IsCTPGameMode(out var gamemode))
        {
            int lastFoundIdx = -1; //optimization AND prevents miscoloring
            foreach (var obj in self.pages[0].subObjects)
            {
                //var obj = self.pages[0].subObjects[i];
                if (obj is MenuLabel label)
                {
                    if (label.label.color == Futile.white && label.label.text.StartsWith(": "))
                    {
                        //try to find corresponding chatLog
                        //foreach (var (username, message) in self.chatHud.chatLog)
                        for (int i = lastFoundIdx + 1; i < self.chatHud.chatLog.Count; i++)
                        {
                            string username = self.chatHud.chatLog[i].Item1, message = self.chatHud.chatLog[i].Item2;
                            if (label.label.text == ": " + message)
                            {
                                var player = OnlineManager.players.Find(p => p.id.name == username);
                                if (player != null && gamemode.PlayerTeams.TryGetValue(player, out byte team))
                                    label.label.color = CTPGameMode.LighterTeamColor(gamemode.GetTeamColor(team));
                                lastFoundIdx = i;
                                break;
                            }
                        }
                    }
                }
            }
        }

    }


    public delegate void OnlinePlayerDisplay_Draw_orig(OnlinePlayerDisplay self, float tStacker);
    public static void OnlinePlayerDisplay_Draw(OnlinePlayerDisplay_Draw_orig orig, OnlinePlayerDisplay self, float tStacker)
    {
        orig(self, tStacker);

        if (!CTPGameMode.IsCTPGameMode(out var mode)) return;

        if (mode.PlayerTeams.ContainsKey(self.player))
        {
            self.color = mode.GetTeamColor(mode.PlayerTeams[self.player]);
            self.lighter_color = self.color;


            //recolour everything ahhhhhhhhhh
            self.arrowSprite.color = self.color;
            self.gradient.color = self.color;
            foreach (var msgLbl in self.messageLabels) msgLbl.color = Color.Lerp(Color.white, self.color, 0.5f);
            self.slugIcon.color = self.color;
            self.username.color = self.color;
        }
    }
    private static void MSCRoomSpecificScript_AddRoomSpecificScript(On.MoreSlugcats.MSCRoomSpecificScript.orig_AddRoomSpecificScript orig, Room room)
    {
        string name = room.abstractRoom.name;
        if (name == "SU_PMPSTATION01" && room.game.IsStorySession)
        {
            room.AddObject(new MoreSlugcats.MSCRoomSpecificScript.SU_PMPSTATION01_safety());
        }
        if (name == "HR_LAYERS_OF_REALITY")
        {
            room.AddObject(new MoreSlugcats.MSCRoomSpecificScript.InterlinkControl(room));
        }
        if (name == "DM_ROOF04")
        {
            room.AddObject(new MoreSlugcats.MSCRoomSpecificScript.randomGodsSoundSource(0.45f, new Vector2(460f, 70f), 3000f, room));
        }
        if (name == "DM_ROOF03")
        {
            room.AddObject(new MoreSlugcats.MSCRoomSpecificScript.DM_ROOF03GradientGravity(room));
        }
        if (name == "DM_C13")
        {
            room.AddObject(new MoreSlugcats.MSCRoomSpecificScript.randomGodsSoundSource(0.45f, new Vector2(0f, 380f), 2800f, room));
        }
    }

    private static Hook IteratorUnconsciousHook;
    private static void Map_ResetNotRevealedMarkers(On.HUD.Map.orig_ResetNotRevealedMarkers orig, HUD.Map self)
    {
        orig(self);
        //allows players to see shelters on map
        if (self.hud != null && self.hud.owner is Player pl)
        {
            for (int i = 0; i < self.notRevealedFadeMarkers.Count; i++)
            {
                if (self.notRevealedFadeMarkers[i] is Map.ShelterMarker shelterMarker)
                {
                    shelterMarker.FadeIn((float)(30 + self.notRevealedFadeMarkers.Count));
                    self.notRevealedFadeMarkers.RemoveAt(i);
                }
                if (self.notRevealedFadeMarkers[i] is Map.ItemMarker marker && marker.obj.type == AbstractPhysicalObject.AbstractObjectType.DataPearl)
                {
                    marker.FadeIn((float)(30 + self.notRevealedFadeMarkers.Count));
                    self.notRevealedFadeMarkers.RemoveAt(i);
                }
            }
        }
    }

    private static bool GhostWorldPresence_SpawnGhost(On.GhostWorldPresence.orig_SpawnGhost orig, GhostWorldPresence.GhostID ghostID, int karma, int karmaCap, int ghostPreviouslyEncountered, bool playingAsRed)
    {
        return false;//No ghosts, not even ghost hunches
    }

    //Watcher ghost prevention shenanigans
    private static void GhostWorldPresence_ctor_World_GhostID_int(On.GhostWorldPresence.orig_ctor_World_GhostID_int orig, GhostWorldPresence self, World world, GhostWorldPresence.GhostID ghostID, int spinningTopSpawnId)
    {
        try //mark this ghost as already encountered
        {
            var encounterList = world.game.GetStorySession.saveState.deathPersistentSaveData.spinningTopEncounters;
            if (!encounterList.Contains(spinningTopSpawnId))
                encounterList.Add(spinningTopSpawnId);
        }
        catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }

        orig(self, world, ghostID, spinningTopSpawnId);
    }

    //Watcher ghost shenanigans continued...
    private static void World_SpawnGhost(On.World.orig_SpawnGhost orig, World self)
    {
        //literally just do nothing
        self.spinningTopPresences.Clear(); //just in case
    }
    //It is so unnecessarily hard to prevent the echo from spawning... how about I just immediately despawn it once it spawns, lol?
    private static void SpinningTop_ctor(On.Watcher.SpinningTop.orig_ctor orig, SpinningTop self, Room room, PlacedObject placedObject, GhostWorldPresence worldGhost)
    {
        orig(self, room, placedObject, worldGhost);

        self.slatedForDeletetion = true; //bye-bye, Spinning Top!
    }

    //Update gamemode for clients
    //ALSO check if the game should end as the host
    private static void RainWorldGame_Update(On.RainWorldGame.orig_Update orig, RainWorldGame self)
    {
        orig(self);

        try
        {
            if (CTPGameMode.IsCTPGameMode(out var gamemode))
            {
                gamemode.ClientGameTick();

                //should game end?
                if (gamemode.gameSetup && self.world != null && self.world.rainCycle != null
                    && self.world.rainCycle.timer >= self.world.rainCycle.cycleLength)
                {
                    //display no respawns message
                    if (self.world.rainCycle.timer == self.world.rainCycle.cycleLength)
                    {
                        ChatLogManager.LogMessage("", "The game is ending! Respawns are now disabled.");
                    }

                    bool shouldEnd = self.world.rainCycle.timer >= self.world.rainCycle.cycleLength + 40 * 60; //1 minute hard limit
                    if (!shouldEnd)
                    {
                        shouldEnd = true;
                        foreach (var kvp in gamemode.lobby.playerAvatars) //search if any player is still alive
                        {
                            var oc = kvp.Value.FindEntity(true) as OnlineCreature;
                            if (oc != null && oc.abstractCreature.state.alive)
                            {
                                shouldEnd = false;
                                break;
                            }
                        }
                        if (shouldEnd) RainMeadow.RainMeadow.Debug("[CTP]: Ending game because no more players are still alive!");
                    }
                    if (shouldEnd)
                    {
                        gamemode.EndGame();
                        foreach (var player in OnlineManager.players) //tell others the game is over
                        {
                            if (!player.isMe) player.InvokeOnceRPC(CTPRPCs.GameFinished);
                        }
                    }
                }
            }

            //abstractize the necessary creatures
            if (self.world != null)
            {
                foreach (var crit in CreaturesToAbstractize)
                {
                    if (crit != null && crit.IsLocal())
                    {
                        if (crit.abstractAI != null) crit.abstractAI.RealAI = null; //stupid creature controllers!!!!!
                        crit.Abstractize(crit.pos);
                        crit.realizedCreature = null;
                        try
                        {
                            var opo = crit.GetOnlineObject();
                            opo?.Deactivated(opo.primaryResource); //bye-bye creature!
                        }
                        catch { }
                        RainMeadow.RainMeadow.Debug($"[CTP]: Abstractized duplicate creature {crit}");
                    }
                }
            }
            CreaturesToAbstractize.Clear();

        }
        catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
    }

    //Sets game timer, basically
    private static int RainCycle_GetDesiredCycleLength(On.RainCycle.orig_GetDesiredCycleLength orig, RainCycle self)
    {
        if (CTPGameMode.IsCTPGameMode(out var gamemode)) //should always be true
        {
            int time = gamemode.TimerLength * 40 * 60;
            self.cycleLength = time;
            self.baseCycleLength = time;
            return time;
        }
        else return orig(self);
    }

    //Sets time before Saint's blizzard kills EVERYONE (TimerLength + 1)
    private static int RainWorldGame_BlizzardHardEndTimer(On.RainWorldGame.orig_BlizzardHardEndTimer orig, bool storyMode)
    {
        if (CTPGameMode.IsCTPGameMode(out var gamemode)) //should always be true
            return (gamemode.TimerLength + 1) * 40 * 60;
        return orig(storyMode);
    }

    //Ensures the timer always ticks
    private static bool RainWorldGame_AllowRainCounterToTick(On.RainWorldGame.orig_AllowRainCounterToTick orig, RainWorldGame self)
    {
        return true;
    }

    //Ensures death rain happens
    private static void RainCycle_Update(On.RainCycle.orig_Update orig, RainCycle self)
    {
        orig(self);

        //if death rain SHOULD hit (but has not because we're a region with no rain), force it to hit
        if (!self.deathRainHasHit && self.timer >= self.cycleLength)
        {
            self.RainHit();
            self.deathRainHasHit = true;
        }

        //if time is long enough, switch to total death rain
        if (self.timer >= self.cycleLength + 40 * 60) //more than one minute of rain
        {
            var globRain = self.world.game.globalRain;
            if (globRain.deathRain == null) globRain.InitDeathRain();
            globRain.deathRain.deathRainMode = GlobalRain.DeathRain.DeathRainMode.Mayhem;
            globRain.Intensity = 1f;
        }
    }

    //Prevents players from swallowing pearls
    private static bool Player_CanBeSwallowed(On.Player.orig_CanBeSwallowed orig, Player self, PhysicalObject testObj)
    {
        if (testObj is DataPearl) return false;
        return orig(self, testObj);
    }

    //Makes players release pearls when stunned
    private static void Player_Stun(On.Player.orig_Stun orig, Player self, int st)
    {
        orig(self, st);

        foreach (var grasp in self.grasps)
        {
            if (grasp?.grabbed is DataPearl)
                self.ReleaseGrasp(grasp.graspUsed); //ensures it properly updates stats
                //grasp.Release();
        }
    }

    //Don't overwrite my save files with silly CTP stuff!!!
    private static bool PlayerProgression_SaveToDisk(On.PlayerProgression.orig_SaveToDisk orig, PlayerProgression self, bool saveCurrentState, bool saveMaps, bool saveMiscProg)
    {
        if (CTPGameMode.IsCTPGameMode(out var _)) return false;
        return orig(self, saveCurrentState, saveMaps, saveMiscProg);
    }

    //Prevents the game from loading a pre-existing save state, instead forcing it to use a brand new one.
    //ALSO chooses spawn position!
    private static SaveState PlayerProgression_GetOrInitiateSaveState(On.PlayerProgression.orig_GetOrInitiateSaveState orig, PlayerProgression self, SlugcatStats.Name saveStateNumber, RainWorldGame game, ProcessManager.MenuSetup setup, bool saveAsDeathOrQuit)
    {
        if (CTPGameMode.IsCTPGameMode(out var gamemode)) //hopefully this check is redundant, but I need the gamemode anyway
        {
            RainMeadow.RainMeadow.Debug($"[CTP]: Creating custom blank save state for {saveStateNumber}!");

            //create save state
            var save = new SaveState(saveStateNumber, self);
            save.loaded = true;
            save.redExtraCycles = false;
            save.initiatedInGameVersion = 0;

            save.theGlow = true; //make players glow; it's a nice convenience!
            var ghosts = save.deathPersistentSaveData.ghostsTalkedTo.Keys;
            foreach (var key in ghosts)
            {
                save.deathPersistentSaveData.ghostsTalkedTo[key] = 2; //disable ghost signals and ghosts; pretend we talked to all of them
            }

            //get den pos
            /*string denPos = gamemode.lobby.isOwner
                ? RandomShelterChooser.GetRespawnShelter(gamemode.region, saveStateNumber, new string[0])
                : gamemode.defaultDenPos;*/
            try
            {
                byte myTeam = gamemode.PlayerTeams[OnlineManager.mePlayer];
                string denPos = gamemode.hasSpawnedIn
                    ? RandomShelterChooser.GetRespawnShelter(gamemode.region, saveStateNumber, gamemode.TeamShelters.Where((s, i) => (byte)i != myTeam).ToArray(), gamemode.ShelterRespawnCloseness)
                    : gamemode.TeamShelters[myTeam];
                gamemode.hasSpawnedIn = true;

                save.denPosition = denPos;
                gamemode.defaultDenPos = denPos; //hopefully unnecessary
                gamemode.myLastDenPos = denPos;
            }
            catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }

            return save;
        }
        return orig(self, saveStateNumber, game, setup, saveAsDeathOrQuit);
    }

    //Overrides Meadow overriding the function of going to the death screen after dying... ...did I word that poorly?
    private static void TextPrompt_Update(On.HUD.TextPrompt.orig_Update orig, HUD.TextPrompt self)
    {
        orig(self);

        if (CTPGameMode.IsCTPGameMode(out var gamemode) && self.currentlyShowing == HUD.TextPrompt.InfoID.GameOver && OnlineManager.players.Count > 1) //don't do this in single-player
        {
            if (self.hud.owner is Player player
                && (player.abstractPhysicalObject.world.rainCycle.timer >= player.abstractPhysicalObject.world.rainCycle.cycleLength
                || !gamemode.lobby.clientSettings.Values.Any(cs => cs.inGame && !cs.isMine)))
            { //if respawns are not allowed, hide the game over screen
                self.gameOverMode = false;
                self.restartNotAllowed = 1; //and prevent restarts; use Rain Meadow's exit menu thingy instead
            }
            else
            {
                self.gameOverMode = true;
                self.restartNotAllowed = 0; //let restarts be allowed!
            }
        }
    }

    //Prevent players from restarting while no other players are alive at the moment
    //ALSO stops Rain Meadow from preventing clients from restarting!!
    private static void RainWorldGame_GoToDeathScreen(On.RainWorldGame.orig_GoToDeathScreen orig, RainWorldGame self)
    {
        if (CTPGameMode.IsCTPGameMode(out var gamemode))
        {
            //if the timer has ended, no respawning!
            if (self.world.rainCycle.timer >= self.world.rainCycle.cycleLength)
            {
                if (self.world.rainCycle.timer >= self.world.rainCycle.cycleLength + 40 * 60)
                { //extra 1 minute over; everyone is dead; the game may end now
                    gamemode.SanitizeCTP();
                    orig(self);
                }
                return;
            }

            //Try to determine if there is ANY OTHER player who is still active in the game
            bool otherPlayerInGame = gamemode.lobby.clientSettings.Values.Any(cs => cs.inGame && !cs.isMine);
            RainMeadow.RainMeadow.Debug($"[CTP]: Other player in game = {otherPlayerInGame}");

            if (!otherPlayerInGame) //if I'm the only one currently alive in the game, return
                return;

            //override Rain Meadow's overrides for client restarting
            if (!gamemode.lobby.isOwner && RPCEvent.currentRPCEvent is null)
            {
                RPCEvent.currentRPCEvent = new RPCEvent();
                orig(self);
                RPCEvent.currentRPCEvent = null;
                return;
            }
        }
        orig(self);
    }

    //Prevents the host from passaging; that'd be annoying
    private static void SleepAndDeathScreen_AddPassageButton(On.Menu.SleepAndDeathScreen.orig_AddPassageButton orig, SleepAndDeathScreen self, bool buttonBlack)
    {
        return; //just absolutely do nothing; don't add it
    }
    //Prevents shelter doors from closing and triggering the win screen
    private static void ShelterDoor_Close(On.ShelterDoor.orig_Close orig, ShelterDoor self)
    {
        return;
    }
    //Ensures ALL shelters are marked on the map!
    private static void World_LoadMapConfig(On.World.orig_LoadMapConfig_Timeline orig, World self, SlugcatStats.Timeline timelinePosition)
    {
        orig(self, timelinePosition);
        for (int i = 0; i < self.brokenShelters.Length; i++) self.brokenShelters[i] = false;
    }

    //Colorizes team shelters on the map; a helpful little bonus!
    private static void ShelterMarker_ctor(On.HUD.Map.ShelterMarker.orig_ctor orig, HUD.Map.ShelterMarker self, HUD.Map map, int room, Vector2 inRoomPosition)
    {
        orig(self, map, room, inRoomPosition);

        if (CTPGameMode.IsCTPGameMode(out var gamemode))
        {
            string name = map.mapData.NameOfRoom(room);
            int idx = Array.IndexOf(gamemode.TeamShelters, name);
            if (idx >= 0)
                self.symbolSprite.color = CTPGameMode.LighterTeamColor(gamemode.GetTeamColor(idx));
        }
    }

    //Prevents Rain World from resetting the shelter color!!!
    private static void ShelterMarker_Draw(On.HUD.Map.ShelterMarker.orig_Draw orig, HUD.Map.ShelterMarker self, float timeStacker)
    {
        var origColor = self.symbolSprite.color;
        orig(self, timeStacker);
        self.symbolSprite.color = origColor;
    }

    //Colorize team shortcut entrances
    //Has to be done here because Rain World keeps resetting its colors in Draw. Annoying and inefficient...
    private static void ShortcutGraphics_Draw(On.ShortcutGraphics.orig_Draw orig, ShortcutGraphics self, float timeStacker, Vector2 camPos)
    {
        orig(self, timeStacker, camPos);
        try
        {
            if (CTPGameMode.IsCTPGameMode(out var gamemode))
            {
                for (int i = 0; i < self.entraceSpriteToRoomExitIndex.Length; i++)
                {
                    //if (kvp.Value.element.name == "ShortcutShelter" || kvp.Value.element.name == "ShortcutAShelter")
                    int destNode = self.entraceSpriteToRoomExitIndex[i];
                    if (destNode >= 0 && destNode < self.room.abstractRoom.connections.Length)
                    {
                        var sprite = self.entranceSprites[i, 0];
                        string shelterName = self.room.world.GetAbstractRoom(self.room.abstractRoom.connections[destNode])?.name;
                        int idx = Array.IndexOf(gamemode.TeamShelters, shelterName);
                        if (idx >= 0)
                            sprite.color = CTPGameMode.LighterTeamColor(gamemode.GetTeamColor(idx));
                    }
                }
            }
        }
        catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
    }

    //Set all parts of the map to discovered and revealed
    private static void Map_ctor(On.HUD.Map.orig_ctor orig, Map self, HUD.HUD hud, Map.MapData mapData)
    {
        //add shelters...?
        try
        {
            if (CTPGameMode.IsCTPGameMode(out var gamemode))
            {
                List<Map.MapData.ShelterData> newShelters = new(gamemode.NumberOfTeams);
                foreach (string shel in gamemode.TeamShelters)
                {
                    if (!mapData.shelterData.Any(data => mapData.world.GetAbstractRoom(data.roomIndex).name == shel))
                    {
                        var room = mapData.world.GetAbstractRoom(shel);
                        if (room != null)
                        {
                            newShelters.Add(new(room.index, (room.size * 10).ToVector2()));
                        }
                    }
                }
                if (newShelters.Count > 0) //add new shelter data
                    mapData.shelterData = mapData.shelterData.Concat(newShelters).ToArray();
            }
        }
        catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }

        hud.rainWorld.setup.revealMap = true; //causes everything to be discovered
        orig(self, hud, mapData);
        self.revealAllDiscovered = true; //causes everything to be revealed
    }

    //Sets the pearl team colors; currently done automatically, but we'll probably want to change this later
    private static Color DataPearl_UniquePearlMainColor(On.DataPearl.orig_UniquePearlMainColor orig, DataPearl.AbstractDataPearl.DataPearlType pearlType)
    {
        if (CTPGameMode.IsCTPGameMode(out var gamemode))
            return gamemode.GetTeamColor(CTPGameMode.PearlIdxToTeam(pearlType.index));
            //return Color.HSVToRGB((float)CTPGameMode.PearlIdxToTeam(pearlType.index) / (float)gamemode.NumberOfTeams, 1f, 0.9f);
        return orig(pearlType);
    }
    //Sets the highlight to the same color but with lower saturation
    private static Color? DataPearl_UniquePearlHighLightColor(On.DataPearl.orig_UniquePearlHighLightColor orig, DataPearl.AbstractDataPearl.DataPearlType pearlType)
    {
        //if (CTPGameMode.IsCTPGameMode(out var gamemode))
        //return Color.HSVToRGB((float)CTPGameMode.PearlIdxToTeam(pearlType.index) / (float)gamemode.NumberOfTeams, 0.7f, 0.95f);
        //return orig(pearlType);
        return CTPGameMode.LighterTeamColor(DataPearl.UniquePearlMainColor(pearlType));
    }

    //Makes pearls buoyant; handy for Shoreline!
    private static void DataPearl_ctor(On.DataPearl.orig_ctor orig, DataPearl self, AbstractPhysicalObject abstractPhysicalObject, World world)
    {
        orig(self, abstractPhysicalObject, world);

        self.buoyancy = 1.5f; //hopefully this is enough...?

        //ALSO destroy if there's already a team pearl of this color, or if it can't be a team pearl
        if (CTPGameMode.IsCTPGameMode(out var gamemode) && self.IsLocal()) { //don't destroy others' pearls
            if (!gamemode.CanBeTeamPearl(self.AbstractPearl))
            {
                //destroy the pearl
                //try { CTPGameMode.DestroyPearl(ref opo); } catch { }
                try {
                    CTPGameMode.DestroyPearl(self.AbstractPearl);
                }
                catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
            }
        }
    }


    private static void AbstractDataPearl_ctor(On.DataPearl.AbstractDataPearl.orig_ctor orig, DataPearl.AbstractDataPearl self, World world, AbstractPhysicalObject.AbstractObjectType objType, PhysicalObject realizedObject, WorldCoordinate pos, EntityID ID, int originRoom, int placedObjectIndex, PlacedObject.ConsumableObjectData consumableData, DataPearl.AbstractDataPearl.DataPearlType dataPearlType)
    {
        orig(self, world, objType, realizedObject, pos, ID, originRoom, placedObjectIndex, consumableData, dataPearlType);

        if (CTPGameMode.IsCTPGameMode(out var gamemode) && self.IsLocal() && !gamemode.CanBeTeamPearl(self))
        {
            //destroy the pearl
            try
            {
                CTPGameMode.DestroyPearl(self);
            }
            catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
        }
    }

    //Prevent arena overlay from trying to start a new game or something stupid like that!
    private static void ArenaOverlay_PlayerPressedContinue(On.Menu.ArenaOverlay.orig_PlayerPressedContinue orig, ArenaOverlay self)
    {
        try
        {
            (self.manager.currentMainLoop as RainWorldGame).ExitGame(false, true);
        }
        catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
        //DON'T process anything!!! Just try to go back to lobby!
    }

    //Colorize team results as a nice little bonus
    private static void PlayerResultBox_GrafUpdate(On.Menu.PlayerResultBox.orig_GrafUpdate orig, PlayerResultBox self, float timeStacker)
    {
        orig(self, timeStacker);

        if (CTPGameMode.IsCTPGameMode(out var gamemode))
            self.playerNameLabel.label.color = gamemode.GetTeamColor(self.player.playerNumber);
    }

    //Shove iterators into wall, where they'll be out of sight (a stupid solution, but it seems to work pretty well)
    public static void Oracle_Update(On.Oracle.orig_Update orig, Oracle self, bool eu)
    {
        orig(self, eu);

        self.firstChunk.pos.x += 100f;
    }

    //Make iterators unconscious
    public delegate bool orig_Get_Oracle_Consious(Oracle self);
    public static bool Oracle_Conscious_Get(orig_Get_Oracle_Consious orig, Oracle self)
    {
        return false;
    }

    //Stop unconscious iterators from disabling anti-gravity
    private static void SSOracleBehavior_UnconciousUpdate(On.SSOracleBehavior.orig_UnconciousUpdate orig, SSOracleBehavior self)
    {
        orig(self);
        self.oracle.room.gravity = 0f;
    }

    //Remove connections to blocked rooms
    private static void WorldLoader_CreatingWorld(On.WorldLoader.orig_CreatingWorld orig, WorldLoader self)
    {
        //add list of indices to block
        List<int> blockedConnections = new();
        foreach (var room in self.abstractRooms)
        {
            if (RandomShelterFilter.BLOCKED_ROOMS.Contains(room.name))
                blockedConnections.Add(room.index);
        }

        foreach (var room in self.abstractRooms)
        {
            for (int i = 0; i < room.connections.Length; i++)
            {
                if (blockedConnections.Contains(room.connections[i]))
                    room.connections[i] = -1;
            }
        }

        orig(self);
    }
    #endregion
    #region conditional weak tables
    private static readonly ConditionalWeakTable<DataPearl, PorlData> PorlCWT = new ConditionalWeakTable<DataPearl, PorlData>();
    public static PorlData GetData(this DataPearl Porl)
    {
        return PorlCWT.GetValue(Porl, (DataPearl _) => new PorlData(Porl));
    }
    public class PorlData
    {
        public PorlData(DataPearl Porl)
        {
            self = Porl;
        }
        public DataPearl self;

        public bool ShouldSendMessage(int team)
        {
            if (team == LastTeamGrasp) return false; //don't send messages unless it actually changes hands...
            LastTeamGrasp = team;
            long newTime = DateTime.Now.Ticks / 1000L; //in milliseconds
            bool send = newTime - LastMsgTime > 1000; //> 1 second delay
            if (send)
                LastMsgTime = newTime;
            return send;
        }

        public int LastTeamGrasp = -1; // no team
        public long LastMsgTime = -1;

        //public string LastTeamGrasp = "";
    }
    #endregion

    #region Friendly Fire hooks
    //Used for all of these, just to simplify the process and reduce duplicate code
    private static T FFTrick<T>(CTPGameMode mode, OnlinePlayer p1, OnlinePlayer p2, Func<T> orig)
    {
        if (mode.PlayerTeams.TryGetValue(p1, out byte t1) && mode.PlayerTeams.TryGetValue(p2, out byte t2) && t1 != t2)
        {
            mode.friendlyFire = true; //just trick the game into thinking FF is true for a second
            var ret = orig();
            mode.friendlyFire = false;
            return ret;
        }
        return orig();
    }

    private static bool Weapon_HitThisObject(On.Weapon.orig_HitThisObject orig, Weapon self, PhysicalObject obj)
    {
        if (CTPGameMode.IsCTPGameMode(out var mode) && !mode.friendlyFire && obj is Player pl)
        {
            if (self.thrownBy is Player slug)
            {
                return FFTrick(mode, pl.abstractCreature.GetOnlineCreature().owner, slug.abstractCreature.GetOnlineCreature().owner, () => orig(self, obj));
            }
        }
        return orig(self, obj);
    }

    private static bool Player_CanMaulCreature(On.Player.orig_CanMaulCreature orig, Player self, Creature crit)
    {
        if (CTPGameMode.IsCTPGameMode(out var mode) && !mode.friendlyFire && crit is Player pl)
        {
            return FFTrick(mode, pl.abstractCreature.GetOnlineCreature().owner, self.abstractCreature.GetOnlineCreature().owner, () => orig(self, crit));
        }
        return orig(self, crit);
    }

    //This one is a bit different... no friendly fire generally just disables Arti's stun as a whole; this re-enables it
    private static void Player_ClassMechanicsArtificer(On.Player.orig_ClassMechanicsArtificer orig, Player self)
    {
        if (CTPGameMode.IsCTPGameMode(out var mode) && !mode.friendlyFire)
        {
            mode.friendlyFire = true;
            orig(self);
            mode.friendlyFire = false;
            return;
        }
        orig(self);
    }

    private static bool Player_SlugSlamConditions(On.Player.orig_SlugSlamConditions orig, Player self, PhysicalObject otherObject)
    {
        if (CTPGameMode.IsCTPGameMode(out var mode) && !mode.friendlyFire && otherObject is Player pl)
        {
            return FFTrick(mode, pl.abstractCreature.GetOnlineCreature().owner, self.abstractCreature.GetOnlineCreature().owner, () => orig(self, otherObject));
        }
        return orig(self, otherObject);
    }

    private static void Player_Collide(On.Player.orig_Collide orig, Player self, PhysicalObject otherObject, int myChunk, int otherChunk)
    {
        if (CTPGameMode.IsCTPGameMode(out var mode) && !mode.friendlyFire && otherObject is Player pl)
        {
            FFTrick(mode, pl.abstractCreature.GetOnlineCreature().owner, self.abstractCreature.GetOnlineCreature().owner, () => { orig(self, otherObject, myChunk, otherChunk); return true; }); //give it a phony return type
            return;
        }
        orig(self, otherObject, myChunk, otherChunk);
    }
    #endregion
}
