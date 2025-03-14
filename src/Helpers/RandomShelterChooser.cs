using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions.Comparers;
using Vector2 = UnityEngine.Vector2;

namespace CaptureThePearl.Helpers;

/// <summary>
/// Used to find the exact shelter at which to spawn.
/// </summary>
public static class RandomShelterChooser
{
    //public const float MINIMUM_SHELTER_DISTANCE = 500f; //regions are typically 2000-4000 wide, so this is pretty small
                                                        //todo: make this configurable, not constant
    /// <summary>
    /// Gets a shelter to respawn at!
    /// What is this mess... this sort is totally ridiculous;
    /// but it should work.
    /// Are you glad I didn't make you do this, lol?
    /// </summary>
    /// <param name="region">The region name.</param>
    /// <param name="slugcat">The slugcat.</param>
    /// <param name="otherTeamShelters">A list of shelters assigned to other teams.</param>
    /// <param name="distanceLeniency">How far away the shelter CAN be. 0 = MUST be furthest possible shelter; 1 = ANY possible shelter.</param>
    /// <returns></returns>
    /// <exception cref="IndexOutOfRangeException">Thrown if not enough shelters in the region to support the number of teams.</exception>
    public static string GetRespawnShelter(string region, SlugcatStats.Name slugcat, string[] otherTeamShelters, float distanceLeniency = 0.5f)
    {
        RandomShelterFilter.FindValidShelterPositions(region, slugcat);

        if (otherTeamShelters.Length >= RandomShelterFilter.shelterNames.Length)
            throw new IndexOutOfRangeException("There are more team shelters than there are shelters in the region!!!!");

        List<Vector2> otherShelterLocs = new();
        foreach (var s in otherTeamShelters)
        {
            int idx = Array.IndexOf(RandomShelterFilter.shelterNames, s);
            if (idx >= 0) otherShelterLocs.Add(RandomShelterFilter.shelterPositions[idx]);
        }

        var unorderedShelters = RandomShelterFilter.shelterNames
            .Select((n, i) => (n, RandomShelterFilter.shelterPositions[i]))
            .ToList();
        //manual sort... :(
        List<(string, float)> orderedShelters = new(unorderedShelters.Count);
        foreach (var s in unorderedShelters)
        {
            float score = MIN_DISTANCE(s.Item2, otherShelterLocs);
            int idx = orderedShelters.FindIndex(s => score > s.Item2); //index of first shelter with a better score
            if (idx < 0) orderedShelters.Add((s.n, score));
            else orderedShelters.Insert(idx, (s.n, score));
        }

        var shelterArr = orderedShelters.Select(s => s.Item1).ToArray();
        RainMeadow.RainMeadow.Debug($"[CTP]: Choosing top {distanceLeniency} of shelters: {string.Join(", ", shelterArr)}");

        int randomIdx = Mathf.FloorToInt(UnityEngine.Random.value * shelterArr.Length * distanceLeniency); //find a random shelter in the BEST FIRST HALF

        return shelterArr[randomIdx];
    }

    private static float MIN_DISTANCE(Vector2 a, List<Vector2> b)
    {
        if (b.Count < 1) return 0;
        float dist = float.PositiveInfinity;
        foreach (Vector2 v in b) dist = Mathf.Min(dist, (a - v).SqrMagnitude());
        return dist;
    }
}

/// <summary>
/// Used to find all valid shelters in the region.
/// Don't use this; use RandomShelterChooser if possible.
/// </summary>
public static class RandomShelterFilter
{
    public static string[] BANNED_SHELTERS = new string[] //put shelter names here, of course. Like SB_S03 or something
    {
        "idk that precipice one",
        "the OE one near the SU gate",
        "the SU one near the OE gate",
        "possibly the DS_SB shelter because that one would suck?",
        "the MSC shelter at the top of Subterranean (Farm Arrays entrance)",
        "oh no... submerged superstructure... what to do about that...???", //maybe just blacklist ALL of Bitter Aerie...? (except for Saint)
                                                                            //but Bitter Aerie sounds like a lot more fun than Submerged Superstructure!!
                                                                            //maybe make Bitter Aerie and Submerged Superstructure separate region options? That'd be a mess
        "the SH shelter near its GW entrance", //accessible only by Arti, Spearmaster, Saint, or a lot of spear-climbing or advanced movement; not fun
        "possibly the top shelter in Five Pebbles?" //also definitely for RM
    };

    public static string[] shelterNames = new string[0];
    public static Vector2[] shelterPositions = new Vector2[0];

