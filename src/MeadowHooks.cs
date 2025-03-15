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
    public static void ApplyHooks()
    {
        lobbySelectHook = new Hook(
            typeof(LobbySelectMenu).GetConstructors()[0],
            LobbySelectMenu_ctor
            );
        deathScreenRPCHook = new Hook(
            typeof(StoryRPCs).GetMethod(nameof(StoryRPCs.GoToDeathScreen)),
            StoryRPCs_GoToDeathScreen
            );
        leaveLobbyHook = new Hook(
            typeof(OnlineManager).GetMethod(nameof(OnlineManager.LeaveLobby)),
            OnlineManager_LeaveLobby
            );

        RainMeadow.RainMeadow.Debug("[CTP]: Applied Rain Meadow hooks");
    }

    private static Hook lobbySelectHook, deathScreenRPCHook, leaveLobbyHook;

    public static void RemoveHooks()
    {
        lobbySelectHook?.Undo();
        deathScreenRPCHook?.Undo();
        leaveLobbyHook?.Undo();
    }


    private delegate void LobbySelectMenu_ctor_orig(LobbySelectMenu self, ProcessManager manager);
    private static void LobbySelectMenu_ctor(LobbySelectMenu_ctor_orig orig, LobbySelectMenu self, ProcessManager manager)
    {
        orig(self, manager);

        self.filterModeDropDown.AddItems(true, new Menu.Remix.MixedUI.ListItem("Capture the Pearl"));
    }

    //Don't go to death screen while in the Capture the Pearl gamemode!!
    //This probably ought to go in CTPGameHooks, but I'm keeping it here to keep all the Meadow and Rain World stuff separated.
    private delegate void EmptyDelegate();
    private static void StoryRPCs_GoToDeathScreen(EmptyDelegate orig)
    {
        if (CTPGameMode.IsCTPGameMode(out var _)) return;
        orig();
    }

    private static void OnlineManager_LeaveLobby(EmptyDelegate orig)
    {
        orig();

        CTPGameHooks.RemoveHooks();
    }

}
