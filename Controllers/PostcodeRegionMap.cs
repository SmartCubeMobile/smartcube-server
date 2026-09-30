using System.Text.RegularExpressions;

namespace SmartCubeMobileV2026.Controllers
{
    // Maps a UK postcode to an electricity supply region (Ofgem distribution area id 10-23).
    // The postcode *area* (letters before the digits) decides the region in most cases; a few
    // areas straddle two regions, so learned outward-code mappings from real EnergyLinx runs
    // (dbo.PostcodeRegions) take priority over this table.
    public static class PostcodeRegionMap
    {
        public static readonly Dictionary<int, (string Gsp, string Name)> Regions = new()
        {
            [10] = ("_A", "Eastern"),
            [11] = ("_B", "East Midlands"),
            [12] = ("_C", "London"),
            [13] = ("_D", "Merseyside & North Wales"),
            [14] = ("_E", "Midlands"),
            [15] = ("_F", "North East"),
            [16] = ("_G", "North West"),
            [17] = ("_P", "North Scotland"),
            [18] = ("_N", "South Scotland"),
            [19] = ("_J", "South East"),
            [20] = ("_H", "Southern"),
            [21] = ("_K", "South Wales"),
            [22] = ("_L", "South West"),
            [23] = ("_M", "Yorkshire"),
        };

        private static readonly Dictionary<string, int> AreaToRegion = new(StringComparer.OrdinalIgnoreCase)
        {
            // 10 Eastern
            ["AL"] = 10, ["CB"] = 10, ["CM"] = 10, ["CO"] = 10, ["IP"] = 10, ["NR"] = 10, ["PE"] = 10, ["SG"] = 10, ["SS"] = 10, ["IG"] = 10, ["RM"] = 10,
            // 11 East Midlands
            ["DE"] = 11, ["LE"] = 11, ["LN"] = 11, ["NG"] = 11, ["NN"] = 11, ["MK"] = 11,
            // 12 London
            ["E"] = 12, ["EC"] = 12, ["N"] = 12, ["NW"] = 12, ["SE"] = 12, ["SW"] = 12, ["W"] = 12, ["WC"] = 12,
            ["EN"] = 12, ["HA"] = 12, ["TW"] = 12, ["UB"] = 12, ["WD"] = 12,
            // 13 Merseyside & North Wales
            ["CH"] = 13, ["CW"] = 13, ["L"] = 13, ["LL"] = 13, ["WA"] = 13,
            // 14 Midlands
            ["B"] = 14, ["CV"] = 14, ["DY"] = 14, ["GL"] = 14, ["HR"] = 14, ["ST"] = 14, ["SY"] = 14, ["TF"] = 14, ["WR"] = 14, ["WS"] = 14, ["WV"] = 14,
            // 15 North East
            ["DH"] = 15, ["DL"] = 15, ["NE"] = 15, ["SR"] = 15, ["TS"] = 15,
            // 16 North West
            ["BB"] = 16, ["BL"] = 16, ["CA"] = 16, ["FY"] = 16, ["LA"] = 16, ["M"] = 16, ["OL"] = 16, ["PR"] = 16, ["SK"] = 16, ["WN"] = 16,
            // 17 North Scotland
            ["AB"] = 17, ["DD"] = 17, ["HS"] = 17, ["IV"] = 17, ["KW"] = 17, ["PH"] = 17, ["ZE"] = 17,
            // 18 South Scotland
            ["DG"] = 18, ["EH"] = 18, ["FK"] = 18, ["G"] = 18, ["KA"] = 18, ["KY"] = 18, ["ML"] = 18, ["PA"] = 18, ["TD"] = 18,
            // 19 South East
            ["BN"] = 19, ["BR"] = 19, ["CR"] = 19, ["CT"] = 19, ["DA"] = 19, ["KT"] = 19, ["ME"] = 19, ["RH"] = 19, ["SM"] = 19, ["TN"] = 19,
            // 20 Southern
            ["BH"] = 20, ["DT"] = 20, ["GU"] = 20, ["HP"] = 20, ["OX"] = 20, ["PO"] = 20, ["RG"] = 20, ["SL"] = 20, ["SN"] = 20, ["SO"] = 20, ["SP"] = 20,
            // 21 South Wales
            ["CF"] = 21, ["LD"] = 21, ["NP"] = 21, ["SA"] = 21,
            // 22 South West
            ["BA"] = 22, ["BS"] = 22, ["EX"] = 22, ["PL"] = 22, ["TA"] = 22, ["TQ"] = 22, ["TR"] = 22,
            // 23 Yorkshire
            ["BD"] = 23, ["DN"] = 23, ["HD"] = 23, ["HG"] = 23, ["HU"] = 23, ["HX"] = 23, ["LS"] = 23, ["S"] = 23, ["WF"] = 23, ["YO"] = 23,
        };

        public static string Normalise(string postcode) =>
            Regex.Replace((postcode ?? "").ToUpperInvariant(), @"[^A-Z0-9]", "");

        // "SK83JH" -> "SK8"; "SW1A1AA" -> "SW1A"
        public static string Outward(string postcode)
        {
            var p = Normalise(postcode);
            if (p.Length < 5) return p;
            return p[..^3];
        }

        public static string Area(string postcode)
        {
            var m = Regex.Match(Normalise(postcode), @"^[A-Z]{1,2}");
            return m.Success ? m.Value : null;
        }

        public static int? RegionForArea(string postcode)
        {
            var area = Area(postcode);
            return area != null && AreaToRegion.TryGetValue(area, out var r) ? r : null;
        }

        public static int? RegionForGsp(string gsp) =>
            Regions.FirstOrDefault(kv => string.Equals(kv.Value.Gsp, gsp, StringComparison.OrdinalIgnoreCase)).Key is var k && k != 0 ? k : null;
    }
}
