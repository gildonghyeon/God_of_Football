using System.Collections.Generic;
namespace MyFootballCareer
{
    // 팀마다 정해진 이름과 전술. 세리에A·라리가·프리미어리그 클럽이 떠오르도록 도시·지역 이름과 별명으로 지었습니다 (실제 클럽 이름은 쓰지 않음).
    // 전술(포메이션·스타일)도 그 클럽의 경기 방식을 참고했습니다. 팀 ID는 저장 파일과 연결되어 있어 바꾸지 않습니다.
    // 유니폼은 TeamKits에 있습니다.
    public static class TeamIdentity
    {
        public struct Entry
        {
            public string id, name, oldName;
            public Formation formation;
            public TacticStyle style;
        }

        static Entry E(string id, string name, string oldName, Formation f, TacticStyle s)
        { return new Entry { id = id, name = name, oldName = oldName, formation = f, style = s }; }

        const Formation F442 = Formation.F442, F433 = Formation.F433, F4231 = Formation.F4231, F352 = Formation.F352, F532 = Formation.F532;
        const TacticStyle Pos = TacticStyle.Possession, Press = TacticStyle.HighPress, Low = TacticStyle.LowBlock, Wing = TacticStyle.Wing, Counter = TacticStyle.Counter;

        public static readonly Entry[] All =
        {
            // 1부
            E("crownhaven",          "Madrid Merengues",        "Crownhaven FC",       F433,  Counter),
            E("kingsport-athletic",  "Catalonia Blaugrana",     "Kingsport Athletic",  F433,  Press),
            E("silverkeep-united",   "Manchester Sky Blues",    "Silverkeep United",   F433,  Pos),
            E("goldmoor-city",       "Merseyside Reds",         "Goldmoor City",       F433,  Press),
            E("highspire",           "North London Gunners",    "Highspire FC",        F433,  Pos),
            E("stormwatch-rovers",   "Milano Nerazzurri",       "Stormwatch Rovers",   F352,  Wing),
            E("starfall-united",     "Turin Zebras",            "Starfall United",     F532,  Low),
            E("valecrest-city",      "Madrid Rojiblancos",      "Valecrest City",      F442,  Low),
            E("thunderpeak",         "Milano Rossoneri",        "Thunderpeak FC",      F4231, Counter),
            E("aldercrown-albion",   "London Blues",            "Aldercrown Albion",   F4231, Pos),
            // 2부
            E("northbrook",          "Partenopei Azzurri",      "Northbrook FC",       F433,  Pos),
            E("eastmere-united",     "Manchester Red Devils",   "Eastmere United",     F4231, Counter),
            E("westhollow-city",     "North London Lilywhites", "Westhollow City",     F4231, Press),
            E("southvale-rovers",    "Capitolini Giallorossi",  "Southvale Rovers",    F4231, Counter),
            E("oakridge-athletic",   "Bergamo Nerazzurri",      "Oakridge Athletic",   F352,  Press),
            E("pinecrest",           "Sevillistas Blancos",     "Pinecrest FC",        F4231, Wing),
            E("rivermouth-town",     "Aquile Biancocelesti",    "Rivermouth Town",     F433,  Pos),
            E("stonegate-united",    "Tyneside Magpies",        "Stonegate United",    F433,  Press),
            E("copperton",           "Submarino Amarillo",      "Copperton FC",        F442,  Pos),
            E("brightwater",         "Midlands Villans",        "Brightwater SC",      F4231, Counter),
            // 3부
            E("ashgrove",            "Firenze Viola",           "Ashgrove FC",         F4231, Pos),
            E("elmstead-town",       "Bilbao Lions",            "Elmstead Town",       F4231, Press),
            E("foxden-united",       "Donostia Txuri-Urdin",    "Foxden United",       F433,  Pos),
            E("glenhollow-rovers",   "Seville Verdiblancos",    "Glenhollow Rovers",   F4231, Wing),
            E("hollowmere",          "Toro Granata",            "Hollowmere FC",       F352,  Low),
            E("lakeshore-athletic",  "Sussex Seagulls",         "Lakeshore Athletic",  F4231, Pos),
            E("millbrook-city",      "London Hammers",          "Millbrook City",      F4231, Counter),
            E("nettlefield",         "Murcielagos Ches",        "Nettlefield FC",      F442,  Counter),
            E("quarry-hill-united",  "Vallecas Franjirrojos",   "Quarry Hill United",  F4231, Press),
            E("rosewood-town",       "Felsinei Rossoblu",       "Rosewood Town",       F4231, Pos),
            // 4부
            E("saltmarsh",           "Black Country Wolves",    "Saltmarsh FC",        F352,  Counter),
            E("thistledown-united",  "Grifone Rossoblu",        "Thistledown United",  F442,  Low),
            E("underhill-rovers",    "Scaligeri Gialloblu",     "Underhill Rovers",    F352,  Low),
            E("wheatley-green",      "Emilia Neroverdi",        "Wheatley Green FC",   F433,  Pos),
            E("yarrow-town",         "Sardinia Isolani",        "Yarrow Town",         F442,  Counter),
            E("brambleton",          "Udine Zebrette",          "Brambleton FC",       F352,  Low),
            E("cinderwick-athletic", "Hampshire Saints",        "Cinderwick Athletic", F4231, Pos),
            E("dunmore-rangers",     "West London Hoops",       "Dunmore Rangers",     F442,  Wing),
            E("fernvale",            "Vigo Celestes",           "Fernvale FC",         F433,  Pos),
            E("graniteford-united",  "Madrid South Azulones",   "Graniteford United",  F442,  Low),
            // 5부
            E("red-cedar",           "Trent Forest Reds",       "Red Cedar United",    F4231, Counter),
            E("iron-bay",            "Lancashire Clarets",      "Iron Bay SC",         F442,  Low),
            E("blue-ridge",          "Merseyside Toffees",      "Blue Ridge FC",       F442,  Wing),
            E("mossbank-united",     "Pamplona Rojillos",       "Mossbank United",     F442,  Press),
            E("maple-hill",          "Conca d'Oro Rosanero",    "Maple Hill FC",       F433,  Counter),
            E("lantern-bay-rovers",  "Hertford Hornets",        "Lantern Bay Rovers",  F442,  Counter),
            E("kettle-cove",         "Balearic Bermellones",    "Kettle Cove FC",      F4231, Low),
            E("hazelbrook",          "Salentini Giallorossi",   "Hazelbrook FC",       F433,  Counter),
            E("sunfield",            "Yorkshire Whites",        "Sunfield Rovers",     F4231, Press),
            E("harbor-town",         "Lariani Blu",             "Harbor Town FC",      F433,  Pos),
        };

