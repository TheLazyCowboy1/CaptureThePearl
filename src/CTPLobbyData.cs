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

    private class CTPState : ResourceDataState
    {
        public CTPState() : base() { }

        public CTPState(CTPLobbyData data) : base()
        {
            if (!CTPGameMode.IsCTPGameMode(out var gamemode)) return;

            teamPlayers = new(gamemode.PlayerTeams.Keys.Select(p => p.id).ToList());
            playerTeams = new(gamemode.PlayerTeams.Values.Select(p => (ushort)p).ToList());
            teamShelters = gamemode.TeamShelters;
            //teamPearls = new(gamemode.TeamPearls.Select(p => p == null ? new OnlineEntity.EntityId(0, OnlineEntity.EntityId.IdType.none, -1) : p.id).ToList());
            teamPoints = gamemode.TeamPoints;
            numberOfTeams = gamemode.NumberOfTeams;
            timerLength = gamemode.TimerLength;
            spawnCreatures = gamemode.SpawnCreatures;
        }

        [OnlineField]
        private DynamicOrderedPlayerIDs teamPlayers;
        [OnlineField]
        private DynamicOrderedUshorts playerTeams;
        [OnlineField]
        private string[] teamShelters;
        //[OnlineField]
        //private OnlinePhysicalObject[] teamPearls;
        //private DynamicOrderedEntityIDs teamPearls; //this is really expensive, and I don't like it
        [OnlineField]
        private int[] teamPoints;
        [OnlineField]
        private byte numberOfTeams;
        [OnlineField]
        private int timerLength;
        [OnlineField]
        private bool spawnCreatures;

        public override void ReadTo(OnlineResource.ResourceData data, OnlineResource resource)
        {
            if (!CTPGameMode.IsCTPGameMode(out var gamemode)) return;
            if (gamemode.lobby.isOwner) return; //don't apply this for host!

            //gamemode.PlayerTeams = teamPlayers.list.Select((id, idx) => new KeyValuePair<OnlinePlayer, byte>(lobby.participants.Find(player => player.id == id), (byte)idx)).ToDictionary();
            gamemode.PlayerTeams = new(teamPlayers.list.Count);
            for (int i = 0; i < teamPlayers.list.Count; i++)
                gamemode.PlayerTeams.Add(OnlineManager.players.Find(player => player.id == teamPlayers.list[i]), (byte)playerTeams.list[i]);

            gamemode.TeamShelters = teamShelters;
            //gamemode.TeamPearls = teamPearls.list.Select(id => id.id == -1 ? null : (id.FindEntity(true) as OnlinePhysicalObject)).ToArray();

            gamemode.NumberOfTeams = numberOfTeams;
            gamemode.TimerLength = timerLength;
            gamemode.SpawnCreatures = spawnCreatures;

            gamemode.TeamPoints = teamPoints;

            //ensure cannot join game unless I have been assigned a team
            gamemode.changedRegions = !gamemode.PlayerTeams.ContainsKey(OnlineManager.mePlayer);
        }

        public override Type GetDataType() => typeof(CTPLobbyData);
    }
}
