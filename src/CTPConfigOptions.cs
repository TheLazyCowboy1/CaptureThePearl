using BepInEx.Logging;
using Menu.Remix.MixedUI;
using System;
using UnityEngine;

namespace CaptureThePearl;

public class CTPConfigOptions : OptionInterface
{
    private static ManualLogSource Logger;
    public CTPConfigOptions(ManualLogSource logger)
    {
        Logger = logger;

        TeamShelterCloseness = this.config.Bind<float>("TeamShelterCloseness", 0.5f, new ConfigAcceptableRange<float>(0f, 1f));
        RespawnCloseness = this.config.Bind<float>("RespawnCloseness", 0.75f, new ConfigAcceptableRange<float>(0f, 1f));
        PearlHeldSpeed = this.config.Bind<float>("PearlHeldSpeed", 0.7f, new ConfigAcceptableRange<float>(0.1f, 2f));
    }

    //configs
    public readonly Configurable<float> TeamShelterCloseness;
    public readonly Configurable<float> RespawnCloseness;
    public readonly Configurable<float> PearlHeldSpeed;

    public override void Initialize()
    {
        var opTab = new OpTab(this, "Options");
        this.Tabs = new[]
        {
            opTab
        };

        const float l = 10f, //left margin
            w = 100f, //config width
            t = l+l+w, //text start
            s = 30; //vertical spacing (I find a vertical spacing of 30 to be pleasant for configs; 25 is probably the minimum
        float y = 550f; //current height

        opTab.AddItems(
            new OpLabel(l, y, "Options", true),
            new OpLabel(t, y-=s+s, "Team Shelter Closeness"), //s+s == double spacing. You could also do s * 2f, s * 1.5f, etc.
            new OpUpdown(TeamShelterCloseness, new Vector2(l, y), w, 2) { description = "How close two team shelters can be.\n0 = as far away as possible, 1 = anywhere."},
            new OpLabel(t, y-=s, "Respawn Closeness"),
            new OpUpdown(RespawnCloseness, new Vector2(l, y), w, 2) { description = "How close to another team's shelter players can respawn.\n0 = as far away as possible, 1 = anywhere."},
            new OpLabel(t, y -= s, "Pearl Speed Penalty"),
            new OpUpdown(PearlHeldSpeed, new Vector2(l, y), w, 2) { description = "Multiply's a player's speed when holding a pearl. Makes it easier to catch players running with a team pearl." }
        );
    }

    /**<summary>
     * Calls every frame, approximately.
     * </summary>
     */
    public override void Update()
    {
        
    }

}