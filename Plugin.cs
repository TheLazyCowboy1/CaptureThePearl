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

}
