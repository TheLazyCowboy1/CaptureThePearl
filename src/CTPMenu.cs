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

        ChangePageBackground();

        previousPageIdx = slugcatPageIndex;
    }

    private int previousPageIdx;
    private string previousRegion = "SU";
    public override void Update()
    {
        base.Update();

        if (OnlineManager.lobby.isOwner) //host update stuff
        {
            //RegionSelected = RegionDropdownBox.value;
            storyGameMode.region = Region.GetRegionFullName(RegionDropdownBox.value, slugcatPages[slugcatPageIndex].slugcatNumber);

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
                previousRegion = storyGameMode.region;
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
            slugcatPages[slugcatPageIndex].slugcatImage?.RemoveSprites();
            slugcatPages[slugcatPageIndex].slugcatImage = new InteractiveMenuScene(this, slugcatPages[slugcatPageIndex], Region.GetRegionLandscapeScene(storyGameMode.region));
        } catch { }
    }
}
