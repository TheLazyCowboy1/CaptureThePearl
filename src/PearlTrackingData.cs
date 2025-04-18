using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlayEveryWare.EpicOnlineServices;
using RainMeadow;
using RainMeadow.Generics;
using static RainMeadow.Serializer;

namespace CaptureThePearl;

/// <summary>
/// Time to cave in... this file means that this will be a high-impact mod.
/// </summary>
public class PearlTrackingData : OnlineResource.ResourceData
{
    public PearlTrackingData() : base() { }

    public override ResourceDataState MakeState(OnlineResource resource)
    {
        return new State(this);
    }

    //vars to sync go in CTPGameMode
    public static OnlineEntity.EntityId NullEntityID = new(ushort.MaxValue, OnlineEntity.EntityId.IdType.none, -1);

    private class State : ResourceDataState
    {
        public State() : base() { }

        public State(PearlTrackingData data) : base()
        {
            try
            {
                if (!CTPGameMode.IsCTPGameMode(out var gamemode))
                {
                    teamPearls = new(new(0)); //make the list blank
                    return;
                }

                if (gamemode.pearlTrackerOwner != null && !gamemode.pearlTrackerOwner.isMe)
                    RainMeadow.RainMeadow.Debug($"[CTP]: Received ownership of the game pearl tracking system from {gamemode.pearlTrackerOwner}");
                gamemode.pearlTrackerOwner = OnlineManager.mePlayer;
                gamemode.SetupTrackedPearls();

                //trackedPearls = gamemode.TrackedPearls;
                teamPearls = new(gamemode.TeamPearls.Select(pearl => pearl == null ? NullEntityID : pearl.id).ToList());
            }
            catch (Exception ex)
            {
                RainMeadow.RainMeadow.Error(ex);
            }
        }
        [OnlineField]
        private DynamicOrderedEntityIDs teamPearls;

        public override void ReadTo(OnlineResource.ResourceData data, OnlineResource resource)
        {
            try
            {
                if (!CTPGameMode.IsCTPGameMode(out var gamemode)) return;
                gamemode.pearlTrackerOwner = resource.owner;

                //if (resource.isOwner) return; //don't apply this for host!
                    //it might be transferring to me, though!!!

                if (gamemode.TeamPearls.Length != teamPearls.list.Count)
                { //reset TeamPearls list
                    gamemode.TeamPearls = teamPearls.list.Select(
                        id => id == null
                            ? null //if id == null, set opo to null
                            : id.FindEntity(true) as OnlinePhysicalObject
                        ).ToArray();
                }
                else
                {
                    for (int i = 0; i < gamemode.TeamPearls.Length; i++)
                    {
                        if (teamPearls.list[i].type != (byte)OnlineEntity.EntityId.IdType.none //null check
                            && teamPearls.list[i] != gamemode.TeamPearls[i]?.id)
                        {
                            gamemode.TeamPearls[i] = teamPearls.list[i].FindEntity(true) as OnlinePhysicalObject;
                            RainMeadow.RainMeadow.Debug($"[CTP]: Found pearl {gamemode.TeamPearls[i]} for team {i}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RainMeadow.RainMeadow.Error(ex);
            }
        }

        public override Type GetDataType() => typeof(PearlTrackingData);
    }
}
