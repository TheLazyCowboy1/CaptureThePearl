using System;
using System.Collections.Generic;
using System.Linq;
using RainMeadow;
using Menu.Remix.MixedUI;
using UnityEngine;
using Menu.Remix;
using Menu;
using System.Globalization;
using RWCustom;
using Menu.Remix.MixedUI.ValueTypes;

namespace CaptureThePearl;

/// <summary>
/// Note: It literally copies off of the Story menu.
/// </summary>
public class CTPMenu : StoryOnlineMenu
{
    //public OpComboBox2 RegionDropdownBox;
    public OpComboBox RegionDropdownBox;
    public Configurable<string> regionConfig;// = new("SU");
    public OpUpdown TeamUpdown;
    public Configurable<int> teamConfig;// = new(2, new ConfigAcceptableRange<int>(2, 10));
    public OpUpdown TimerUpdown;
    public Configurable<int> timerConfig;// = new(10, new ConfigAcceptableRange<int>(1, 30));
    public OpCheckBox CreatureCheckbox;
    public Configurable<bool> creaturesConfig;// = new(true);

    private MenuTabWrapper tabWrapper; //what on earth is this mess...

    public CTPGameMode gameMode => storyGameMode as CTPGameMode;

    private static string lastRegion = "SU";

    public CTPMenu(ProcessManager manager) : base(manager)
    {
        RainMeadow.RainMeadow.Debug("[CTP]: Setting up menu");

        tabWrapper = new MenuTabWrapper(this, pages[0]);
        pages[0].subObjects.Add(tabWrapper);

        //remove "match save" option
        RemoveMenuObject(clientWantsToOverwriteSave);
        RemoveMenuObject(restartCheckbox);


        //make scug saves fresh, WORKS BUT NEEDD TO FIX LAYERING
        if (OnlineManager.lobby.isOwner) //messes up client menu
        {
            for (int k = 0; k < slugcatPages.Count; k++) this.pages.Remove(this.slugcatPages[k]);
            slugcatPages.Clear();
            redIsDead = false;
            artificerIsDead = false;
            saintIsDead = false;
            for (int j = 0; j < slugcatColorOrder.Count; j++)
            {
                slugcatPages.Add(new SlugcatSelectMenu.SlugcatPageNewGame(this, null, 1 + j, slugcatColorOrder[j]));
                pages.Add(slugcatPages[j]);
            }
        }

        previousPageIdx = slugcatPageIndex;

        storyGameMode = (CTPGameMode)OnlineManager.lobby.gameMode;

        storyGameMode.Sanitize();
        gameMode.SanitizeCTP();
        storyGameMode.currentCampaign = slugcatPages[slugcatPageIndex].slugcatNumber;


        //add region dropdowns
        regionConfig = new(storyGameMode.region == null ? lastRegion : storyGameMode.region);
        teamConfig = new(gameMode.NumberOfTeams, new ConfigAcceptableRange<int>(2, 4)); //cap at 4 teams
        timerConfig = new(gameMode.TimerLength, new ConfigAcceptableRange<int>(1, 30));
        creaturesConfig = new(gameMode.SpawnCreatures);
        SetupRegionDropdown();


        //To-do: Dropdown for slugcats for host
    }

