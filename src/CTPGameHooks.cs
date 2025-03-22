using CaptureThePearl.Helpers;
using HUD;
using Menu;
using Menu.Remix.MixedUI;
using Mono.WebBrowser;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RainMeadow;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
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
        On.PlayerProgression.SaveToDisk += PlayerProgression_SaveToDisk;
        On.PlayerProgression.GetOrInitiateSaveState += PlayerProgression_GetOrInitiateSaveState;
        On.HUD.TextPrompt.Update += TextPrompt_Update;
        try
        { //Remove Meadow changing the game over text
            On.HUD.TextPrompt.UpdateGameOverString -= RainMeadow.RainMeadow.instance.TextPrompt_UpdateGameOverString;
        } catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }

        On.RainWorldGame.GoToDeathScreen += RainWorldGame_GoToDeathScreen;
        On.Menu.SleepAndDeathScreen.AddPassageButton += SleepAndDeathScreen_AddPassageButton;
        On.GhostWorldPresence.ctor += GhostWorldPresence_ctor;
        On.ShelterDoor.Close += ShelterDoor_Close;
        On.World.LoadMapConfig += World_LoadMapConfig;
        On.HUD.Map.ShelterMarker.ctor += ShelterMarker_ctor;
        On.HUD.Map.ShelterMarker.Draw += ShelterMarker_Draw;
        On.ShortcutGraphics.Draw += ShortcutGraphics_Draw;
        On.HUD.Map.ctor += Map_ctor;
        try
        { //Remove Meadow preventing restarts
            On.Menu.KarmaLadderScreen.Update -= RainMeadow.RainMeadow.instance.KarmaLadderScreen_Update;
        } catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }

        //pearl colors
        On.DataPearl.UniquePearlMainColor += DataPearl_UniquePearlMainColor;
        On.DataPearl.UniquePearlHighLightColor += DataPearl_UniquePearlHighLightColor;
        On.DataPearl.ctor += DataPearl_ctor;

        On.Menu.ArenaOverlay.PlayerPressedContinue += ArenaOverlay_PlayerPressedContinue;
        On.Menu.PlayerResultBox.GrafUpdate += PlayerResultBox_GrafUpdate;

        On.WorldLoader.CreatingWorld += WorldLoader_CreatingWorld;

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
        On.HUD.Map.ResetNotRevealedMarkers += Map_ResetNotRevealedMarkers;
        if(ModManager.MSC) On.MoreSlugcats.MSCRoomSpecificScript.AddRoomSpecificScript += MSCRoomSpecificScript_AddRoomSpecificScript;
        chatColourHook = new Hook(typeof(ChatLogOverlay).GetMethod(nameof(ChatLogOverlay.UpdateLogDisplay)), ChatLogOverlay_UpdateLogDisplay);

        playerDisplayHook = new Hook(typeof(OnlinePlayerDisplay).GetMethod(nameof(OnlinePlayerDisplay.Draw)), OnlinePlayerDisplay_Draw);
        On.PhysicalObject.Grabbed += PhysicalObject_Grabbed;
        On.Player.ReleaseGrasp += Player_ReleaseGrasp;
        On.Weapon.HitThisObject += Weapon_HitThisObject;
        HooksApplied = true;
    }
    public static void RemoveHooks()
    {
        if (!HooksApplied) return;
        RainMeadow.RainMeadow.Debug("[CTP]: Removing CTPGameHooks");

        On.RainWorldGame.Update -= RainWorldGame_Update;

        //On.Player.Update -= Player_Update;
        On.RainCycle.GetDesiredCycleLength -= RainCycle_GetDesiredCycleLength;
        On.RainWorldGame.BlizzardHardEndTimer -= RainWorldGame_BlizzardHardEndTimer;
        On.RainWorldGame.AllowRainCounterToTick -= RainWorldGame_AllowRainCounterToTick;
        On.RainCycle.Update -= RainCycle_Update;

        On.Player.CanBeSwallowed -= Player_CanBeSwallowed;
        On.PlayerProgression.SaveToDisk -= PlayerProgression_SaveToDisk;
        On.PlayerProgression.GetOrInitiateSaveState -= PlayerProgression_GetOrInitiateSaveState;
        On.HUD.TextPrompt.Update -= TextPrompt_Update;

        On.HUD.TextPrompt.UpdateGameOverString += RainMeadow.RainMeadow.instance.TextPrompt_UpdateGameOverString;

        On.RainWorldGame.GoToDeathScreen -= RainWorldGame_GoToDeathScreen;
        On.Menu.SleepAndDeathScreen.AddPassageButton -= SleepAndDeathScreen_AddPassageButton;
        On.GhostWorldPresence.ctor -= GhostWorldPresence_ctor;
        On.ShelterDoor.Close -= ShelterDoor_Close;
        On.World.LoadMapConfig -= World_LoadMapConfig;
        On.HUD.Map.ShelterMarker.ctor -= ShelterMarker_ctor;
        On.HUD.Map.ShelterMarker.Draw -= ShelterMarker_Draw;
        On.ShortcutGraphics.Draw -= ShortcutGraphics_Draw;
        On.HUD.Map.ctor -= Map_ctor;

        On.Menu.KarmaLadderScreen.Update += RainMeadow.RainMeadow.instance.KarmaLadderScreen_Update;

        On.DataPearl.UniquePearlMainColor -= DataPearl_UniquePearlMainColor;
        On.DataPearl.UniquePearlHighLightColor -= DataPearl_UniquePearlHighLightColor;
        On.DataPearl.ctor -= DataPearl_ctor;

        On.Menu.ArenaOverlay.PlayerPressedContinue -= ArenaOverlay_PlayerPressedContinue;
        On.Menu.PlayerResultBox.GrafUpdate -= PlayerResultBox_GrafUpdate;

        On.Oracle.Update -= Oracle_Update;
        On.SSOracleBehavior.UnconciousUpdate -= SSOracleBehavior_UnconciousUpdate;
        IteratorUnconsciousHook?.Undo();

        On.WorldLoader.CreatingWorld -= WorldLoader_CreatingWorld;//Changed from a += to a -= since it may be a mistake -Pocky
        On.GhostWorldPresence.SpawnGhost -= GhostWorldPresence_SpawnGhost;
        On.HUD.Map.ResetNotRevealedMarkers -= Map_ResetNotRevealedMarkers;
        if (ModManager.MSC) On.MoreSlugcats.MSCRoomSpecificScript.AddRoomSpecificScript -= MSCRoomSpecificScript_AddRoomSpecificScript;
        playerDisplayHook?.Undo();
        chatColourHook?.Undo();
        On.PhysicalObject.Grabbed -= PhysicalObject_Grabbed;
        On.Player.ReleaseGrasp -= Player_ReleaseGrasp;
        On.Weapon.HitThisObject -= Weapon_HitThisObject;
        HooksApplied = false;
    }
    #region Hooks, a lot of them
    private static bool Weapon_HitThisObject(On.Weapon.orig_HitThisObject orig, Weapon self, PhysicalObject obj)
    {
        if (CTPGameMode.IsCTPGameMode(out var mode) && obj is Player pl)
        {
            if (OnlinePhysicalObject.map.TryGetValue(pl.abstractCreature, out var opo))
            {
                var opposingOnlPl = opo.owner;
                //get my team
                if (self.thrownBy is Player slug)
                {
                    if (OnlinePhysicalObject.map.TryGetValue(slug.abstractCreature, out var opo2))
                    {
                        var throwingOnlPl = opo2.owner;

                        if (mode.PlayerTeams[opposingOnlPl] != mode.PlayerTeams[throwingOnlPl])
                        {
                            return true;
                        }
                        else return mode.friendlyFire;
                    }
                }
            }

        }

        return orig(self, obj);
    }
    private static void Player_ReleaseGrasp(On.Player.orig_ReleaseGrasp orig, Player self, int grasp)
    {
        var grabbed = self.grasps[grasp].grabbed;
        orig(self, grasp);
        if (grabbed is DataPearl porl && CTPGameMode.IsCTPGameMode(out var mode))
        {
            var porlIdx = CTPGameMode.PearlIdxToTeam(porl.AbstractPearl.dataPearlType.index);
            mode.TeamLostAPearl(porlIdx);
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
                mode.TeamHasAPearl(mode.PlayerTeams[onPl], porlIdx);
            }
        }
    }
    //bad coding practice, will fix later
    public static void ChatLogOverlay_UpdateLogDisplay(Action<ChatLogOverlay> orig, ChatLogOverlay self)
    {
        if (CTPGameMode.IsCTPGameMode(out var mode))
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
        else orig(self);
    }
    public static void OnlinePlayerDisplay_Draw(Action<OnlinePlayerDisplay> orig, OnlinePlayerDisplay self)
    {
        orig(self);

        if (!CTPGameMode.IsCTPGameMode(out var mode)) return;
        self.color = mode.GetTeamColor(mode.PlayerTeams[self.player]);
        self.lighter_color = self.color;


        //recolour everything ahhhhhhhhhh
        self.arrowSprite.color = self.color;
        self.gradient.color = self.color;
        foreach (var msgLbl in self.messageLabels) msgLbl.color = Color.Lerp(Color.white, self.color, 0.5f);
        self.slugIcon.color = self.color;
        self.username.color = self.color;
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

    //Update gamemode for clients
    //ALSO check if the game should end as the host
    private static void RainWorldGame_Update(On.RainWorldGame.orig_Update orig, RainWorldGame self)
    {
        orig(self);
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

            //get den pos
            /*string denPos = gamemode.lobby.isOwner
                ? RandomShelterChooser.GetRespawnShelter(gamemode.region, saveStateNumber, new string[0])
                : gamemode.defaultDenPos;*/
            byte myTeam = gamemode.PlayerTeams[OnlineManager.mePlayer];
            string denPos = gamemode.hasSpawnedIn
                ? RandomShelterChooser.GetRespawnShelter(gamemode.region, saveStateNumber, gamemode.TeamShelters.Where((s, i) => (byte) i != myTeam).ToArray(), Plugin.Options.RespawnCloseness.Value)
                : gamemode.TeamShelters[myTeam];
            gamemode.hasSpawnedIn = true;

            save.denPosition = denPos;
            gamemode.defaultDenPos = denPos; //hopefully unnecessary
            gamemode.myLastDenPos = denPos;

            return save;
        }
        return orig(self, saveStateNumber, game, setup, saveAsDeathOrQuit);
    }

    //Overrides Meadow overriding the function of going to the death screen after dying... ...did I word that poorly?
    private static void TextPrompt_Update(On.HUD.TextPrompt.orig_Update orig, HUD.TextPrompt self)
    {
        orig(self);

        if (self.currentlyShowing == HUD.TextPrompt.InfoID.GameOver && OnlineManager.players.Count > 1) //don't do this in single-player
        {
            if (self.hud.owner is Player player
                && player.abstractPhysicalObject.world.rainCycle.timer >= player.abstractPhysicalObject.world.rainCycle.cycleLength)
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

    //Prevents echoes from spawning
    private static void GhostWorldPresence_ctor(On.GhostWorldPresence.orig_ctor orig, GhostWorldPresence self, World world, GhostWorldPresence.GhostID ghostID)
    {
        orig(self, world, ghostID);
        self.ghostRoom = new AbstractRoom("FAKE_GHOST_ROOM", new int[] {-1}, -1, -1, -1, -1); //nope; don't spawn ghosts, please; they're scary!
    }

    //Prevents shelter doors from closing and triggering the win screen
    private static void ShelterDoor_Close(On.ShelterDoor.orig_Close orig, ShelterDoor self)
    {
        return;
    }

    //Ensures ALL shelters are marked on the map!
    private static void World_LoadMapConfig(On.World.orig_LoadMapConfig orig, World self, SlugcatStats.Name slugcatNumber)
    {
        orig(self, slugcatNumber);

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
                self.symbolSprite.color = CTPGameMode.LigherTeamColor(gamemode.GetTeamColor(idx));
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

        if (CTPGameMode.IsCTPGameMode(out var gamemode))
        {
            for (int i = 0; i < self.entraceSpriteToRoomExitIndex.Length; i++)
            {
                //if (kvp.Value.element.name == "ShortcutShelter" || kvp.Value.element.name == "ShortcutAShelter")
                int destNode = self.entraceSpriteToRoomExitIndex[i];
                if (destNode >= 0) {
                    var sprite = self.entranceSprites[i, 0];
                    string shelterName = self.room.world.GetAbstractRoom(self.room.abstractRoom.connections[destNode])?.name;
                    int idx = Array.IndexOf(gamemode.TeamShelters, shelterName);
                    if (idx >= 0)
                        sprite.color = CTPGameMode.LigherTeamColor(gamemode.GetTeamColor(idx));
                }
            }
        }
    }

    //Set all parts of the map to discovered!
    private static void Map_ctor(On.HUD.Map.orig_ctor orig, HUD.Map self, HUD.HUD hud, HUD.Map.MapData mapData)
    {
        hud.rainWorld.setup.revealMap = true;
        orig(self, hud, mapData);
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
        return CTPGameMode.LigherTeamColor(DataPearl.UniquePearlMainColor(pearlType));
    }

    //Makes pearls buoyant; handy for Shoreline!
    private static void DataPearl_ctor(On.DataPearl.orig_ctor orig, DataPearl self, AbstractPhysicalObject abstractPhysicalObject, World world)
    {
        orig(self, abstractPhysicalObject, world);

        self.buoyancy = 1.5f; //hopefully this is enough...?
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

        public string LastTeamGrasp = "";
    }
    #endregion
}
