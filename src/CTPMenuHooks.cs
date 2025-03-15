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
            On.Menu.SlugcatSelectMenu.AddColorButtons -= SlugcatSelectMenu_AddColorButtons;
            On.Menu.SimpleButton.Clicked -= SimpleButton_Clicked;
        }
        public static void ApplyHooks()
        {
            On.ProcessManager.PostSwitchMainProcess += ProcessManager_PostSwitchMainProcess;
            On.Menu.SlugcatSelectMenu.AddColorButtons += SlugcatSelectMenu_AddColorButtons;
            On.Menu.SimpleButton.Clicked += SimpleButton_Clicked;
        }
        private static void SlugcatSelectMenu_AddColorButtons(On.Menu.SlugcatSelectMenu.orig_AddColorButtons orig, SlugcatSelectMenu self)
        {
            if (self is CTPMenu storyMenu && OnlineManager.lobby.isOwner)
            {
                if (self.colorInterface == null)
                {
                    Vector2 pos = new Vector2(1000f - (1366f - self.manager.rainWorld.options.ScreenSize.x) / 2f, self.manager.rainWorld.options.ScreenSize.y - 100f);
                    self.colorInterface = self.GetColorInterfaceForSlugcat(storyMenu.slugcatColorOrder[storyMenu.hostSlugIndex], pos);
                    self.pages[0].subObjects.Add(self.colorInterface);
                }
            }
            else orig(self);
        }
        private static void SimpleButton_Clicked(On.Menu.SimpleButton.orig_Clicked orig, SimpleButton self)
        {
            orig(self);
            if (self.signalText != null && self.signalText == "CTPHostScugButton" && self.menu != null && self.menu is CTPMenu storyMenu)
            {
                storyMenu.hostSlugIndex = (storyMenu.hostSlugIndex > storyMenu.slugcatColorOrder.Count - 1) ? 0 : storyMenu.hostSlugIndex + 1;
                storyMenu.personaSettings.playingAs = storyMenu.slugcatColorOrder[storyMenu.hostSlugIndex];
                storyMenu.storyGameMode.avatarSettings.playingAs = storyMenu.slugcatColorOrder[storyMenu.hostSlugIndex];
                if (storyMenu.colorChecked)
                {
                    storyMenu.RemoveColorButtons();
                    storyMenu.AddColorButtons();
                }

                storyMenu.hostScugButton.menuLabel.text = SlugcatStats.getSlugcatName(storyMenu.slugcatColorOrder[storyMenu.hostSlugIndex]);
            }
        }

        private static void ProcessManager_PostSwitchMainProcess(On.ProcessManager.orig_PostSwitchMainProcess orig, ProcessManager self, ProcessManager.ProcessID ID)
        {
            if (ID == Plugin.CTPMenuProcessID) self.currentMainLoop = new CTPMenu(self);
            orig(self, ID);
        }

    }
}
