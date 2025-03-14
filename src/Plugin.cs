using System;
using System.Security;
using System.Security.Permissions;
using BepInEx;
using RainMeadow;

#pragma warning disable CS0618

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace CaptureThePearl; //rename this with ctrl + r

//dependencies:
//Rain Meadow:
[BepInDependency("henpemaz.rainmeadow", BepInDependency.DependencyFlags.HardDependency)]


[BepInPlugin(MOD_ID, MOD_NAME, MOD_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string MOD_ID = "LazyCowboy.CaptureThePearl";
    public const string MOD_NAME = "Capture the Pearl";
    public const string MOD_VERSION = "0.0.1";

    //made static for easy access. Hopefully this mod should never be initiated twice anyway...
    public static ConfigOptions Options;

    public Plugin()
    {
        try
        {
            Options = new ConfigOptions(Logger);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            throw;
        }
    }
    private void OnEnable()
    {
        On.RainWorld.OnModsInit += RainWorld_OnModsInit;
    }
    private void OnDisable()
    {
        //Remove hooks
        On.RainWorld.OnModsInit -= RainWorld_OnModsInit;

        if (IsInit)
        {
            On.ProcessManager.PostSwitchMainProcess -= ProcessManager_PostSwitchMainProcess;

            On.PlayerProgression.IsThereASavedGame -= PlayerProgression_IsThereASavedGame;
            On.Menu.SlugcatSelectMenu.ContinueStartedGame -= CTPMenu_ContinueStartedGame;

            IsInit = false;
        }
    }

    private bool IsInit;
    private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);
        try
        {
            if (IsInit) return; //prevents adding hooks twice

            //set up ExtEnums first!!!
            SetupExtEnums();

            MeadowHooks.ApplyHooks(Logger);
            On.ProcessManager.PostSwitchMainProcess += ProcessManager_PostSwitchMainProcess;

            //these are not in CTPGameHooks is because they affect the select menu
            On.PlayerProgression.IsThereASavedGame += PlayerProgression_IsThereASavedGame;
            On.Menu.SlugcatSelectMenu.ContinueStartedGame += CTPMenu_ContinueStartedGame;

            MachineConnector.SetRegisteredOI(MOD_ID, Options);
            IsInit = true;

            Logger.LogDebug("Hooks added!");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            throw;
        }
    }

    private void ProcessManager_PostSwitchMainProcess(On.ProcessManager.orig_PostSwitchMainProcess orig, ProcessManager self, ProcessManager.ProcessID ID)
    {
        if (ID == CTPMenuProcessID) self.currentMainLoop = new CTPMenu(self);
        orig(self, ID);
    }

    public static ProcessManager.ProcessID CTPMenuProcessID;
    public static MeadowGameMode.OnlineGameModeType CTPGameModeType;

    public void SetupExtEnums()
    {
        CTPMenuProcessID = new ProcessManager.ProcessID("CaptureThePearlMenu", true);

        CTPGameModeType = new MeadowGameMode.OnlineGameModeType("Capture the Pearl", true);
        //MeadowGameMode.gamemodes.Add(CTPGameModeType, typeof(CTPGameMode));
        MeadowGameMode.RegisterType(CTPGameModeType, typeof(CTPGameMode), CTPGameMode.GameModeDescription);

    }


    private static bool PlayerProgression_IsThereASavedGame(On.PlayerProgression.orig_IsThereASavedGame orig, PlayerProgression self, SlugcatStats.Name saveStateNumber)
    {
        if (CTPGameMode.IsCTPGameMode(out var _)) //this prevents the game from starting dream sequences
        {
            Debug("Overriding isSaveGame to true");
            return true;
        }
        return orig(self, saveStateNumber);
    }
    private static void CTPMenu_ContinueStartedGame(On.Menu.SlugcatSelectMenu.orig_ContinueStartedGame orig, Menu.SlugcatSelectMenu self, SlugcatStats.Name storyGameCharacter)
    {
        if (self.ID == CTPMenuProcessID)
        {
            Debug("Avoiding potential statistics menu detour");

            self.manager.menuSetup.startGameCondition = ProcessManager.MenuSetup.StoryGameInitCondition.Load;
            self.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.Game);
            self.PlaySound(SoundID.MENU_Continue_Game);
        }
        else orig(self, storyGameCharacter);
    }


    public static void Debug(object obj) => RainMeadow.RainMeadow.Debug($"[CTP]: {obj}");

}
