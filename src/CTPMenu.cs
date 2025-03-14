using System;
using System.Collections.Generic;
using System.Linq;
using RainMeadow;
using Menu.Remix.MixedUI;
using UnityEngine;
using Menu.Remix;
using Menu;

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

    public CTPMenu(ProcessManager manager) : base(manager)
    {
        tabWrapper = new MenuTabWrapper(this, pages[0]);
        pages[0].subObjects.Add(tabWrapper);

        //add region dropdowns
        SetupRegionDropdown();

        //remove "match save" option
        RemoveMenuObject(clientWantsToOverwriteSave);

        previousPageIdx = slugcatPageIndex;
    }

    private int previousPageIdx;
    private string previousRegion = "";
    public override void Update()
    {
        base.Update();

        if (OnlineManager.lobby.isOwner) //host update stuff
        {
            if (RegionDropdownBox != null)
            {
                //RegionSelected = RegionDropdownBox.value;
                storyGameMode.region = RegionDropdownBox.value;

                if (slugcatPageIndex != previousPageIdx)
                {
                    var oldItems = RegionDropdownBox._itemList;
                    var newItems = GetRegionList(slugcatPages[slugcatPageIndex].slugcatNumber);
                    RegionDropdownBox.RemoveItems(true, oldItems.Except(newItems).Select(item => item.name).ToArray());
                    RegionDropdownBox.AddItems(true, newItems.Except(oldItems).ToArray());
                    previousPageIdx = slugcatPageIndex;
                }
            }
        }
        else //client update stuff
        {
            if (storyGameMode.region != previousRegion)
            {
                ChangePageBackground();
                //previousRegion = storyGameMode.region; //changed in ChangePageBackground() instead
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
        RegionDropdownBox.description = "";

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

            RainMeadow.RainMeadow.Debug($"[CTP]: Changed background region scene to {previousRegion}.");
        } 
        catch (Exception ex) 
        {
            RainMeadow.RainMeadow.Error(ex); 
        }
    }

    private static void RemoveMenuObject(MenuObject obj)
    {
        if (obj != null)
        {
            if (obj is CheckBox cb) cb.Checked = false;
            obj.RemoveSprites();
            obj.inactive = true;
        }
    }

}
