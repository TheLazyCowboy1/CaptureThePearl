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
        //should these be in CTPGameHooks?
        chatMessageHook = new Hook(
            typeof(ChatHud).GetMethod(nameof(ChatHud.AddMessage)),
            ChatHud_AddMessage
            );
        playerNameMessageHook = new Hook(
            typeof(RPCs).GetMethod(nameof(RPCs.UpdateUsernameTemporarily)),
            RPCs_UpdateUsernameTemporarily
            );
        chatTutorialHook = new Hook(
            typeof(ChatHud).GetConstructors()[0],
            ChatHud_ctor
            );
        resourceFromID = new Hook(
            typeof(OnlineManager).GetMethod(nameof(OnlineManager.ResourceFromIdentifier)),
            OnlineManager_ResourceFromIdentifier
            );

        RainMeadow.RainMeadow.Debug("[CTP]: Applied Rain Meadow hooks");
    }

    private static Hook lobbySelectHook, deathScreenRPCHook, leaveLobbyHook,
        chatMessageHook, playerNameMessageHook, chatTutorialHook,
        resourceFromID;

    public static void RemoveHooks()
    {
        lobbySelectHook?.Undo();
        deathScreenRPCHook?.Undo();
        leaveLobbyHook?.Undo();
        chatMessageHook?.Undo();
        chatTutorialHook?.Undo();
    }


    //Add Capture the Pearl to lobby filter
    private delegate void LobbySelectMenu_ctor_orig(LobbySelectMenu self, ProcessManager manager);
    private static void LobbySelectMenu_ctor(LobbySelectMenu_ctor_orig orig, LobbySelectMenu self, ProcessManager manager)
    {
        orig(self, manager);

        self.filterModeDropDown.AddItems(true, new Menu.Remix.MixedUI.ListItem(CTPGameMode.GameModeName));
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

    //Filter messages from other teams, unless they start with '+'
    private delegate void ChatHud_AddMessage_orig(ChatHud self, string user, string message);
    private static void ChatHud_AddMessage(ChatHud_AddMessage_orig orig, ChatHud self, string user, string message)
    {
        if (!message.StartsWith("+"))
        {
            //don't add message if sent by other team
            if (CTPGameMode.IsCTPGameMode(out var gamemode) && gamemode.otherTeamsMuted)
            {
                byte myTeam = gamemode.GetMyTeam();
                foreach (var kvp in gamemode.PlayerTeams)
                {
                    if (kvp.Key.id.name == user)
                    {
                        if (kvp.Value != myTeam)
                            return; //he's not on my team! Don't send message
                        break; //he's on my team; no problem
                    }
                }
            }
        }
        else if (message.Length > 1)
            message = message.Substring(1); //remove the +

        orig(self, user, message);
    }

    //Filter messages from other teams.... why is this in two separate places???
    private delegate void RPCs_UpdateUsernameTemporarily_orig(RPCEvent rpc, string lastSentMessage);
    private static void RPCs_UpdateUsernameTemporarily(RPCs_UpdateUsernameTemporarily_orig orig, RPCEvent rpc, string lastSentMessage)
    {
        if (!lastSentMessage.StartsWith("+"))
        {
            //don't add message if sent by other team
            if (CTPGameMode.IsCTPGameMode(out var gamemode) && gamemode.otherTeamsMuted)
            {
                byte myTeam = gamemode.GetMyTeam();
                foreach (var kvp in gamemode.PlayerTeams)
                {
                    if (kvp.Key.id == rpc.from.id)
                    {
                        if (kvp.Value != myTeam)
                            return; //he's not on my team! Don't send message
                        break; //he's on my team; no problem
                    }
                }
            }
        }
        else if (lastSentMessage.Length > 1)
            lastSentMessage = lastSentMessage.Substring(1); //remove the +

        orig(rpc, lastSentMessage);
    }

    //update chat tutorial message
    private delegate void ChatHud_ctor_orig(ChatHud self, HUD.HUD hud, RoomCamera camera);
    private static void ChatHud_ctor(ChatHud_ctor_orig orig, ChatHud self, HUD.HUD hud, RoomCamera camera)
    {
        if (CTPGameMode.IsCTPGameMode(out var _) && !ChatLogManager.shownChatTutorial)
        {
            //show custom chat tutorial message
            hud.textPrompt.AddMessage(hud.rainWorld.inGameTranslator.Translate("Press '") + (RainMeadow.RainMeadow.rainMeadowOptions.ChatButtonKey.Value) + hud.rainWorld.inGameTranslator.Translate("' to chat, press '") + (RainMeadow.RainMeadow.rainMeadowOptions.ChatLogKey.Value) + hud.rainWorld.inGameTranslator.Translate("' to toggle the chat log") + ", prefix messages with + to send to all teams", 60, 320, true, true);
            ChatLogManager.shownChatTutorial = true;
        }

        orig(self, hud, camera);
    }

    //patch Meadow's broken code...
    private delegate OnlineResource stringDel(string rid);
    private static OnlineResource OnlineManager_ResourceFromIdentifier(stringDel orig, string rid)
    {
        var ret = orig(rid);
        if (ret != null || OnlineManager.lobby == null)
            return ret;

        //if (rid.Contains('_') && )
        if (rid.Contains("_")
            && OnlineManager.lobby.worldSessions.TryGetValue(rid.Substring(0, rid.IndexOf('_')), out var ws)
            && ws.roomSessions.TryGetValue(rid.Substring(rid.IndexOf('_') + 1), out var room))
            return room;
        if (OnlineManager.lobby.worldSessions.TryGetValue(rid, out var r)) return r;

        RainMeadow.RainMeadow.Error("Resource ACTUALLY not found: " + rid);
        return null;
    }

}