    private static string lastSearchedRegion = "";
    private static SlugcatStats.Name lastSearchedSlugcat = null;

    public static void FindValidShelterPositions(string region, SlugcatStats.Name slugcat)
    {
        if (lastSearchedRegion == region && lastSearchedSlugcat == slugcat)
            return; //there's nothing to be done! lucky you!

        string filePath = FindRegionFile(region);
        if (!File.Exists(filePath)) throw new FileNotFoundException($"Failed to find file for {region}");

        //get region shelter list
        var shelters = FindRegionShelters(filePath, slugcat.value);

        //blacklist unaccessible shelters
        BlacklistShelters(shelters, filePath, slugcat.value);

        shelterNames = shelters.ToArray();

        //get map positions
        string mapPath = FindMapFile(region, slugcat.value);
        if (!File.Exists(mapPath)) throw new FileNotFoundException($"Failed to find map file for {region}");
        shelterPositions = FindShelterPositions(shelters, mapPath);

        lastSearchedRegion = region;
        lastSearchedSlugcat = slugcat;
    }

    private static List<string> FindRegionShelters(string path, string slugcat)
    {
        List<string> shelters = new();

        string[] lines = File.ReadAllLines(path);

        bool roomStartFound = false;
        foreach (string line in lines)
        {
            if (!roomStartFound) //block start
            {
                if (line.StartsWith("ROOMS"))
                    roomStartFound = true;
            }
            else if (line.StartsWith("END ")) //block end
                break;
            else //actual line, hopefully
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string l = line.Trim();
                if (l.StartsWith("//")) continue; //don't process comments

                string[] data = Regex.Split(l, " : ");
                if (!shelters.Contains(data[0]) && data[data.Length - 1] == "SHELTER" || data[data.Length - 1] == "ANCIENTSHELTER")
                    shelters.Add(data[0]);
            }
        }

        return shelters;
    }

    private static void BlacklistShelters(List<string> shelters, string path, string slugcat)
    {
        //start with manual blacklist!
        foreach (string s in BANNED_SHELTERS) shelters.Remove(s);

        string[] lines = File.ReadAllLines(path);

        bool conditionalFound = false;
        foreach (string line in lines)
        {
            if (!conditionalFound) //block start
            {
                if (line.StartsWith("CONDITIONAL"))
                    conditionalFound = true;
            }
            else if (line.StartsWith("END ")) //block end
                break;
            else //actual line, hopefully
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string l = line.Trim();
                if (l.StartsWith("//")) continue; //don't process comments

                string[] data = Regex.Split(l, " : ");
                if (data.Length < 3) continue; //invalid data; who knows what weird line this might be!!

                if (shelters.Contains(data[2])) //potentially blacklist it!
                {
                    if (data[0] == slugcat && data[1] == "HIDEROOM") shelters.Remove(data[2]);
                    if (data[0] != slugcat && data[1] == "EXCLUSIVEROOM") shelters.Remove(data[2]);
                }
            }
        }
    }

    private static Vector2[] FindShelterPositions(List<string> shelterNames, string path)
    {
        Vector2[] shelterLocs = new Vector2[shelterNames.Count];

        string[] lines = File.ReadAllLines(path);

        bool roomStartFound = false;
        foreach (string line in lines)
        {
            if (!roomStartFound) //block start
            {
                if (line.StartsWith("ROOMS"))
                    roomStartFound = true;
            }
            else if (line.StartsWith("END ")) //block end
                break;
            else //actual line, hopefully
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string l = line.Trim();
                if (l.StartsWith("//")) continue; //don't process comments

                string[] nameData = l.Split(':');
                int idx = shelterNames.IndexOf(nameData[0]);
                if (idx < 0) continue;

                string data = nameData[1].Trim();
                string[] d = Regex.Split(data, "><");
                if (d.Length < 2) continue; //invalid data, somehow?
                if (float.TryParse(d[0], out var x) && float.TryParse(d[1], out var y))
                    shelterLocs[idx] = new Vector2(x, y);
            }
        }

        return shelterLocs;
    }

    private static string FindRegionFile(string region)
    {
        return AssetManager.ResolveFilePath(Path.Combine("world", region, "world_" + region + ".txt"));
    }
    private static string FindMapFile(string region, string slugcat)
    {
        string path = AssetManager.ResolveFilePath(Path.Combine("world", region, "map_" + region + "-" + slugcat + ".txt"));
        if (File.Exists(path)) return path;
        return AssetManager.ResolveFilePath(Path.Combine("world", region, "map_" + region + ".txt"));
    }
}
