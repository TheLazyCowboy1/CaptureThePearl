using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        if (clientWantsToOverwriteSave != null) clientWantsToOverwriteSave.Checked = false;
        clientWantsToOverwriteSave?.RemoveSprites();

        //ChangePageBackground();
        AdjustBackgroundLocation();

        previousPageIdx = slugcatPageIndex;
    }

    private int previousPageIdx;
    private string previousRegion = "";
    public override void Update()
    {
        base.Update();

        if (OnlineManager.lobby.isOwner) //host update stuff
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

    public void ChangePageBackground()
    {
        if (OnlineManager.lobby.isOwner) return; //owner gets default background scene
        if (string.IsNullOrEmpty(storyGameMode.region)) return; //don't process false regions
        try
        {
            //find original image
            //int slugImageIdx = slugcatPages[slugcatPageIndex].subObjects.IndexOf(slugcatPages[slugcatPageIndex].slugcatImage);
            //if (slugcatPageIndex < 0) return; //if the background doesn't exist yet... don't change it, I guess

            //remove sprites
            if (slugcatPages[slugcatPageIndex].slugcatImage != null)
            {
                //pages[0].RemoveSubObject(slugcatPages[slugcatPageIndex].slugcatImage);
                //slugcatPages[slugcatPageIndex].RemoveSubObject(slugcatPages[slugcatPageIndex].slugcatImage);
                slugcatPages[slugcatPageIndex].slugcatImage.UnloadImages();
                slugcatPages[slugcatPageIndex].slugcatImage.RemoveSprites();
                //slugcatPages[slugcatPageIndex].slugcatImage = null;
                //slugcatPages[slugcatPageIndex].subObjects[slugcatPageIndex]?.RemoveSprites();

                /*slugcatPages[slugcatPageIndex].slugcatImage.sceneID = Region.GetRegionLandscapeScene(storyGameMode.region);
                slugcatPages[slugcatPageIndex].slugcatImage.BuildScene();
                AddBackgroundIllustrations(slugcatPages[slugcatPageIndex].slugcatImage);*/
                slugcatPages[slugcatPageIndex].slugcatImage = new InteractiveMenuScene(this, slugcatPages[slugcatPageIndex], Region.GetRegionLandscapeScene(storyGameMode.region));

                slugcatPages[slugcatPageIndex].slugcatImage.Show();
                slugcatPages[slugcatPageIndex].slugcatImage.Container.MoveToBack(); //don't let it cover up anything else; that's annoying

                previousRegion = storyGameMode.region;

                RainMeadow.RainMeadow.Debug($"[CTP]: Changed background region scene to {previousRegion}.");
            }

            //create new background
            //new MenuScene()
            //slugcatPages[slugcatPageIndex].slugcatImage = new InteractiveMenuScene(this, pages[0], Region.GetRegionLandscapeScene(storyGameMode.region));

            //replace old background
            //slugcatPages[slugcatPageIndex].subObjects[slugcatPageIndex] = slugcatPages[slugcatPageIndex].slugcatImage;
            //slugcatPages[slugcatPageIndex].subObjects.Insert(slugImageIdx, slugcatPages[slugcatPageIndex].slugcatImage);
            //pages[0].subObjects.Add(slugcatPages[slugcatPageIndex].slugcatImage);
            //slugcatPages[slugcatPageIndex].slugcatImage.
            //slugcatPages[slugcatPageIndex].slugcatImage.Show();

            //previousRegion = storyGameMode.region;
        } catch { }
    }

    private void AdjustBackgroundLocation()
    {
        if (OnlineManager.lobby.isOwner) return; //don't change host's background
        if (slugcatPages[slugcatPageIndex].slugcatImage == null)
        {
            RainMeadow.RainMeadow.Debug("[CTP]: Couldn't change background image; no background to be changed!!");
            return;
        }
        slugcatPages[slugcatPageIndex].slugcatImage.Container.alpha = 0.7f; //make it partially transparent
        //slugcatPages[slugcatPageIndex].slugcatImage.Container.SetPosition(0, 0); //center it
        slugcatPages[slugcatPageIndex].slugcatImage.Container.scale = 0.5f; //increase scale so it doesn't cover the entire screen
    }
}
