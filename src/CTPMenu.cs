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
using System.IO;
using System.Threading.Tasks;

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
    private static SlugcatStats.Name lastSlugcat = null;
    private string newSessionText = "NEW SESSION";
    private string clientDescription = "ERROR LOADING LOBBY: PLEASE WAIT";

    public CTPMenu(ProcessManager manager) : base(manager)
    {
        RainMeadow.RainMeadow.Debug("[CTP]: Setting up menu");

        if (lastSlugcat != null) //set the page to the one I previously started on
        {
            int idx = this.slugcatColorOrder.IndexOf(lastSlugcat);
            if (idx >= 0) this.slugcatPageIndex = idx;
        }

        tabWrapper = new MenuTabWrapper(this, pages[0]);
        pages[0].subObjects.Add(tabWrapper);

        //remove "match save" option
        //RemoveMenuObject(base.clientWantsToOverwriteSave); //this has been renamed, so I'm not sure what this is removing
        RemoveMenuObject(restartCheckbox);
        newSessionText = Translate("NEW SESSION");

        //make scug saves fresh, WORKS BUT NEEDD TO FIX LAYERING
        if (OnlineManager.lobby.isOwner) //messes up client menu
        {
            for (int k = 0; k < slugcatPages.Count; k++)
            {
                slugcatPages[k]?.RemoveSprites(); //otherwise can leave annoying remnants
                this.pages.Remove(this.slugcatPages[k]);
                slugcatPages[k] = null;
            }
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
        timerConfig = new(gameMode.TimerLength, new ConfigAcceptableRange<int>(1, 60));
        creaturesConfig = new(gameMode.SpawnCreatures);
        SetupCustomUIElements();


        //To-do: Dropdown for slugcats for host
    }

    private int previousPageIdx;
    private string previousRegion = "";
    private Task dropdownUpdateTask;
    public override void Update()
    {
        base.Update();

        if (OnlineManager.lobby.isOwner) //host update stuff
        {
            //Update region dropdown list
            //  made into a Task because apparently it is very slow (why??) and it was restarting while still active (HOW????)
            if (slugcatPageIndex != previousPageIdx && dropdownUpdateTask == null)
            {
                dropdownUpdateTask = Task.Run(() =>
                {
                    var idx = slugcatPageIndex;
                    try
                    {
                        var oldItems = RegionDropdownBox._itemList;
                        var newItems = GetRegionList(slugcatPages[idx].slugcatNumber);
                        RegionDropdownBox.RemoveItems(true, oldItems.Except(newItems).Select(item => item.name).ToArray());
                        RegionDropdownBox.AddItems(true, newItems.Except(oldItems).ToArray());
                    }
                    catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
                    previousPageIdx = idx;

                    RainMeadow.RainMeadow.Debug($"[CTP]: Updated region dropdown list for {idx} - {slugcatPages[slugcatPageIndex].slugcatNumber}");
                    dropdownUpdateTask = null; //clear itself out
                });
            }

            SetGreyedOutConfigs(dropdownUpdateTask != null); //grey out configs if they're being reset; otherwise ensure not greyed out
            if (this.scroll == 0 && this.lastScroll == 0 && dropdownUpdateTask == null) //don't ask; just trust
                UpdateConfigs();

            //Set start text to always be "NEW SESSION"
            startButton.menuLabel.text = newSessionText;

        }
        else //client update stuff
        {
            //Change background if host changes region or client changes slugcat
            if ((storyGameMode.region != previousRegion || previousPageIdx != slugcatPageIndex))
            {
                ChangePageBackground();
                clientDescription = GetCurrentCampaignName() + (string.IsNullOrEmpty(storyGameMode.region) ? Translate(" - Unknown Region") : " - " + Translate(Region.GetRegionFullName(storyGameMode.region, storyGameMode.currentCampaign)));

                //ensure the new region selected is actually in my region list
                if (!RegionDropdownBox._itemList.Any(item => item.name == storyGameMode.region))
                    RegionDropdownBox.AddItems(false, new ListItem(storyGameMode.region, Region.GetRegionFullName(storyGameMode.region, storyGameMode.currentCampaign)));
            }
            //if (base.onlineDifficultyLabel != null)
                //base.onlineDifficultyLabel.text = clientDescription;

            //set custom settings
            RegionDropdownBox.value = storyGameMode.region;
            TeamUpdown.SetValueInt(gameMode.NumberOfTeams);
            TimerUpdown.SetValueInt(gameMode.TimerLength);
            CreatureCheckbox.SetValueBool(gameMode.SpawnCreatures);
        }
    }

    public void SetupCustomUIElements()
    {
        //if (!OnlineManager.lobby.isOwner) return;
        RainMeadow.RainMeadow.Debug("[CTP]: Setting up custom UI elements.");

        RegionDropdownBox = new(
                regionConfig,
                this.nextButton.pos + new Vector2(0, 300),
                180,
                GetRegionList(slugcatPages[slugcatPageIndex].slugcatNumber)
                );
        RegionDropdownBox.description = "\n\nThe region in which to play.";
        //RegionDropdownBox.OnChange += UpdateConfigs;
        new UIelementWrapper(tabWrapper, RegionDropdownBox);
        pages[0].subObjects.Add(new MenuLabel(this, pages[0], "Region:", RegionDropdownBox.pos + new Vector2(0, 20f), new Vector2(100f, 30f), false));

        TeamUpdown = new(teamConfig, this.nextButton.pos + new Vector2(0, 350), 60);
        TeamUpdown.description = "\n\nThe number of teams to use. Make sure there are more shelters than teams!!";
        //TeamUpdown.OnChange += UpdateConfigs;
        new UIelementWrapper(tabWrapper, TeamUpdown);
        pages[0].subObjects.Add(new MenuLabel(this, pages[0], "Teams:", TeamUpdown.pos + new Vector2(-30, 25), new Vector2(100f, 30f), false));

        TimerUpdown = new(timerConfig, this.nextButton.pos + new Vector2(100, 350), 60);
        TimerUpdown.description = "\n\nThe length of the game in minutes.";
        //TimerUpdown.OnChange += UpdateConfigs;
        new UIelementWrapper(tabWrapper, TimerUpdown);
        pages[0].subObjects.Add(new MenuLabel(this, pages[0], "Timer:", TimerUpdown.pos + new Vector2(-30, 25), new Vector2(100f, 30f), false));

        //CreatureCheckbox = new(this, pages[0], this, this.nextButton.pos + new Vector2(100, 400), 100f, "Creatures?", "CreatureCheckbox");//new(creaturesConfig, this.nextButton.pos + new Vector2(0, 450));
        CreatureCheckbox = new(creaturesConfig, this.nextButton.pos + new Vector2(100, 400));
        CreatureCheckbox.description = "\n\nWhether creatures should spawn in the world.";
        //CreatureCheckbox.Checked = gameMode.SpawnCreatures;
        //if (OnlineManager.lobby.isOwner) CreatureCheckbox.selectable = true;
        //pages[0].subObjects.Add(CreatureCheckbox);
        //CreatureCheckbox.Checked = gameMode.SpawnCreatures;
        //if (gameMode.SpawnCreatures) CreatureCheckbox.Clicked();
        //CreatureCheckbox.OnChange += UpdateConfigs;
        new UIelementWrapper(tabWrapper, CreatureCheckbox);
        pages[0].subObjects.Add(new MenuLabel(this, pages[0], "Creatures?", CreatureCheckbox.pos + new Vector2(-100f, 0), new Vector2(100f, 30f), false));

        SetGreyedOutConfigs(!storyGameMode.lobby.isOwner);
    }
    public void SetGreyedOutConfigs(bool greyed)
    {
        RegionDropdownBox.greyedOut = greyed;
        TeamUpdown.greyedOut = greyed;
        TimerUpdown.greyedOut = greyed;
        CreatureCheckbox.greyedOut = greyed;
    }

    public void UpdateConfigs()
    {
        try
        {
            storyGameMode.region = RegionDropdownBox.value;
            if (TeamUpdown.value != gameMode.NumberOfTeams.ToString() && Int32.TryParse(TeamUpdown.value, out int teams)) gameMode.NumberOfTeams = (byte)teams;
            if (TimerUpdown.value != gameMode.TimerLength.ToString() && Int32.TryParse(TimerUpdown.value, out int timer)) gameMode.TimerLength = timer;
            gameMode.SpawnCreatures = CreatureCheckbox.value == "true";
        }
        catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
    }

    public override void ShutDownProcess()
    {
        lastRegion = storyGameMode.region; //so that if we end a round, it tries to keep the same region selected
        lastSlugcat = storyGameMode.currentCampaign; //tries to keep same slugcat
        UpdateConfigs();

        base.ShutDownProcess();
    }

    private List<ListItem> GetRegionList(SlugcatStats.Name slugcat)
    {
        List<ListItem> list = new();
        //var regions = Region.GetFullRegionOrder();
        //var regions = Region.LoadAllRegions(slugcat);
        var regions = PrioritizeRegions(
            SlugcatStats.SlugcatStoryRegions(slugcat)
            .Union(SlugcatStats.SlugcatOptionalRegions(slugcat))
            .Union(SpecialIncludedRegions(slugcat)));
            //.ToArray();
        for (int i = 0; i < regions.Length; i++)
        {
            string reg = Region.GetProperRegionAcronym(SlugcatStats.SlugcatToTimeline(slugcat), regions[i]);
            list.Add(new(reg, Region.GetRegionFullName(reg, slugcat), i));
        }
        return list;
    }
    private string[] SpecialIncludedRegions(SlugcatStats.Name slugcat)
    {
        if (slugcat == Watcher.WatcherEnums.SlugcatStatsName.Watcher)
            return new string[]
            {
                "WARB", //Salination
                "WARC", //Fetid Glen
                "WARD", //Cold Storage
                "WARE", //Heat Ducts
                "WARF", //Aether Ridge
                "WARG", //"Surface" or something, idk?
                "WBLA", //Badlands
                //"WDSR", //bad Drainage System //probably too small and annoying
                "WGWR", //bad Garbage Wastes
                "WHIR", //bad Industrial Complex
                //"WPTA" //Signal Spires //as cool as this would be... too spoilery, and it probably wouldn't be fun in practice
                "WRFA", //Coral Caves
                "WRFB", //Turbulent Pump
                "WRRA", //Rusted Wrecks
                "WSKA", //Torrential Railways
                "WSKB", //Sunlit Port //NEEDS SPECIAL SHELTERS
                "WSKC", //Stormy Coast
                "WSKD", //Shrouded Coast
                "WSUR", //bad Outskirts warp
                "WTDA", //Torrid Desert
                "WTDB", //Desolate Tract
                "WVWA" //Verdant Waterways
            };
        return new string[0];
    }
    //Partially sorts the region list, in order to make more fun regions easier to find
    private static string[] PrioritizeRegions(IEnumerable<string> en)
    {
        string[] regionPriorities = new string[]
        {
            "SU", //Outskirts
            "HI", //High Industrial
            "MS", //Bitter Aerie
            "WSKB" //Sunlit Port
        };
        string[] discouragedRegions = new string[]
        {
            "DM", //Looks to the Moon, simply because it's way too big and would probably crash everyone with lag
            "UW" //The Exterior, because it probably just won't be fun, lol
        };

        var list = en.ToList();

        //start from the least important; grab the region (if it's in the list) and move it to the front
        for (int i = regionPriorities.Length - 1; i >= 0; i--)
        {
            int idx = list.IndexOf(regionPriorities[i]);
            if (idx > 0)
            {
                string r = list[idx];
                list.RemoveAt(idx);
                list.Insert(0, r); //insert at start
            }
        }
        for (int i = discouragedRegions.Length - 1; i >= 0; i--)
        {
            int idx = list.IndexOf(discouragedRegions[i]);
            if (idx > 0)
            {
                string r = list[idx];
                list.RemoveAt(idx);
                list.Add(r); //add to the end
            }
        }

        return list.ToArray();
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

            backgroundSprite?.RemoveFromContainer(); //remove its old self from its container

            backgroundSprite = GetRegionSprite();

            if (backgroundSprite != null)
            {
                backgroundSprite.alpha = 0.5f;
                backgroundSprite.scale *= 0.7f;
                backgroundSprite.x = this.manager.rainWorld.options.ScreenSize.x * 0.5f;
                backgroundSprite.y = this.manager.rainWorld.options.ScreenSize.y * 0.7f;

                //page.Container.AddChild(backgroundSprite);
                page.Container.AddChildAtIndex(backgroundSprite, 0);

                RainMeadow.RainMeadow.Debug($"[CTP]: Changed background region scene to {storyGameMode.region}.");
            }
            else
                RainMeadow.RainMeadow.Debug($"[CTP]: Failed to load background for {storyGameMode.region}.");

            previousRegion = storyGameMode.region;
            previousPageIdx = slugcatPageIndex;
        } 
        catch (Exception ex) 
        {
            RainMeadow.RainMeadow.Error(ex); 
        }
    }
    private FSprite GetRegionSprite()
    {
        var scene = new InteractiveMenuScene(this, slugcatPages[slugcatPageIndex], Region.GetRegionLandscapeScene(storyGameMode.region));
        scene.flatMode = true;
        scene.BuildScene(); //rebuild, but flat this time!!!!!
        if (ModManager.MSC) scene.BuildMSCScene();

        FSprite sprite;
        if (scene.flatIllustrations.Count < 1)
        {
            RainMeadow.RainMeadow.Debug("[CTP]: Couldn't find flat illustration for " + storyGameMode.region);

            //mostly copied from WarpRegionIcon.AddGraphics
            string reg = "warp-" + storyGameMode.region.ToLowerInvariant();
            if (!Futile.atlasManager.DoesContainElementWithName(reg))
            {
                Texture2D tex = new Texture2D(0, 0);
                string path = AssetManager.ResolveFilePath("illustrations/" + reg + ".png");
                if (File.Exists(path))
                {
                    ImageConversion.LoadImage(tex, File.ReadAllBytes(path));
                }
                tex.filterMode = 0;
                Futile.atlasManager.LoadAtlasFromTexture(reg, tex, false);
            }
            sprite = new FSprite(reg, true);
            sprite.scale = 5f; //default size = 100x100; this makes it 500x500; then turns into 350x350
            return sprite;
        }
        sprite = scene.flatIllustrations[0].sprite;
        scene.flatIllustrations.Clear(); //remove its references to my stuff!!!!
        return sprite;
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
