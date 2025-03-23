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
        }
        public static void ApplyHooks()
        {
            On.ProcessManager.PostSwitchMainProcess += ProcessManager_PostSwitchMainProcess;
        }

        private static void ProcessManager_PostSwitchMainProcess(On.ProcessManager.orig_PostSwitchMainProcess orig, ProcessManager self, ProcessManager.ProcessID ID)
        {
            if (ID == Plugin.CTPMenuProcessID) self.currentMainLoop = new CTPMenu(self);
            orig(self, ID);
        }

    }
}
