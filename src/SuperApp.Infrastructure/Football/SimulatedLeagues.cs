namespace SuperApp.Infrastructure.Football;

/// <summary>A club of the simulated provider: short code, name, strength (60–92) and kit colours.</summary>
internal sealed record SimClub(string Code, string Name, int Strength, string Kit, string Kit2);

/// <summary>A headline player of a simulated club (the rest of the squad is generated).</summary>
internal sealed record SimStar(string Name, string Position, decimal Price);

internal sealed record SimLeague(int LeagueId, int ExternalBase, IReadOnlyList<SimClub> Clubs, IReadOnlyDictionary<string, SimStar[]> Stars,
    string[] FirstNames, string[] LastNames, int Gameweeks);

/// <summary>
/// The two leagues the simulated provider serves — the same clubs, kit colours and headline players as the site's demo
/// data (js/data/fantasy.js), so demo mode and API mode look alike. Real player names; no logos.
/// </summary>
internal static class SimulatedLeagues
{
    public const int EgyptLeagueId = 233;
    public const int EnglandLeagueId = 39;

    public static SimLeague? Find(int leagueId) => leagueId switch
    {
        EgyptLeagueId => Egypt,
        EnglandLeagueId => England,
        _ => null,
    };

    private static SimStar S(string name, string position, decimal price) => new(name, position, price);

    public static readonly SimLeague Egypt = new(EgyptLeagueId, 9_100_000,
    [
        new("AHL", "Al Ahly", 92, "#C8102E", "#FFFFFF"), new("ZAM", "Zamalek", 86, "#FFFFFF", "#D7182A"),
        new("PYR", "Pyramids", 88, "#0A2240", "#6CACE4"), new("MAS", "Al Masry", 78, "#00843D", "#FFFFFF"),
        new("MOD", "Modern Sport", 74, "#00A3AD", "#0B2545"), new("CER", "Ceramica Cleopatra", 76, "#232323", "#F58220"),
        new("SMO", "Smouha", 70, "#5BC2E7", "#FFFFFF"), new("ENP", "ENPPI", 71, "#E31B23", "#FFFFFF"),
        new("ISM", "Ismaily", 66, "#FFD200", "#0055A4"), new("ITT", "Al Ittihad", 69, "#1B5E20", "#F2C230"),
        new("GAI", "Talaea El Gaish", 67, "#7A1F2B", "#FFFFFF"), new("NBE", "National Bank", 72, "#002F6C", "#C9A227"),
        new("MAH", "Ghazl El Mahalla", 64, "#2D5DA8", "#FFFFFF"), new("GOU", "El Gouna", 65, "#F47B20", "#0B2545"),
        new("PET", "Petrojet", 66, "#005BAC", "#FDB913"), new("HAR", "Haras El Hodoud", 62, "#3B6E3B", "#FFFFFF"),
        new("ZED", "ZED FC", 73, "#111111", "#C6FF00"), new("PHA", "Pharco", 63, "#0098DB", "#FFFFFF"),
    ],
    new Dictionary<string, SimStar[]>
    {
        ["AHL"] = [S("Mohamed El Shenawy", "GK", 5.5m), S("Yasser Ibrahim", "DEF", 5.5m), S("Mohamed Hany", "DEF", 5.0m), S("Ahmed Ramadan", "DEF", 5.0m),
            S("Emam Ashour", "MID", 9.0m), S("Trezeguet", "MID", 9.5m), S("Ahmed Sayed Zizo", "MID", 10.0m), S("Hussein El Shahat", "MID", 7.5m),
            S("Marwan Attia", "MID", 6.0m), S("Mohamed Sherif", "FWD", 8.5m)],
        ["ZAM"] = [S("Mohamed Awad", "GK", 5.0m), S("Hossam Abdelmaguid", "DEF", 5.0m), S("Mahmoud Bentayg", "DEF", 5.0m), S("Abdallah El Saied", "MID", 7.5m),
            S("Nabil Emad Dunga", "MID", 6.0m), S("Ahmed Hamdi", "MID", 6.5m), S("Nasser Mansi", "FWD", 8.0m), S("Seif El Din Jaziri", "FWD", 7.5m)],
        ["PYR"] = [S("Ahmed El Shenawy", "GK", 5.0m), S("Ahmed Samy", "DEF", 5.0m), S("Osama Galal", "DEF", 5.0m), S("Mohanad Lasheen", "MID", 6.5m),
            S("Ramadan Sobhi", "MID", 8.0m), S("Walid El Karti", "MID", 7.5m), S("Mostafa Fathi", "MID", 8.5m), S("Fiston Mayele", "FWD", 9.0m)],
        ["MAS"] = [S("Salah Mohsen", "FWD", 7.0m), S("Mohamed Hamdy Zaki", "DEF", 4.5m), S("Omar El Wahsh", "MID", 6.0m)],
        ["CER"] = [S("Marwan Hamdi", "FWD", 7.0m), S("Mohamed Reda Bobo", "MID", 6.5m)],
        ["ZED"] = [S("Hossam Ashraf", "FWD", 6.5m), S("Mohamed Hesham", "MID", 6.0m)],
        ["SMO"] = [S("Mohamed Adel", "FWD", 6.0m)],
        ["ENP"] = [S("Ahmed Kamal", "MID", 6.0m)],
        ["NBE"] = [S("Ahmed Refaat", "MID", 6.5m)],
        ["MOD"] = [S("Mohamed Magdy Afsha", "MID", 6.5m)],
        ["ISM"] = [S("Ahmed Yasser", "FWD", 5.5m)],
        ["ITT"] = [S("Mabululu", "FWD", 6.5m)],
    },
    ["Mohamed", "Ahmed", "Mahmoud", "Mostafa", "Omar", "Karim", "Hossam", "Youssef", "Ali", "Islam", "Amr", "Tarek", "Hesham", "Ibrahim", "Khaled", "Ziad",
        "Hamza", "Marwan", "Seif", "Hazem", "Abdelrahman", "Mazen", "Ayman", "Sherif", "Kareem", "Fady", "Belal", "Adham"],
    ["Gaber", "Fathy", "El Sayed", "Hamdy", "Abdelaziz", "Mansour", "Salah", "Kamal", "Shawky", "Eid", "Ragab", "Soliman", "Nasr", "El Gendy", "Fouad",
        "Hegazy", "Morsy", "Wahba", "Zaki", "Atef", "Galal", "Shehata", "Rizk", "Barakat", "El Masry", "Tawfik", "Farag", "Lotfy"],
    34);

