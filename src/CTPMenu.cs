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

namespace CaptureThePearl;

/// <summary>
/// Note: It literally copies off of the Story menu.
/// </summary>
public class CTPMenu : StoryOnlineMenu
{
    //public OpComboBox2 RegionDropdownBox;
    public OpComboBox RegionDropdownBox;
    public Configurable<string> regionConfig = new("SU");

    private MenuTabWrapper tabWrapper; //what on earth is this mess...

    public SimpleButton hostScugButton;
    public int hostSlugIndex;

    public CTPMenu(ProcessManager manager) : base(manager)
    {
        tabWrapper = new MenuTabWrapper(this, pages[0]);
        pages[0].subObjects.Add(tabWrapper);

        //remove "match save" option
        RemoveMenuObject(clientWantsToOverwriteSave);
        RemoveMenuObject(restartCheckbox);

        //NOTE: I personally don't agree with this. I think requiring campaign slugcat is a pretty important feature.
        RemoveMenuObject(reqCampaignSlug);
        storyGameMode.requireCampaignSlugcat = false;


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
        //

        //add region dropdowns
        SetupRegionDropdown();

        previousPageIdx = slugcatPageIndex;

        storyGameMode = (CTPGameMode)OnlineManager.lobby.gameMode;

        storyGameMode.Sanitize();
        storyGameMode.currentCampaign = slugcatPages[slugcatPageIndex].slugcatNumber;
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

            storyGameMode.region = RegionDropdownBox.value;

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

            //Could this stuff be moved to the constructor, or preferably to a separate function called by the constructor?
            //It doesn't make sense to run it every tick; but if it works, don't bother changing it.

            var sameSpotOtherSide = restartCheckboxPos.x - startButton.pos.x;
            //host button stuff
            if (hostScugButton == null)
            {
                //change scugs on a rotor whenever clicked and set the player to that scug
                var pos = new Vector2(restartCheckbox.pos.x /*+35f*/, restartCheckboxPos.y + 20f);

                hostSlugIndex = 0;
                hostScugButton = new SimpleButton(this, pages[0], SlugcatStats.getSlugcatName(slugcatColorOrder[hostSlugIndex]), "CTPHostScugButton", pos, new Vector2(110, 30));
                pages[0].subObjects.Add(hostScugButton);
            }
            personaSettings.playingAs = slugcatColorOrder[hostSlugIndex];
            storyGameMode.avatarSettings.playingAs = personaSettings.playingAs;

            //move custom colours
            colorsCheckbox.pos = new Vector2(friendlyFire.pos.x, friendlyFire.pos.y - 30f);
            float textWidth = GetRestartTextWidth(base.CurrLang);
            colorsCheckbox.label.pos = new Vector2(-textWidth * 1.5f, colorsCheckbox.pos.y + 3f - 30f);
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
        }
    }

    public void SetupRegionDropdown()
    {
        if (!OnlineManager.lobby.isOwner) return;

        RegionDropdownBox = new(
                regionConfig,
                this.nextButton.pos + new Vector2(-50, 300),
                200,
                GetRegionList(slugcatPages[slugcatPageIndex].slugcatNumber)
                );
        RegionDropdownBox.description = "tba";

        new UIelementWrapper(tabWrapper, RegionDropdownBox);
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
            string reg = Region.GetProperRegionAcronym(slugcat, regions[i]);
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
