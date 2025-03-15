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

    private class CTPState : OnlineResource.ResourceData.ResourceDataState
    {
        public CTPState() : base() { }

        public CTPState(CTPLobbyData data) : base()
        {
            if (!CTPGameMode.IsCTPGameMode(out var gamemode)) return;

            teamPlayers = new(gamemode.PlayerTeams.Keys.Select(p => p.id).ToList());
            playerTeams = new(gamemode.PlayerTeams.Values.Select(p => (ushort)p).ToList());
            teamShelters = gamemode.TeamShelters;
        }

        [OnlineField]
        private DynamicOrderedPlayerIDs teamPlayers;
        [OnlineField]
        private DynamicOrderedUshorts playerTeams;
        [OnlineField]
        private string[] teamShelters;

        public override void ReadTo(OnlineResource.ResourceData data, OnlineResource resource)
        {
            if (!CTPGameMode.IsCTPGameMode(out var gamemode)) return;

            //gamemode.PlayerTeams = teamPlayers.list.Select((id, idx) => new KeyValuePair<OnlinePlayer, byte>(OnlineManager.players.Find(player => player.id == id), (byte)idx)).ToDictionary();
            gamemode.PlayerTeams = new(teamPlayers.list.Count);
            for (int i = 0; i < teamPlayers.list.Count; i++)
                gamemode.PlayerTeams.Add(OnlineManager.players.Find(player => player.id == teamPlayers.list[i]), (byte)playerTeams.list[i]);

            gamemode.TeamShelters = teamShelters;
        }

        public override Type GetDataType() => typeof(CTPLobbyData);
    }
}
