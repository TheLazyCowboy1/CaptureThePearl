using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RainMeadow;
using RainMeadow.Generics;

namespace CaptureThePearl;

/// <summary>
/// Time to cave in... this file means that this will be a high-impact mod.
/// </summary>
public class CTPLobbyData : OnlineResource.ResourceData
{
    public CTPLobbyData() : base() { }

    public override ResourceDataState MakeState(OnlineResource resource)
    {
        return new CTPState(this);
    }

    //vars to sync go in CTPGameMode
    public static OnlineEntity.EntityId NullEntityID = new(ushort.MaxValue, OnlineEntity.EntityId.IdType.none, -1);

    private class CTPState : ResourceDataState
    {
        public CTPState() : base() { }

        public CTPState(CTPLobbyData data) : base()
        {
            try
            {
                if (!CTPGameMode.IsCTPGameMode(out var gamemode))
                {
                    teamPlayers = new ushort[0]; //fallbacks; otherwise game freezes if the gamemode doesn't exist
                    playerTeams = new byte[0];
                    teamShelters = new string[0];
                    teamPoints = new int[0];
                    return;
                }

                var tempKeys = gamemode.PlayerTeams.Keys.ToArray();
                foreach (var key in tempKeys)
                {
                    if (key == null || gamemode.PlayerTeams[key] == null)
                    {
                        gamemode.PlayerTeams.Remove(key);
                        RainMeadow.RainMeadow.Debug($"[CTP]: Glitched player??? {key}");
                    }
                }
                teamPlayers = gamemode.PlayerTeams.Keys.Select(p => p.inLobbyId).ToArray();
                playerTeams = gamemode.PlayerTeams.Values.ToArray();
                teamShelters = gamemode.TeamShelters;
                teamPoints = gamemode.TeamPoints;
                numberOfTeams = gamemode.NumberOfTeams;
                timerLength = gamemode.TimerLength;
                spawnCreatures = gamemode.SpawnCreatures;
                respawnCloseness = gamemode.ShelterRespawnCloseness;
                targetDistance = gamemode.TargetShelterDistance;
                pearlHeldSpeed = gamemode.PearlHeldSpeed;
                armPlayers = gamemode.ArmPlayers;

                teamPearls = gamemode.TeamPearls.Select(opo => opo == null ? NullEntityID : opo.id).ToArray();
            }
            catch (Exception ex)
            {
                RainMeadow.RainMeadow.Error(ex);
            }
        }

        [OnlineField(group = "players")] //as annoying as these "group" flags are, they help shrink the data size
        private ushort[] teamPlayers;
        //private DynamicOrderedUshorts teamPlayers;
        //private DynamicOrderedPlayerIDs teamPlayers;
        [OnlineField(group = "players")]
        private byte[] playerTeams;
        //private DynamicOrderedUshorts playerTeams;
        [OnlineField(group = "configs")]
        private string[] teamShelters;
        [OnlineField(group = "points")]
        private int[] teamPoints;
        [OnlineField(group = "configs")]
        private byte numberOfTeams;
        [OnlineField(group = "configs")]
        private int timerLength;
        [OnlineField(group = "configs")]
        private bool spawnCreatures;
        [OnlineField(group = "configs")]
        private float respawnCloseness;
        [OnlineField(group = "configs")]
        private float targetDistance;
        [OnlineField(group = "configs")]
        private float pearlHeldSpeed;
        [OnlineField(group = "configs")]
        private bool armPlayers;

        [OnlineField(group = "pearls")]
        private OnlineEntity.EntityId[] teamPearls;

        public override void ReadTo(OnlineResource.ResourceData data, OnlineResource resource)
        {
            try
            {
                if (!CTPGameMode.IsCTPGameMode(out var gamemode)) return;
                if (gamemode.lobby.isOwner) return; //don't apply this for host!

                //gamemode.PlayerTeams = teamPlayers.list.Select((id, idx) => new KeyValuePair<OnlinePlayer, byte>(lobby.participants.Find(player => player.id == id), (byte)idx)).ToDictionary();
                gamemode.PlayerTeams = new(teamPlayers.Length);
                for (int i = 0; i < teamPlayers.Length; i++)
                    gamemode.PlayerTeams.Add(OnlineManager.players.Find(player => player.inLobbyId == teamPlayers[i]), playerTeams[i]);

                gamemode.TeamShelters = teamShelters;

                gamemode.NumberOfTeams = numberOfTeams;
                gamemode.TimerLength = timerLength;
                gamemode.SpawnCreatures = spawnCreatures;

                gamemode.ShelterRespawnCloseness = respawnCloseness;
                gamemode.TargetShelterDistance = targetDistance;
                gamemode.PearlHeldSpeed = pearlHeldSpeed;
                gamemode.ArmPlayers = armPlayers;

                gamemode.TeamPoints = teamPoints;

                //gamemode.TeamPearls = teamPearls.Select(id => (id != NullEntityID && OnlineManager.recentEntities.TryGetValue(id, out OnlineEntity ent)) ? (ent as OnlinePhysicalObject) : null).ToArray();
                gamemode.TeamPearls = teamPearls.Select(id => id == NullEntityID ? null : id.FindEntity(true) as OnlinePhysicalObject).ToArray();

                //ensure cannot join game unless I have been assigned a team
                gamemode.changedRegions = !gamemode.PlayerTeams.ContainsKey(OnlineManager.mePlayer);
            }
            catch (Exception ex)
            {
                RainMeadow.RainMeadow.Error(ex);
            }
        }

        public override Type GetDataType() => typeof(CTPLobbyData);
    }
}
