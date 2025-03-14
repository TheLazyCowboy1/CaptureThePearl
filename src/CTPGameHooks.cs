using CaptureThePearl.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CaptureThePearl;

/// <summary>
/// Hooks that are only active WHILE playing the game mode.
/// For example, no swallowing pearls.
/// </summary>
public static class CTPGameHooks
{

    public static void ApplyHooks()
    {
        On.Player.ctor += Player_ctor;
        On.PlayerProgression.SaveToDisk += PlayerProgression_SaveToDisk;
        On.PlayerProgression.GetOrInitiateSaveState += PlayerProgression_GetOrInitiateSaveState;
    }

    public static void RemoveHooks()
    {
        On.Player.ctor -= Player_ctor;
        On.PlayerProgression.SaveToDisk -= PlayerProgression_SaveToDisk;
        On.PlayerProgression.GetOrInitiateSaveState -= PlayerProgression_GetOrInitiateSaveState;
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


    private static SaveState PlayerProgression_GetOrInitiateSaveState(On.PlayerProgression.orig_GetOrInitiateSaveState orig, PlayerProgression self, SlugcatStats.Name saveStateNumber, RainWorldGame game, ProcessManager.MenuSetup setup, bool saveAsDeathOrQuit)
    {
        if (CTPGameMode.IsCTPGameMode(out var gamemode))
        {
            RainMeadow.RainMeadow.Debug($"[CTP]: Creating custom blank save state for {saveStateNumber}!");

            //create save state
            var save = new SaveState(saveStateNumber, self);
            save.loaded = true;
            save.redExtraCycles = false;
            save.initiatedInGameVersion = 0;

            //get den pos (TEMPORARY IMPLEMENTATION!)
            string denPos = RandomShelterChooser.GetRespawnShelter(gamemode.region, saveStateNumber, new string[0]);

            save.denPosition = denPos;
            gamemode.myLastDenPos = denPos;

            return save;
        }
        return orig(self, saveStateNumber, game, setup, saveAsDeathOrQuit);
    }
}
