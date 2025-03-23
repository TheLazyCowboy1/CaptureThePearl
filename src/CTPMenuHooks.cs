using Menu;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RainMeadow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CaptureThePearl
{
    public static class CTPMenuHooks
    {
        public static void RemoveHooks()
        {
            On.ProcessManager.PostSwitchMainProcess -= ProcessManager_PostSwitchMainProcess;
            On.Menu.SlugcatSelectMenu.StartGame -= SlugcatSelectMenu_StartGame;

            On.PlayerProgression.IsThereASavedGame -= PlayerProgression_IsThereASavedGame;
            On.PlayerProgression.WipeSaveState -= PlayerProgression_WipeSaveState;
        }

        public static void ApplyHooks()
        {
            On.ProcessManager.PostSwitchMainProcess += ProcessManager_PostSwitchMainProcess;
            On.Menu.SlugcatSelectMenu.StartGame += SlugcatSelectMenu_StartGame;

            On.PlayerProgression.IsThereASavedGame += PlayerProgression_IsThereASavedGame;
            On.PlayerProgression.WipeSaveState += PlayerProgression_WipeSaveState;
        }

        private static bool PlayerProgression_IsThereASavedGame(On.PlayerProgression.orig_IsThereASavedGame orig, PlayerProgression self, SlugcatStats.Name saveStateNumber)
        {
            if (CTPGameMode.IsCTPGameMode(out var _)) return true; //act like there's always a saved game
            return orig(self, saveStateNumber);
        }

        private static void PlayerProgression_WipeSaveState(On.PlayerProgression.orig_WipeSaveState orig, PlayerProgression self, SlugcatStats.Name saveStateNumber)
        {
            if (CTPGameMode.IsCTPGameMode(out var _)) return; //don't wipe save state for CTP
            orig(self, saveStateNumber);
        }

        private static void SlugcatSelectMenu_StartGame(On.Menu.SlugcatSelectMenu.orig_StartGame orig, SlugcatSelectMenu self, SlugcatStats.Name storyGameCharacter)
        {
            if (self is CTPMenu menu) menu.StartGame(storyGameCharacter);
            else orig(self, storyGameCharacter);
        }
        private static void ProcessManager_PostSwitchMainProcess(On.ProcessManager.orig_PostSwitchMainProcess orig, ProcessManager self, ProcessManager.ProcessID ID)
        {
            if (ID == Plugin.CTPMenuProcessID) self.currentMainLoop = new CTPMenu(self);
            orig(self, ID);
        }

    }
}