    private int previousPageIdx;
    private string previousRegion = "";
    public override void Update()
    {
        base.Update();

        if (OnlineManager.lobby.isOwner) //host update stuff
        {
            if (RegionDropdownBox == null)
                SetupRegionDropdown();

            UpdateConfigs();

            //Update region dropdown list
            if (slugcatPageIndex != previousPageIdx)
            {
                var oldItems = RegionDropdownBox._itemList;
                var newItems = GetRegionList(slugcatPages[slugcatPageIndex].slugcatNumber);
                RegionDropdownBox.RemoveItems(true, oldItems.Except(newItems).Select(item => item.name).ToArray());
                RegionDropdownBox.AddItems(true, newItems.Except(oldItems).ToArray());
                previousPageIdx = slugcatPageIndex;
            }

            //Set start text to always be "NEW SESSION"
            startButton.menuLabel.text = Translate("NEW SESSION");

        }
        else //client update stuff
        {
            //Change background if host changes region or client changes slugcat
            if (storyGameMode.region != previousRegion || previousPageIdx != slugcatPageIndex)
            {
                ChangePageBackground();
            }
            if (onlineDifficultyLabel != null)
            {
                onlineDifficultyLabel.text = GetCurrentCampaignName() + (string.IsNullOrEmpty(storyGameMode.region) ? Translate(" - Unknown Region") : " - " + Translate(Region.GetRegionFullName(storyGameMode.region, storyGameMode.currentCampaign)));
            }

            //set custom settings
            RegionDropdownBox.value = storyGameMode.region;
            TeamUpdown.SetValueInt(gameMode.NumberOfTeams);
            TimerUpdown.SetValueInt(gameMode.TimerLength);
            CreatureCheckbox.SetValueBool(gameMode.SpawnCreatures);
        }
    }

    public void SetupRegionDropdown()
    {
        //if (!OnlineManager.lobby.isOwner) return;

        RegionDropdownBox = new(
                regionConfig,
                this.nextButton.pos + new Vector2(0, 300),
                180,
                GetRegionList(slugcatPages[slugcatPageIndex].slugcatNumber)
                );
        RegionDropdownBox.description = "\n\nThe region in which to play.";
        RegionDropdownBox.greyedOut = !OnlineManager.lobby.isOwner;
        //RegionDropdownBox.OnChange += UpdateConfigs;
        new UIelementWrapper(tabWrapper, RegionDropdownBox);
        pages[0].subObjects.Add(new MenuLabel(this, pages[0], "Region:", RegionDropdownBox.pos + new Vector2(0, 20f), new Vector2(100f, 30f), false));

        TeamUpdown = new(teamConfig, this.nextButton.pos + new Vector2(0, 350), 60);
        TeamUpdown.description = "\n\nThe number of teams to use. Make sure there are more shelters than teams!!";
        TeamUpdown.greyedOut = !OnlineManager.lobby.isOwner;
        //TeamUpdown.OnChange += UpdateConfigs;
        new UIelementWrapper(tabWrapper, TeamUpdown);
        pages[0].subObjects.Add(new MenuLabel(this, pages[0], "Teams:", TeamUpdown.pos + new Vector2(-30, 25), new Vector2(100f, 30f), false));

        TimerUpdown = new(timerConfig, this.nextButton.pos + new Vector2(100, 350), 60);
        TimerUpdown.description = "\n\nThe length of the game in minutes.";
        TimerUpdown.greyedOut = !OnlineManager.lobby.isOwner;
        //TimerUpdown.OnChange += UpdateConfigs;
        new UIelementWrapper(tabWrapper, TimerUpdown);
        pages[0].subObjects.Add(new MenuLabel(this, pages[0], "Timer:", TimerUpdown.pos + new Vector2(-30, 25), new Vector2(100f, 30f), false));

        //CreatureCheckbox = new(this, pages[0], this, this.nextButton.pos + new Vector2(100, 400), 100f, "Creatures?", "CreatureCheckbox");//new(creaturesConfig, this.nextButton.pos + new Vector2(0, 450));
        CreatureCheckbox = new(creaturesConfig, this.nextButton.pos + new Vector2(100, 400));
        CreatureCheckbox.description = "\n\nWhether creatures should spawn in the world.";
        CreatureCheckbox.greyedOut = !OnlineManager.lobby.isOwner;
        //CreatureCheckbox.Checked = gameMode.SpawnCreatures;
        //if (OnlineManager.lobby.isOwner) CreatureCheckbox.selectable = true;
        //pages[0].subObjects.Add(CreatureCheckbox);
        //CreatureCheckbox.Checked = gameMode.SpawnCreatures;
        //if (gameMode.SpawnCreatures) CreatureCheckbox.Clicked();
        //CreatureCheckbox.OnChange += UpdateConfigs;
        new UIelementWrapper(tabWrapper, CreatureCheckbox);
        pages[0].subObjects.Add(new MenuLabel(this, pages[0], "Creatures?", CreatureCheckbox.pos + new Vector2(-100f, 0), new Vector2(100f, 30f), false));
    }