    public static readonly SimLeague England = new(EnglandLeagueId, 9_200_000,
    [
        new("ARS", "Arsenal", 92, "#EF0107", "#FFFFFF"), new("LIV", "Liverpool", 92, "#C8102E", "#F6EB61"),
        new("MCI", "Manchester City", 91, "#6CABDD", "#FFFFFF"), new("CHE", "Chelsea", 86, "#034694", "#FFFFFF"),
        new("MUN", "Manchester United", 82, "#DA291C", "#000000"), new("NEW", "Newcastle United", 83, "#111111", "#FFFFFF"),
        new("TOT", "Tottenham Hotspur", 80, "#FFFFFF", "#132257"), new("AVL", "Aston Villa", 81, "#670E36", "#95BFE5"),
        new("BHA", "Brighton", 77, "#0057B8", "#FFFFFF"), new("CRY", "Crystal Palace", 76, "#1B458F", "#C4122E"),
        new("BOU", "Bournemouth", 74, "#DA291C", "#000000"), new("BRE", "Brentford", 73, "#E30613", "#FFFFFF"),
        new("FUL", "Fulham", 72, "#FFFFFF", "#000000"), new("NFO", "Nottingham Forest", 75, "#DD0000", "#FFFFFF"),
        new("EVE", "Everton", 71, "#003399", "#FFFFFF"), new("WHU", "West Ham United", 72, "#7A263A", "#1BB1E7"),
        new("WOL", "Wolverhampton", 68, "#FDB913", "#231F20"), new("LEE", "Leeds United", 67, "#FFFFFF", "#1D428A"),
        new("SUN", "Sunderland", 65, "#EB172B", "#FFFFFF"), new("BUR", "Burnley", 64, "#6C1D45", "#99D6EA"),
    ],
    new Dictionary<string, SimStar[]>
    {
        ["LIV"] = [S("Alisson", "GK", 5.5m), S("Virgil van Dijk", "DEF", 6.0m), S("Mohamed Salah", "MID", 13.0m), S("Florian Wirtz", "MID", 8.5m), S("Alexander Isak", "FWD", 10.5m)],
        ["ARS"] = [S("David Raya", "GK", 5.5m), S("William Saliba", "DEF", 6.0m), S("Gabriel", "DEF", 6.0m), S("Bukayo Saka", "MID", 10.0m),
            S("Martin Ødegaard", "MID", 8.5m), S("Viktor Gyökeres", "FWD", 9.0m)],
        ["MCI"] = [S("Gianluigi Donnarumma", "GK", 5.5m), S("Joško Gvardiol", "DEF", 6.0m), S("Rodri", "MID", 6.5m), S("Phil Foden", "MID", 8.0m),
            S("Omar Marmoush", "MID", 8.0m), S("Erling Haaland", "FWD", 14.0m)],
        ["CHE"] = [S("Robert Sánchez", "GK", 5.0m), S("Reece James", "DEF", 5.5m), S("Cole Palmer", "MID", 10.5m), S("Enzo Fernández", "MID", 7.0m), S("João Pedro", "FWD", 7.5m)],
        ["MUN"] = [S("Bruno Fernandes", "MID", 9.0m), S("Bryan Mbeumo", "MID", 8.0m), S("Benjamin Šeško", "FWD", 7.5m)],
        ["NEW"] = [S("Bruno Guimarães", "MID", 7.0m), S("Anthony Gordon", "MID", 7.5m), S("Nick Woltemade", "FWD", 7.0m)],
        ["TOT"] = [S("Guglielmo Vicario", "GK", 5.0m), S("Mohammed Kudus", "MID", 7.0m), S("Richarlison", "FWD", 6.5m)],
        ["AVL"] = [S("Morgan Rogers", "MID", 7.0m), S("Ollie Watkins", "FWD", 8.5m)],
        ["BHA"] = [S("Kaoru Mitoma", "MID", 6.5m), S("Danny Welbeck", "FWD", 6.5m)],
        ["CRY"] = [S("Adam Wharton", "MID", 5.5m), S("Jean-Philippe Mateta", "FWD", 7.5m)],
        ["NFO"] = [S("Morgan Gibbs-White", "MID", 7.5m), S("Chris Wood", "FWD", 7.5m)],
        ["EVE"] = [S("Jordan Pickford", "GK", 5.0m), S("Iliman Ndiaye", "MID", 6.5m)],
        ["WHU"] = [S("Jarrod Bowen", "MID", 7.5m)],
        ["BOU"] = [S("Antoine Semenyo", "MID", 7.5m)],
        ["WOL"] = [S("Jørgen Strand Larsen", "FWD", 6.5m)],
    },
    ["James", "Jack", "Harry", "Oliver", "George", "Charlie", "Tom", "Josh", "Ben", "Lewis", "Ryan", "Callum", "Luke", "Jamie", "Kyle", "Aaron", "Liam",
        "Owen", "Connor", "Dan", "Mason", "Ethan", "Sam", "Joe"],
    ["Walker", "Cooper", "Hughes", "Clarke", "Wright", "Turner", "Mitchell", "Phillips", "Evans", "Morris", "Bennett", "Barnes", "Fletcher", "Webb",
        "Carter", "Lowe", "Shaw", "Holmes", "Dawson", "Reid", "Palmer", "Knight", "Ward", "Burke"],
    38);
}
