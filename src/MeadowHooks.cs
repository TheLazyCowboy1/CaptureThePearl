using MonoMod.RuntimeDetour;
using RainMeadow;
using RainMeadow.UI.Components;
using System;
using System.Linq;
using UnityEngine;

namespace CaptureThePearl;

/// <summary>
/// Hooks on Rain Meadow itself. Hopefully Lazy will handle all of this.
/// </summary>
public static class MeadowHooks
{
    public static void ApplyHooks()
    {
        deathScreenRPCHook = new Hook( //don't go to death screen unless I allow it
            typeof(StoryRPCs).GetMethod(nameof(StoryRPCs.GoToDeathScreen)),
            StoryRPCs_GoToDeathScreen
            );
        leaveLobbyHook = new Hook( //remove hooks when leaving lobby
            typeof(OnlineManager).GetMethod(nameof(OnlineManager.LeaveLobby)),
            OnlineManager_LeaveLobby
            );
        chatMessageHook = new Hook( //filter messages from other teams in chat
            typeof(ChatHud).GetMethod(nameof(ChatHud.AddMessage)),
            ChatHud_AddMessage
            );
        playerNameMessageHook = new Hook( //filter messages from other teams above players
            typeof(RPCs).GetMethod(nameof(RPCs.UpdateUsernameTemporarily)),
            RPCs_UpdateUsernameTemporarily
            );
        chatTutorialHook = new Hook( //update chat tutorial
            typeof(ChatHud).GetConstructors()[0],
            ChatHud_ctor
            );
        //change chat color in chat menu
        chatColourHook = new Hook(typeof(ChatLogOverlay).GetMethod(nameof(ChatLogOverlay.UpdateLogDisplay)), ChatLogOverlay_UpdateLogDisplay);
        //remove spectate option for other teams
        spectateButtonHook = new Hook(typeof(SpectatorOverlay).GetMethod(nameof(SpectatorOverlay.Update)), SpectatorOverlay_Update);
        //update playerDisplay colors (e.g: player name colors)
        playerDisplayHook = new Hook(typeof(OnlinePlayerDisplay).GetMethod(nameof(OnlinePlayerDisplay.Draw)), OnlinePlayerDisplay_Draw);

        RainMeadow.RainMeadow.Debug("[CTP]: Applied Rain Meadow hooks");
    }

    private static Hook deathScreenRPCHook, leaveLobbyHook,
        chatMessageHook, playerNameMessageHook, chatTutorialHook,
        playerDisplayHook, chatColourHook, spectateButtonHook;

    public static void RemoveHooks()
    {
        deathScreenRPCHook?.Undo();
        leaveLobbyHook?.Undo();
        chatMessageHook?.Undo();
        playerNameMessageHook?.Undo();
        chatTutorialHook?.Undo();
        playerDisplayHook?.Undo();
        spectateButtonHook?.Undo();
        chatColourHook?.Undo();
    }

    //Don't go to death screen while in the Capture the Pearl gamemode!!
    //This probably ought to go in CTPGameHooks, but I'm keeping it here to keep all the Meadow and Rain World stuff separated.
    private delegate void EmptyDelegate();
    private static void StoryRPCs_GoToDeathScreen(EmptyDelegate orig)
    {
        //if (CTPGameMode.IsCTPGameMode(out var _)) return;
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
        if (!ChatLogManager.shownChatTutorial)
        {
            //show custom chat tutorial message
            hud.textPrompt.AddMessage(hud.rainWorld.inGameTranslator.Translate("Press '") + (RainMeadow.RainMeadow.rainMeadowOptions.ChatButtonKey.Value) + hud.rainWorld.inGameTranslator.Translate("' to chat, press '") + (RainMeadow.RainMeadow.rainMeadowOptions.ChatLogKey.Value) + hud.rainWorld.inGameTranslator.Translate("' to toggle the chat log") + ", prefix messages with + to send to all teams", 60, 320, true, true);
            ChatLogManager.shownChatTutorial = true;
        }

        orig(self, hud, camera);
    }


    private delegate void UpdateLogDisplay_orig(ChatLogOverlay self);
    private static void ChatLogOverlay_UpdateLogDisplay(UpdateLogDisplay_orig orig, ChatLogOverlay self)
    {
        int oldLength = self.scroller.subObjects.Count;

        orig(self);

        if (CTPGameMode.IsCTPGameMode(out var gamemode))
        {
            Color color = Color.white;
            for (int i = oldLength; i < self.scroller.subObjects.Count; i++) //loop through NEW subobjects
            {
                if (self.scroller.subObjects[i] is UsernameMenuLabel userLabel) //look for username labels
                {
                    var playerInfo = gamemode.PlayerTeams.FirstOrDefault(p => p.Key.id.name == userLabel.text); //match them to players
                    if (playerInfo.Key != null)
                    {
                        color = Color.Lerp(Color.white, CTPGameMode.GetTeamColor(playerInfo.Value), 0.5f); //whiten the color a bit for readability
                        if (userLabel.subObjects.Last() is AlignedMenuLabel messageLabel)
                            messageLabel.label.color = color; //set message label color next to username
                    }
                    else
                        RainMeadow.RainMeadow.Error($"[CTP]: Could not find player {userLabel.text} in team player list");
                }
                else if (self.scroller.subObjects[i] is AlignedMenuLabel messageLabel)
                {
                    if (messageLabel.label.color != ChatLogManager.defaultSystemColor)
                        messageLabel.label.color = color; //set color to the color of whatever player was last found in the list
                }
            }
        }

    }

    private delegate void SpectatorOverlay_Update_orig(SpectatorOverlay self);
    private static void SpectatorOverlay_Update(SpectatorOverlay_Update_orig orig, SpectatorOverlay self)
    {
        orig(self);

        foreach (var button in self.PlayerButtons)
        {
            if (CTPGameMode.IsCTPGameMode(out var gamemode) && !gamemode.OnMyTeam(button.player))
                button.buttonBehav.greyedOut = true; //grey out spectate button for players on other team
        }
    }

    public delegate void OnlinePlayerDisplay_Draw_orig(OnlinePlayerDisplay self, float tStacker);
    public static void OnlinePlayerDisplay_Draw(OnlinePlayerDisplay_Draw_orig orig, OnlinePlayerDisplay self, float tStacker)
    {
        orig(self, tStacker);

        if (!CTPGameMode.IsCTPGameMode(out var mode)) return;

        if (mode.PlayerTeams.ContainsKey(self.player))
        {
            self.color = CTPGameMode.GetTeamColor(mode.PlayerTeams[self.player]);
            self.lighter_color = self.color;


            //recolour everything ahhhhhhhhhh
            self.arrowSprite.color = self.color;
            self.gradient.color = self.color;
            foreach (var msgLbl in self.messageLabels) msgLbl.color = Color.Lerp(Color.white, self.color, 0.5f);
            self.slugIcon.color = self.color;
            self.username.color = self.color;
        }
    }

}