        static Dictionary<string, Entry> byId;
        static Dictionary<string, string> oldToNew;

        static void Build()
        {
            if (byId != null) return;
            byId = new Dictionary<string, Entry>();
            oldToNew = new Dictionary<string, string>();
            foreach (var e in All) { byId[e.id] = e; oldToNew[e.oldName] = e.name; }
        }

        public static bool TryGet(string id, out Entry entry)
        {
            Build();
            if (id != null && byId.TryGetValue(id, out entry)) return true;
            entry = default(Entry);
            return false;
        }

        // 예전 팀 이름(저장 파일에 글자로 남은 것)을 새 이름으로. 모르는 이름이면 그대로
        public static string Rename(string name)
        {
            Build();
            return name != null && oldToNew.TryGetValue(name, out var n) ? n : name;
        }

        // 설정의 팀 이름을 새 이름으로 맞춥니다 (설정 파일이 예전 이름이어도 게임에서는 새 이름).
        public static void ApplyTo(MatchSettings ms)
        {
            if (ms == null || ms.teams == null) return;
            foreach (var t in ms.teams)
                if (t != null && TryGet(t.id, out var e)) t.displayName = e.name;
        }

        // 저장 파일에 글자로 남은 팀 이름(이번 시즌 일정, 이적 제안, 시즌 기록, 트로피, 이적 기록, 커리어 기록)을 새 이름으로
        public static void MigrateSave(SaveData save, MatchSettings ms)
        {
            if (save == null) return;
            if (save.season != null && save.season.matches != null)
                foreach (var m in save.season.matches)
                {
                    var t = ms != null ? ms.FindTeam(m.opponentId) : null;
                    m.opponentName = t != null ? t.displayName : Rename(m.opponentName);
                }
            if (save.offers != null)
                foreach (var o in save.offers)
                {
                    var t = ms != null ? ms.FindTeam(o.teamId) : null;
                    o.teamName = t != null ? t.displayName : Rename(o.teamName);
                }
            if (save.seasonHistory != null) foreach (var h in save.seasonHistory) { h.teamName = Rename(h.teamName); h.champion = Rename(h.champion); }
            if (save.trophies != null) foreach (var t in save.trophies) t.teamName = Rename(t.teamName);
            if (save.transfers != null) foreach (var tr in save.transfers) { tr.fromTeamName = Rename(tr.fromTeamName); tr.toTeamName = Rename(tr.toTeamName); }
            if (save.careers != null) foreach (var c in save.careers) c.teamName = Rename(c.teamName);
        }
    }
}