    public void UpdateConfigs()
    {
        storyGameMode.region = RegionDropdownBox.value;

        try
        {
            //gameMode.NumberOfTeams = (byte)TeamUpdown.GetValueInt();
            if (Int32.TryParse(TeamUpdown.value, out int teams)) gameMode.NumberOfTeams = (byte)teams;
            //gameMode.TimerLength = TimerUpdown.GetValueInt();
            if (Int32.TryParse(TimerUpdown.value, out int timer)) gameMode.TimerLength = timer;
            //gameMode.SpawnCreatures = CreatureCheckbox.GetValueBool();
            gameMode.SpawnCreatures = CreatureCheckbox.value == "true";
        }
        catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }

        //these configs have been giving me no end to trouble
        /*byte teams = (byte)TeamUpdown.valueInt;
        if (teams != null) gameMode.NumberOfTeams = teams;
        int timer = TimerUpdown.valueInt;
        if (timer != null) gameMode.TimerLength = timer;
        bool creatures = CreatureCheckbox.GetValueBool();
        if (creatures != null) gameMode.SpawnCreatures = creatures;*/

        //teamConfig.BoxedValue = ValueConverter.ConvertToValue(TeamUpdown.value, teamConfig.settingType);
        //gameMode.NumberOfTeams = (byte)teamConfig.Value;//(byte)TeamUpdown.GetValueInt();
        //if (gameMode.NumberOfTeams < 2) gameMode.NumberOfTeams = 2;
        //if (gameMode.NumberOfTeams > 10) gameMode.NumberOfTeams = 10;
        //timerConfig.BoxedValue = ValueConverter.ConvertToValue(TimerUpdown.value, timerConfig.settingType);
        //gameMode.TimerLength = timerConfig.Value;//TimerUpdown.GetValueInt();
        //if (gameMode.TimerLength < 1) gameMode.TimerLength = 1;
        //if (gameMode.TimerLength > 30) gameMode.TimerLength = 30;
        //gameMode.SpawnCreatures = CreatureCheckbox.Checked;
        //if (gameMode.SpawnCreatures) gameMode.SpawnCreatures = true;
    }

    public override void ShutDownProcess()
    {
        base.ShutDownProcess();

        lastRegion = gameMode.region; //so that if we end a round, it tries to keep the same region selected
        return;

        //This is OBVIOUSLY not ideal: I want clients to be able to see the settings change AS the host changes them.
        //But it just kept throwing errors. Very annoying. This works functionally, at least.
        gameMode.NumberOfTeams = (byte)TeamUpdown.GetValueInt();
        gameMode.TimerLength = TimerUpdown.GetValueInt();
        gameMode.SpawnCreatures = CreatureCheckbox.GetValueBool();
        RainMeadow.RainMeadow.Debug("[CTP]: Menu set configs!");
    }

    private List<ListItem> GetRegionList(SlugcatStats.Name slugcat)
    {
        List<ListItem> list = new();
        //var regions = Region.GetFullRegionOrder();
        //var regions = Region.LoadAllRegions(slugcat);
        var regions = SlugcatStats.SlugcatStoryRegions(slugcat)
            .Union(SlugcatStats.SlugcatOptionalRegions(slugcat))
            .ToArray();
        for (int i = 0; i < regions.Length; i++)
        {
            string reg = Region.GetProperRegionAcronym(SlugcatStats.SlugcatToTimeline(slugcat), regions[i]);
            list.Add(new(reg, Region.GetRegionFullName(reg, slugcat), i));
        }
        return list;
    }

    private FSprite backgroundSprite;
    public void ChangePageBackground()
    {
        //RainMeadow.RainMeadow.Debug($"[CTP]: Attempting background change for {storyGameMode.region}");
        if (OnlineManager.lobby.isOwner) return; //owner gets default background scene
        if (string.IsNullOrEmpty(storyGameMode.region)) return; //don't process false regions
        try
        {
            var page = slugcatPages[slugcatPageIndex];
            //remove sprites
            if (page.slugcatImage != null)
            {
                page.RemoveSubObject(page.slugcatImage);
                page.slugcatImage.RemoveSprites();
            }

            var scene = new InteractiveMenuScene(this, page, Region.GetRegionLandscapeScene(storyGameMode.region));
            scene.flatMode = true;
            scene.BuildScene(); //rebuild, but flat this time!!!!!
            if (ModManager.MSC) scene.BuildMSCScene();

            if (scene.flatIllustrations.Count < 1)
            {
                RainMeadow.RainMeadow.Debug("Couldn't find flat illustration!!!");
                return;
            }

            backgroundSprite?.RemoveFromContainer(); //remove its old self from its container

            backgroundSprite = scene.flatIllustrations[0].sprite;
            scene.flatIllustrations.Clear(); //remove its references to my stuff!!!!

            backgroundSprite.alpha = 0.5f;
            backgroundSprite.scale = 0.7f;
            backgroundSprite.x = this.manager.rainWorld.options.ScreenSize.x * 0.5f;
            backgroundSprite.y = this.manager.rainWorld.options.ScreenSize.y * 0.7f;

            //page.Container.AddChild(backgroundSprite);
            page.Container.AddChildAtIndex(backgroundSprite, 0);

            previousRegion = storyGameMode.region;
            previousPageIdx = slugcatPageIndex;

            RainMeadow.RainMeadow.Debug($"[CTP]: Changed background region scene to {previousRegion}.");
        } 
        catch (Exception ex) 
        {
            RainMeadow.RainMeadow.Error(ex); 
        }
    }
    /*
    //HOOK TO SCUGSELECTMENU STARTGAME AND USE THIS INSTEAD IF ITS CTP MODE
    public new void StartGame(SlugcatStats.Name storyGameCharacter)
    {
        if (OnlineManager.lobby.isOwner)
        {
            personaSettings.playingAs = slugcatColorOrder[hostSlugIndex];
        }

        if (this.colorChecked)
        {
            List<Color> val = new();
            for (int i = 0; i < manager.rainWorld.progression.miscProgressionData.colorChoices[slugcatColorOrder[hostSlugIndex].value].Count; i++)
            {
                Vector3 vector = new Vector3(1f, 1f, 1f);
                if (manager.rainWorld.progression.miscProgressionData.colorChoices[slugcatColorOrder[hostSlugIndex].value][i].Contains(","))
                {
                    string[] array = manager.rainWorld.progression.miscProgressionData.colorChoices[slugcatColorOrder[hostSlugIndex].value][i].Split(new char[1] { ',' });
                    vector = new Vector3(float.Parse(array[0], (NumberStyles)511, (IFormatProvider)(object)CultureInfo.InvariantCulture),
                        float.Parse(array[1], (NumberStyles)511, (IFormatProvider)(object)CultureInfo.InvariantCulture), float.Parse(array[2],
                        (NumberStyles)511, (IFormatProvider)(object)CultureInfo.InvariantCulture));
                }
                val.Add(RWCustom.Custom.HSL2RGB(vector[0], vector[1], vector[2]));
            }

            personaSettings.currentColors = val;
        }
        else
        {
            // Use the default colors for this slugcat when the checkbox is unchecked
            personaSettings.currentColors = PlayerGraphics.DefaultBodyPartColorHex(slugcatColorOrder[hostSlugIndex]).Select(Custom.hexToColor).ToList();
        }
        manager.arenaSitting = null;

        //manager.rainWorld.progression.WipeSaveState(storyGameMode.currentCampaign);//ALWAYS load a new game
        manager.menuSetup.startGameCondition = ProcessManager.MenuSetup.StoryGameInitCondition.New;

        manager.RequestMainProcessSwitch(ProcessManager.ProcessID.Game);
    }
    */
    private static void RemoveMenuObject(MenuObject obj)
    {
        if (obj != null)
        {
            if (obj is CheckBox cb) { cb.Checked = false; cb.selectable = false; }
            obj.RemoveSprites();
            obj.inactive = true;
        }
    }
}
