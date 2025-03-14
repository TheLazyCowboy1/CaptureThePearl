using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CaptureThePearl;

/// <summary>
/// Hooks that are only active WHILE playing the game mode.
/// For example, no swallowing pearls.
/// </summary>
public static class CTPGameHooks
{

    public static void ApplyHooks()
    {
        On.Player.ctor += Player_ctor;
    }

    public static void RemoveHooks()
    {
        On.Player.ctor -= Player_ctor;
    }

    //Makes players have neuron glow always
    private static void Player_ctor(On.Player.orig_ctor orig, Player self, AbstractCreature abstractCreature, World world)
    {
        orig(self, abstractCreature, world);
        self.glowing = true;
    }
}
