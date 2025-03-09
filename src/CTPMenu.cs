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
        if (clientWantsToOverwriteSave != null) clientWantsToOverwriteSave.Checked = false;
        clientWantsToOverwriteSave?.RemoveSprites();

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
            //remove sprites
            if (slugcatPages[slugcatPageIndex].slugcatImage != null)
            {
                //slugcatPages[slugcatPageIndex].slugcatImage.UnloadImages();
                slugcatPages[slugcatPageIndex].slugcatImage.RemoveSprites();
                //slugcatPages[slugcatPageIndex].RemoveSubObject(slugcatPages[slugcatPageIndex].slugcatImage);

                slugcatPages[slugcatPageIndex].slugcatImage = new InteractiveMenuScene(this, slugcatPages[slugcatPageIndex], Region.GetRegionLandscapeScene(storyGameMode.region));

                //setup image size, alpha, etc.
                slugcatPages[slugcatPageIndex].slugcatImage.useFlatCrossfades = false;
                slugcatPages[slugcatPageIndex].slugcatImage.flatMode = true; //so there are no crossfades, just a flat image
                foreach (var img in slugcatPages[slugcatPageIndex].slugcatImage.flatIllustrations)
                {
                    //img.setAlpha = 0.5f;
                    //img.alpha = 0.5f;
                    //img.size *= 0.5f;
                    //img.pos *= 0.5f;
                    var newColor = img.color;
                    newColor.a *= 0.5f;
                    img.color = newColor;
                    img.sprite.MoveToBack();
                }

                slugcatPages[slugcatPageIndex].slugcatImage.TriggerCrossfade(40);
                slugcatPages[slugcatPageIndex].slugcatImage.Show();
                //AdjustBackgroundLocation();

                //slugcatPages[slugcatPageIndex].subObjects.Insert(0, slugcatPages[slugcatPageIndex].slugcatImage); //move it to the VERY back

                //add it to backgroundContainer
                //backgroundContainer.RemoveAllChildren();
                //backgroundContainer.AddChild(slugcatPages[slugcatPageIndex].slugcatImage.Container);

                previousRegion = storyGameMode.region;

                RainMeadow.RainMeadow.Debug($"[CTP]: Changed background region scene to {previousRegion}.");
            }
        } catch (Exception ex) { RainMeadow.RainMeadow.Error(ex); }
    }

    private void AdjustBackgroundLocation()
    {
        //if (OnlineManager.lobby.isOwner) return; //don't change host's background
        //backgroundContainer.alpha = 0.5f; //make it partially transparent
        //backgroundContainer.SetPosition(0, 0); //center it
        //backgroundContainer.scale = 0.5f; //increase scale so it doesn't cover the entire screen
        /*
        var img = slugcatPages[slugcatPageIndex].slugcatImage;
        List<MenuIllustration> subObjs = img.flatIllustrations;
        subObjs.AddRange(img.depthIllustrations.ConvertAll(ill => ill as MenuIllustration));
        foreach (var cf in img.crossFades.Values) subObjs.AddRange(cf);
        foreach (var obj in subObjs)
        {
            obj.setAlpha = 0.5f;
            obj.alpha = 0.5f;
            //obj.size *= 0.5f;
            //obj.pos *= 0.5f;
            //obj.myContainer?.MoveToBack();
            obj.Container.MoveToBack();
        }
        */
    }
}
