/**
 * Seed: competitions and teams for the match schedule (demo data).
 * Crests are drawn as original monograms in each team's colours (no official logos).
 */
(function (SA) {
  // id: [name, short, primary, secondary]
  const T = (name, short, a, b) => ({ name, short, a, b });
  const teams = {
    // Egypt
    AHL: T('Al Ahly', 'AHL', '#C8102E', '#FFFFFF'), ZAM: T('Zamalek', 'ZAM', '#FFFFFF', '#D7182A'), PYR: T('Pyramids', 'PYR', '#0A2240', '#6CACE4'),
    MAS: T('Al Masry', 'MAS', '#00843D', '#FFFFFF'), CER: T('Ceramica Cleopatra', 'CER', '#232323', '#F58220'), SMO: T('Smouha', 'SMO', '#5BC2E7', '#FFFFFF'),
    ENP: T('ENPPI', 'ENP', '#E31B23', '#FFFFFF'), ISM: T('Ismaily', 'ISM', '#FFD200', '#0055A4'), ITT: T('Al Ittihad', 'ITT', '#1B5E20', '#F2C230'),
    ZED: T('ZED FC', 'ZED', '#111111', '#C6FF00'), NBE: T('National Bank', 'NBE', '#002F6C', '#C9A227'), MOD: T('Modern Sport', 'MOD', '#00A3AD', '#0B2545'),
    GAI: T('Talaea El Gaish', 'GAI', '#7A1F2B', '#FFFFFF'), PET: T('Petrojet', 'PET', '#005BAC', '#FDB913'), GOU: T('El Gouna', 'GOU', '#F47B20', '#0B2545'),
    MAH: T('Ghazl El Mahalla', 'MAH', '#2D5DA8', '#FFFFFF'), HAR: T('Haras El Hodoud', 'HAR', '#3B6E3B', '#FFFFFF'), PHA: T('Pharco', 'PHA', '#0098DB', '#FFFFFF'),
    // Africa
    SUN: T('Mamelodi Sundowns', 'SUN', '#FFD100', '#00843D'), EST: T('Espérance de Tunis', 'EST', '#C8102E', '#F2C230'), WAC: T('Wydad AC', 'WAC', '#C8102E', '#FFFFFF'),
    RCA: T('Raja CA', 'RCA', '#00843D', '#FFFFFF'), TPM: T('TP Mazembe', 'TPM', '#111111', '#FFFFFF'), HIL: T('Al Hilal Omdurman', 'HIL', '#1F4E9C', '#FFFFFF'),
    SIM: T('Simba SC', 'SIM', '#C8102E', '#FFFFFF'), YAN: T('Young Africans', 'YAN', '#0B7A3E', '#FFD200'), PIR: T('Orlando Pirates', 'PIR', '#111111', '#FFFFFF'),
    MCA: T('MC Alger', 'MCA', '#C8102E', '#00843D'), CRB: T('CR Belouizdad', 'CRB', '#C8102E', '#FFFFFF'), PET2: T('Petro de Luanda', 'PDL', '#F2C230', '#1F4E9C'),
    // Europe
    RMA: T('Real Madrid', 'RMA', '#FFFFFF', '#B8A160'), BAR: T('Barcelona', 'BAR', '#A50044', '#004D98'), ATM: T('Atlético Madrid', 'ATM', '#CB3524', '#FFFFFF'),
    ATH: T('Athletic Club', 'ATH', '#EE2523', '#FFFFFF'), RSO: T('Real Sociedad', 'RSO', '#0067B1', '#FFFFFF'), SEV: T('Sevilla', 'SEV', '#FFFFFF', '#D71920'),
    VIL: T('Villarreal', 'VIL', '#FFE667', '#005187'), BET: T('Real Betis', 'BET', '#00954C', '#FFFFFF'),
    LIV: T('Liverpool', 'LIV', '#C8102E', '#00B2A9'), MCI: T('Manchester City', 'MCI', '#6CABDD', '#1C2C5B'), ARS: T('Arsenal', 'ARS', '#EF0107', '#FFFFFF'),
    CHE: T('Chelsea', 'CHE', '#034694', '#FFFFFF'), MUN: T('Manchester United', 'MUN', '#DA291C', '#FBE122'), TOT: T('Tottenham', 'TOT', '#FFFFFF', '#132257'),
    NEW: T('Newcastle', 'NEW', '#241F20', '#FFFFFF'), AVL: T('Aston Villa', 'AVL', '#670E36', '#95BFE5'), BHA: T('Brighton', 'BHA', '#0057B8', '#FFFFFF'), WHU: T('West Ham', 'WHU', '#7A263A', '#1BB1E7'),
    INT: T('Inter', 'INT', '#0068A8', '#111111'), MIL: T('AC Milan', 'MIL', '#FB090B', '#111111'), JUV: T('Juventus', 'JUV', '#111111', '#FFFFFF'),
    NAP: T('Napoli', 'NAP', '#12A0D7', '#FFFFFF'), ROM: T('Roma', 'ROM', '#8E1F2F', '#F0BC42'), LAZ: T('Lazio', 'LAZ', '#87D8F7', '#FFFFFF'),
    ATA: T('Atalanta', 'ATA', '#1E71B8', '#111111'), FIO: T('Fiorentina', 'FIO', '#482E92', '#FFFFFF'),
    BAY: T('Bayern Munich', 'BAY', '#DC052D', '#FFFFFF'), BVB: T('Borussia Dortmund', 'BVB', '#FDE100', '#111111'), B04: T('Bayer Leverkusen', 'B04', '#E32221', '#111111'),
    RBL: T('RB Leipzig', 'RBL', '#FFFFFF', '#DD0741'), VFB: T('Stuttgart', 'VFB', '#FFFFFF', '#E32219'), SGE: T('Eintracht Frankfurt', 'SGE', '#111111', '#E1000F'),
    PSG: T('Paris Saint-Germain', 'PSG', '#004170', '#DA291C'), OM: T('Marseille', 'OM', '#FFFFFF', '#2FAEE0'), ASM: T('Monaco', 'ASM', '#E51B22', '#FFFFFF'),
    OL: T('Lyon', 'OL', '#FFFFFF', '#14387F'), LOSC: T('Lille', 'LIL', '#E01E13', '#20325F'), OGCN: T('Nice', 'NIC', '#111111', '#C8102E'),
    BEN: T('Benfica', 'BEN', '#E83030', '#FFFFFF'), POR: T('Porto', 'POR', '#00428C', '#FFFFFF'),
    // Nations
    EGY: T('Egypt', 'EGY', '#C8102E', '#FFFFFF'), MAR: T('Morocco', 'MAR', '#C1272D', '#006233'), SEN: T('Senegal', 'SEN', '#00853F', '#FDEF42'),
    NGA: T('Nigeria', 'NGA', '#008751', '#FFFFFF'), ALG: T('Algeria', 'ALG', '#006233', '#FFFFFF'), TUN: T('Tunisia', 'TUN', '#E70013', '#FFFFFF'),
    GHA: T('Ghana', 'GHA', '#FCD116', '#006B3F'), CMR: T('Cameroon', 'CMR', '#007A5E', '#CE1126'), CIV: T('Côte d’Ivoire', 'CIV', '#F77F00', '#009E60'),
    ESP: T('Spain', 'ESP', '#AA151B', '#F1BF00'), FRA: T('France', 'FRA', '#0055A4', '#FFFFFF'), ENG: T('England', 'ENG', '#FFFFFF', '#CE1124'),
    GER: T('Germany', 'GER', '#111111', '#DD0000'), POR2: T('Portugal', 'POR', '#006600', '#FF0000'), ITA: T('Italy', 'ITA', '#0066CC', '#FFFFFF'),
    NED: T('Netherlands', 'NED', '#FF6600', '#FFFFFF'), BEL: T('Belgium', 'BEL', '#E30613', '#111111'),
  };

  // days: weekdays the competition plays (0 = Sun). times: Cairo kick-off hours.
  const competitions = [
    { id: 'epl-eg', name: 'Egyptian Premier League', short: 'EPL Egypt', region: 'Egypt', color: '#0B6E4F', days: [1, 4, 5, 6], times: [17, 20], perDay: 3, stage: 'Gameweek',
      teams: ['AHL', 'ZAM', 'PYR', 'MAS', 'CER', 'SMO', 'ENP', 'ISM', 'ITT', 'ZED', 'NBE', 'MOD', 'GAI', 'PET', 'GOU', 'MAH', 'HAR', 'PHA'], venue: ['Cairo International Stadium', 'Al Salam Stadium', '30 June Stadium', 'Borg El Arab Stadium', 'Suez Stadium', 'Ismailia Stadium'] },
    { id: 'caf-cl', name: 'CAF Champions League', short: 'CAF CL', region: 'Africa', color: '#2459D6', days: [5, 6, 0], times: [17, 20], perDay: 2, stage: 'Second preliminary round',
      teams: ['AHL', 'PYR', 'SUN', 'EST', 'WAC', 'RCA', 'TPM', 'HIL', 'SIM', 'YAN', 'PIR', 'MCA', 'CRB', 'PET2'], venue: ['Cairo International Stadium', 'Stade Mohammed V', 'Loftus Versfeld', 'Stade Olympique de Radès', 'Benjamin Mkapa Stadium'] },
    { id: 'ucl', name: 'UEFA Champions League', short: 'UCL', region: 'Europe', color: '#14213D', days: [2, 3], times: [19.75, 22], perDay: 4, stage: 'League phase · Matchday',
      teams: ['RMA', 'BAR', 'BAY', 'MCI', 'LIV', 'ARS', 'PSG', 'INT', 'JUV', 'MIL', 'BVB', 'ATM', 'CHE', 'NAP', 'B04', 'BEN', 'POR'], venue: ['Santiago Bernabéu', 'Allianz Arena', 'Anfield', 'Parc des Princes', 'San Siro', 'Etihad Stadium'] },
    { id: 'pl', name: 'Premier League', short: 'England', region: 'Europe', color: '#3D195B', days: [6, 0, 1], times: [14.5, 17, 19.5], perDay: 3, stage: 'Matchweek',
      teams: ['LIV', 'MCI', 'ARS', 'CHE', 'MUN', 'TOT', 'NEW', 'AVL', 'BHA', 'WHU'], venue: ['Anfield', 'Etihad Stadium', 'Emirates Stadium', 'Stamford Bridge', 'Old Trafford', 'St James’ Park'] },
    { id: 'laliga', name: 'La Liga', short: 'Spain', region: 'Europe', color: '#EE3A2F', days: [5, 6, 0], times: [16, 18.25, 21], perDay: 3, stage: 'Matchday',
      teams: ['RMA', 'BAR', 'ATM', 'ATH', 'RSO', 'SEV', 'VIL', 'BET'], venue: ['Santiago Bernabéu', 'Spotify Camp Nou', 'Metropolitano', 'San Mamés'] },
    { id: 'seriea', name: 'Serie A', short: 'Italy', region: 'Europe', color: '#0A6DB1', days: [6, 0], times: [16, 19, 21.75], perDay: 3, stage: 'Matchday',
      teams: ['INT', 'MIL', 'JUV', 'NAP', 'ROM', 'LAZ', 'ATA', 'FIO'], venue: ['San Siro', 'Allianz Stadium', 'Stadio Diego Armando Maradona', 'Stadio Olimpico'] },
    { id: 'bundesliga', name: 'Bundesliga', short: 'Germany', region: 'Europe', color: '#D20515', days: [5, 6, 0], times: [16.5, 19.5], perDay: 2, stage: 'Matchday',
      teams: ['BAY', 'BVB', 'B04', 'RBL', 'VFB', 'SGE'], venue: ['Allianz Arena', 'Signal Iduna Park', 'BayArena', 'Red Bull Arena'] },
    { id: 'ligue1', name: 'Ligue 1', short: 'France', region: 'Europe', color: '#091C3E', days: [5, 6, 0], times: [18, 22.08], perDay: 2, stage: 'Matchday',
      teams: ['PSG', 'OM', 'ASM', 'OL', 'LOSC', 'OGCN'], venue: ['Parc des Princes', 'Orange Vélodrome', 'Stade Louis II', 'Groupama Stadium'] },
    { id: 'afcon-q', name: 'AFCON 2027 Qualifiers', short: 'Nations · Africa', region: 'National teams', color: '#C09300', days: [4, 1], times: [19, 22], perDay: 2, stage: 'Group stage · Matchday',
      teams: ['EGY', 'MAR', 'SEN', 'NGA', 'ALG', 'TUN', 'GHA', 'CMR', 'CIV'], venue: ['Cairo International Stadium', 'Stade Mohammed V', 'Stade Abdoulaye Wade', 'Moshood Abiola Stadium'] },
    { id: 'unl', name: 'UEFA Nations League', short: 'Nations League', region: 'National teams', color: '#00296B', days: [4, 0, 3], times: [21.75], perDay: 2, stage: 'League A · Matchday',
      teams: ['ESP', 'FRA', 'ENG', 'GER', 'POR2', 'ITA', 'NED', 'BEL'], venue: ['Wembley Stadium', 'Stade de France', 'Estádio da Luz', 'Johan Cruijff ArenA'] },
  ];

  // Egyptian players abroad worth flagging on fixtures (demo)
  const egyptiansAbroad = { LIV: 'Salah', MCI: 'Marmoush' };

  SA.data.competitions = { teams, competitions, egyptiansAbroad };
})(window.SA);
