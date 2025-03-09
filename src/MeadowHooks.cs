using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using RainMeadow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CaptureThePearl;

/// <summary>
/// Hooks on Rain Meadow itself. Hopefully Lazy will handle all of this.
/// </summary>
public static class MeadowHooks
{
    public static void ApplyHooks(ManualLogSource logger)
    {
        lobbySelectHook = new Hook(
            typeof(LobbySelectMenu).GetConstructors()[0],
            LobbySelectMenu_ctor
            );
        /*lobbyCreateHook = new Hook(
            typeof(LobbyCreateMenu).GetConstructors()[0],
            LobbyCreateMenu_ctor
            );*/

        logger.LogDebug("Applied Rain Meadow hooks");
    }

    private static Hook lobbySelectHook, lobbyCreateHook;

    public static void RemoveHooks()
    {
        lobbySelectHook?.Undo();
        lobbyCreateHook?.Undo();
    }


    private delegate void LobbySelectMenu_ctor_orig(LobbySelectMenu self, ProcessManager manager);
    private static void LobbySelectMenu_ctor(LobbySelectMenu_ctor_orig orig, LobbySelectMenu self, ProcessManager manager)
    {
        orig(self, manager);

        self.filterModeDropDown.AddItems(true, new Menu.Remix.MixedUI.ListItem("Capture the Pearl"));
    }

    private delegate void LobbyCreateMenu_ctor_orig(LobbyCreateMenu self, ProcessManager manager);
    private static void LobbyCreateMenu_ctor(LobbyCreateMenu_ctor_orig orig, LobbyCreateMenu self, ProcessManager manager)
    {
        orig(self, manager);

        self.modeDropDown.AddItems(true, new Menu.Remix.MixedUI.ListItem("Capture the Pearl"));
    }
}
