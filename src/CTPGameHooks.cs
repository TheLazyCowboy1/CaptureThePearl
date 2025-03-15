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

        On.Player.ctor += Player_ctor;
        On.PlayerProgression.SaveToDisk += PlayerProgression_SaveToDisk;
        On.PlayerProgression.GetOrInitiateSaveState += PlayerProgression_GetOrInitiateSaveState;
        On.HUD.TextPrompt.UpdateGameOverString += TextPrompt_UpdateGameOverString;
        On.HUD.TextPrompt.Update += TextPrompt_Update;

        On.RainWorldGame.GoToDeathScreen += RainWorldGame_GoToDeathScreen;
        On.Menu.SleepAndDeathScreen.AddPassageButton += SleepAndDeathScreen_AddPassageButton;
        //On.Menu.KarmaLadderScreen.Update += KarmaLadderScreen_Update;
        try
        {
            On.Menu.KarmaLadderScreen.Update -= RainMeadow.RainMeadow.instance.KarmaLadderScreen_Update;
        } catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }

        HooksApplied = true;
    }

    public static void RemoveHooks()
    {
        if (!HooksApplied) return;
        RainMeadow.RainMeadow.Debug("[CTP]: Removing CTPGameHooks");

        On.Player.ctor -= Player_ctor;
        On.PlayerProgression.SaveToDisk -= PlayerProgression_SaveToDisk;
        On.PlayerProgression.GetOrInitiateSaveState -= PlayerProgression_GetOrInitiateSaveState;
        On.PlayerProgression.GetOrInitiateSaveState -= PlayerProgression_GetOrInitiateSaveState;
        On.HUD.TextPrompt.UpdateGameOverString -= TextPrompt_UpdateGameOverString;
        On.HUD.TextPrompt.Update -= TextPrompt_Update;

        On.RainWorldGame.GoToDeathScreen -= RainWorldGame_GoToDeathScreen;
        On.Menu.SleepAndDeathScreen.AddPassageButton -= SleepAndDeathScreen_AddPassageButton;
        //On.Menu.KarmaLadderScreen.Update -= KarmaLadderScreen_Update;
        On.Menu.KarmaLadderScreen.Update += RainMeadow.RainMeadow.instance.KarmaLadderScreen_Update;

        HooksApplied = false;
    }

    //Makes players have neuron glow always
    private static void Player_ctor(On.Player.orig_ctor orig, Player self, AbstractCreature abstractCreature, World world)
    {
        orig(self, abstractCreature, world);
        self.glowing = true;
    }

    //Don't overwrite my save files with silly CTP stuff!!!
    private static bool PlayerProgression_SaveToDisk(On.PlayerProgression.orig_SaveToDisk orig, PlayerProgression self, bool saveCurrentState, bool saveMaps, bool saveMiscProg)
    {
        if (CTPGameMode.IsCTPGameMode(out var _)) return false;
        return orig(self, saveCurrentState, saveMaps, saveMiscProg);
    }

    //Prevents the game from loading a pre-existing save state, instead forcing it to use a brand new one.
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

    //Resets the game over string to something more palatable...
    private static void TextPrompt_UpdateGameOverString(On.HUD.TextPrompt.orig_UpdateGameOverString orig, HUD.TextPrompt self, Options.ControlSetup.Preset controllerType)
    {
        self.gameOverString = "Um go restart you're probably dead now no use fighting it give up already okay bye bye hopefully this won't break the universe";
    }

    //Overrides Meadow overriding the function of going to the death screen after dying... ...did I word that poorly?
    private static void TextPrompt_Update(On.HUD.TextPrompt.orig_Update orig, HUD.TextPrompt self)
    {
        orig(self);

        if (self.currentlyShowing == HUD.TextPrompt.InfoID.GameOver)
        {
            self.gameOverMode = true;
            self.restartNotAllowed = 0; //let restarts be allowed ya jerk!! (idk y my comments are so weird)
        }
    }

    //Attempts to prevent players from restarting while no other players are alive at the moment... we'll see how this goes...
    //ALSO stops Rain Meadow from preventing clients from restarting!!
    private static void RainWorldGame_GoToDeathScreen(On.RainWorldGame.orig_GoToDeathScreen orig, RainWorldGame self)
    {
        if (CTPGameMode.IsCTPGameMode(out var gamemode))
        {
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

    //Overrides Rain Meadow blocking the continue button for the host
    private static SimpleButton tempContinueButton = null;
    private static void KarmaLadderScreen_Update(On.Menu.KarmaLadderScreen.orig_Update orig, Menu.KarmaLadderScreen self)
    {
        //Override Rain Meadow's stuff; ugh it can be frustrating sometimes...
        if (tempContinueButton != null && self.continueButton == null)
            self.continueButton = tempContinueButton;

        orig(self);

        self.continueButton.buttonBehav.greyedOut = self.ButtonsGreyedOut; //override whether it is greyed out

        tempContinueButton = self.continueButton;
        self.continueButton = null; //set it to null so Rain Meadow can't mess with it

        //NOTE: Rain Meadow's code for greying out the button applies after ALL of this
    }

    //Prevents the host from passaging; that'd be annoying
    private static void SleepAndDeathScreen_AddPassageButton(On.Menu.SleepAndDeathScreen.orig_AddPassageButton orig, SleepAndDeathScreen self, bool buttonBlack)
    {
        return; //just absolutely do nothing; don't add it
    }
}
