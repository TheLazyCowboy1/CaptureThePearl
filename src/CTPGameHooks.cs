using CaptureThePearl.Helpers;
using Menu;
using RainMeadow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CaptureThePearl;

/// <summary>
/// Hooks that are only active WHILE playing the game mode.
/// For example, no swallowing pearls.
/// </summary>
public static class CTPGameHooks
{
    public static bool HooksApplied = false;
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
        try
        { //Remove Meadow preventing restarts
            On.Menu.KarmaLadderScreen.Update -= RainMeadow.RainMeadow.instance.KarmaLadderScreen_Update;
        } catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }

        //pearl colors
        On.DataPearl.UniquePearlMainColor += DataPearl_UniquePearlMainColor;
        On.DataPearl.UniquePearlHighLightColor += DataPearl_UniquePearlHighLightColor;

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

        On.Menu.KarmaLadderScreen.Update += RainMeadow.RainMeadow.instance.KarmaLadderScreen_Update;

        On.DataPearl.UniquePearlMainColor -= DataPearl_UniquePearlMainColor;
        On.DataPearl.UniquePearlHighLightColor -= DataPearl_UniquePearlHighLightColor;

        HooksApplied = false;
    }


    //Update gamemode for clients
    private static void RainWorldGame_Update(On.RainWorldGame.orig_Update orig, RainWorldGame self)
    {
        orig(self);
        if (CTPGameMode.IsCTPGameMode(out var gamemode))
            gamemode.ClientGameTick();
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

    //Ensures player is glowing
    //Hopefully not needed because SaveState is marked as theGlow = true
    private static void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
    {
        orig(self, eu);

        self.glowing = true;
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

        if (self.currentlyShowing == HUD.TextPrompt.InfoID.GameOver)
        {
            self.gameOverMode = true;
            self.restartNotAllowed = 0; //let restarts be allowed!
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
                return;

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

    //Sets the pearl team colors; currently done automatically, but we'll probably want to change this later
    private static Color DataPearl_UniquePearlMainColor(On.DataPearl.orig_UniquePearlMainColor orig, DataPearl.AbstractDataPearl.DataPearlType pearlType)
    {
        if (CTPGameMode.IsCTPGameMode(out var gamemode))
            return Color.HSVToRGB((float)CTPGameMode.PearlIdxToTeam(pearlType.index) / (float)gamemode.NumberOfTeams, 1f, 0.9f);
        return orig(pearlType);
    }
    //Sets the highlight to the same color but with lower saturation
    private static Color? DataPearl_UniquePearlHighLightColor(On.DataPearl.orig_UniquePearlHighLightColor orig, DataPearl.AbstractDataPearl.DataPearlType pearlType)
    {
        if (CTPGameMode.IsCTPGameMode(out var gamemode))
            return Color.HSVToRGB((float)CTPGameMode.PearlIdxToTeam(pearlType.index) / (float)gamemode.NumberOfTeams, 0.7f, 0.95f);
        return orig(pearlType);
    }
}
