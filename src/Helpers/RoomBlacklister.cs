using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace CaptureThePearl.Helpers;

public static class RoomBlacklister
{
    public static string[] BLOCKED_ROOMS = new string[]
    {
        "SU_CAVE01", //gives Saint access to OE area
        "LC_FINAL", //scav king fight
        "OE_CAVE03", //grants access to OE_SU area
        "OE_FINAL03", //alt ending room
        "HR_AI", //just block it off; don't want people crawing into there or achieving Saint's ending
        "SH_LEDGE", //accessible only to a few slugcats; way to GW gate
        "SL_C14", //way to MS gate
        "UW_H01", //the way to roof; difficult to access for most slugcats + far removed area
        "SI_SAINTINTRO", //difficult to get back from
        "MS_MEM06", //gives Saint access to Submerged Superstructure proper
        "SB_D06", //gives access to the Depths
        "SB_F03", //the ravine; over-powered for Saint, who can climb up it easily
        "RM_D07", //above RM_AI; mostly inaccessible
        "RM_CORE", //grants access to the core
        "HI_W02" //Watcher entrance to Hydroponics
    };

    public static void BlacklistRooms(World world, string[] teamShelters, float additionalDistance)
    {
        Vector2[] shelterPos = new Vector2[teamShelters.Length];
        for (int i = 0; i < teamShelters.Length; i++)
        {
            shelterPos[i] = world.GetAbstractRoom(teamShelters[i]).mapPos;
        }
        float maxTotalDistance = Mathf.Max(shelterPos.Select(p => shelterPos.Sum(o => Vector2.Distance(p, o))).ToArray())
            + additionalDistance * teamShelters.Length; //add additionalDistance

        List<int> blacklistedRooms = new();
        foreach (var room in world.abstractRooms)
        {
            if (room == null) continue;

            if (BLOCKED_ROOMS.Contains(room.name) //automatically blacklist rooms specified above
                || shelterPos.Sum(p => Vector2.Distance(room.mapPos, p)) > maxTotalDistance) //room is too far from shelters
            {
                blacklistedRooms.Add(room.index);
                world.DisabledMapRooms.Add(room.name);

                for (int i = 0; i < room.roomAttractions.Length; i++)
                {
                    room.roomAttractions[i] = AbstractRoom.CreatureRoomAttraction.Forbidden; //forbid creatures from entering this room
                }
                for (int i = 0; i < room.connections.Length; i++)
                {
                    room.connections[i] = -1; //disconnect all room connections
                }
            }
        }

        foreach (var room in world.abstractRooms)
        {
            for (int i = 0; i < room.connections.Length; i++)
            {
                if (blacklistedRooms.Contains(room.connections[i]))
                    room.connections[i] = -1; //disconnect from blacklisted rooms
            }
        }

        RainMeadow.RainMeadow.Debug($"[CTP]: Blacklisted {blacklistedRooms.Count} rooms in region {world.name}");
        blacklistedRooms.Clear();

    }

    public static bool InBounds(Vector2 pos, List<Vector2> teamShelterPos, float additionalDistance)
    {
        float maxTotalDistance = Mathf.Max(teamShelterPos.Select(p => teamShelterPos.Sum(o => Vector2.Distance(p, o))).ToArray())
            + additionalDistance * teamShelterPos.Count; //add additionalDistance
        return teamShelterPos.Sum(p => Vector2.Distance(pos, p)) <= maxTotalDistance;
    }
}
