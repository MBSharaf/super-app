/**
 * Arabic names of clubs and national teams as the live score feed spells them (league tables, match centre).
 * Looked up by SA.data.clubsAr.name(name): exact name first, then without a trailing "FC"/"SC"/"CF"… suffix.
 * Unknown names stay as they are (Latin script), so a new club never breaks the table.
 */
(function (SA) {
  const AR = {
    // Egypt
    'Al Ahly': 'الأهلي', Zamalek: 'الزمالك', Pyramids: 'بيراميدز', 'Ceramica Cleopatra': 'سيراميكا كليوباترا', 'Al Ittihad Alexandria': 'الاتحاد السكندري',
    'Modern Sport': 'مودرن سبورت', 'Al Mokawloon Al Arab': 'المقاولون العرب', 'National Bank': 'البنك الأهلي', Smouha: 'سموحة', ZED: 'زد',
    'Petrojet-Suez': 'بتروجت', Petrojet: 'بتروجت', 'Al Masry': 'المصري', 'Olympic El Qanah': 'القناة', 'Wadi Degla': 'وادي دجلة', "Tala'ea El Gaish": 'طلائع الجيش',
    'Talaea El Gaish': 'طلائع الجيش', 'Al Sharkeyah-ENPPI': 'إنبي', ENPPI: 'إنبي', 'Ghazl Al Mahalla': 'غزل المحلة', 'Ghazl El Mahalla': 'غزل المحلة',
    'Asyut Petroleum': 'بترول أسيوط', 'Abo Qir Semad': 'أبو قير للأسمدة', 'El Gouna': 'الجونة', Ismaily: 'الإسماعيلي', Pharco: 'فاركو', 'Haras El Hodoud': 'حرس الحدود',
    'El Geish': 'طلائع الجيش', 'Kahrabaa Ismailia': 'كهرباء الإسماعيلية', 'Haras El Hodood': 'حرس الحدود',
    // England
    Arsenal: 'آرسنال', 'Brighton & Hove Albion': 'برايتون', 'Manchester City': 'مانشستر سيتي', Liverpool: 'ليفربول', 'AFC Bournemouth': 'بورنموث', Bournemouth: 'بورنموث',
    Brentford: 'برينتفورد', 'Leeds United': 'ليدز يونايتد', Chelsea: 'تشيلسي', Everton: 'إيفرتون', 'Tottenham Hotspur': 'توتنهام', 'Hull City': 'هال سيتي',
    'Aston Villa': 'أستون فيلا', 'Newcastle United': 'نيوكاسل', 'Coventry City': 'كوفنتري سيتي', Fulham: 'فولهام', 'Ipswich Town': 'إيبسويتش', 'Nottingham Forest': 'نوتنجهام فورست',
    Sunderland: 'سندرلاند', 'Manchester United': 'مانشستر يونايتد', 'Crystal Palace': 'كريستال بالاس', 'West Ham United': 'وست هام', 'Wolverhampton Wanderers': 'وولفرهامبتون',
    Burnley: 'بيرنلي', 'Leicester City': 'ليستر سيتي', Southampton: 'ساوثهامبتون',
    // Spain
    Barcelona: 'برشلونة', 'Real Madrid': 'ريال مدريد', 'Atlético Madrid': 'أتلتيكو مدريد', 'Real Betis': 'ريال بيتيس', 'Real Sociedad': 'ريال سوسيداد', Sevilla: 'إشبيلية',
    'Racing Santander': 'راسينج سانتاندير', 'Athletic Club': 'أتلتيك بلباو', 'Deportivo Alavés': 'ألافيس', Villarreal: 'فياريال', Espanyol: 'إسبانيول',
    'Deportivo A Coruña': 'ديبورتيفو لاكورونيا', Osasuna: 'أوساسونا', 'Celta Vigo': 'سيلتا فيجو', Levante: 'ليفانتي', 'Rayo Vallecano': 'رايو فاييكانو', Valencia: 'فالنسيا',
    Getafe: 'خيتافي', Elche: 'إلتشي', 'Málaga': 'مالقة', Girona: 'جيرونا', Mallorca: 'مايوركا', 'Las Palmas': 'لاس بالماس',
    // Italy
    Lazio: 'لاتسيو', Roma: 'روما', Inter: 'إنتر ميلان', Milan: 'ميلان', Como: 'كومو', Juventus: 'يوفنتوس', Frosinone: 'فروزينوني', Napoli: 'نابولي', Bologna: 'بولونيا',
    Genoa: 'جنوى', Cagliari: 'كالياري', Atalanta: 'أتالانتا', Torino: 'تورينو', Monza: 'مونزا', Lecce: 'ليتشي', Fiorentina: 'فيورنتينا', Parma: 'بارما', Sassuolo: 'ساسولو',
    Udinese: 'أودينيزي', Venezia: 'فينيسيا', Empoli: 'إمبولي', Verona: 'فيرونا',
    // Germany
    'Borussia Dortmund': 'بوروسيا دورتموند', 'Bayern München': 'بايرن ميونخ', Freiburg: 'فرايبورج', 'RB Leipzig': 'لايبزيج', 'Bayer Leverkusen': 'باير ليفركوزن',
    'Werder Bremen': 'فيردر بريمن', 'Eintracht Frankfurt': 'آينتراخت فرانكفورت', 'VfB Stuttgart': 'شتوتجارت', Elversberg: 'إلفرسبرج', Augsburg: 'أوجسبورج',
    Hoffenheim: 'هوفنهايم', 'Hamburger SV': 'هامبورج', 'Mainz 05': 'ماينز', '1. FC Köln': 'كولن', Paderborn: 'بادربورن', 'Schalke 04': 'شالكه',
    'Union Berlin': 'يونيون برلين', 'Borussia Mönchengladbach': 'بوروسيا مونشنجلادباخ', Wolfsburg: 'فولفسبورج', 'St. Pauli': 'سانت باولي', Heidenheim: 'هايدنهايم',
    // France
    'Paris Saint-Germain': 'باريس سان جيرمان', 'Paris FC': 'باريس إف سي', Lille: 'ليل', Lyon: 'ليون', Marseille: 'مارسيليا', Brest: 'بريست', Nice: 'نيس', Monaco: 'موناكو',
    Rennes: 'رين', Lorient: 'لوريان', Lens: 'لانس', Toulouse: 'تولوز', 'Le Mans': 'لومان', Troyes: 'تروا', Auxerre: 'أوكسير', Strasbourg: 'ستراسبورج', Angers: 'أنجيه',
    'Le Havre': 'لوهافر', Nantes: 'نانت', Metz: 'ميتز',
    // Turkey
    Galatasaray: 'جالطة سراي', Trabzonspor: 'طرابزون سبور', 'Beşiktaş': 'بشكتاش', 'Amed Sportif': 'آمد سبورتيف', 'Kasımpaşa': 'قاسم باشا', 'Fenerbahçe': 'فنربخشة',
    'Başakşehir': 'باشاك شهير', Kocaelispor: 'كوجالي سبور', 'Göztepe': 'جوزتبه', Rizespor: 'ريزه سبور', 'Çorum FK': 'تشوروم', 'Gençlerbirliği': 'جنشلر بيرليجي',
    'Erzurumspor FK': 'أرضروم سبور', Alanyaspor: 'ألانيا سبور', Konyaspor: 'قونيا سبور', 'Gaziantep FK': 'غازي عنتاب', Samsunspor: 'سامسون سبور', 'Eyüpspor': 'أيوب سبور',
    // Rest of Europe
    'Club Brugge': 'كلوب بروج', 'Sporting CP': 'سبورتنج لشبونة', LASK: 'لاسك لينز', 'Shakhtar Donetsk': 'شاختار دونيتسك', Viking: 'فايكنج', 'PSV Eindhoven': 'آيندهوفن',
    'Bodø/Glimt': 'بودو جليمت', Porto: 'بورتو', 'AEK Athens': 'آيك أثينا', 'Sabah FK': 'صباح', Feyenoord: 'فينورد', 'Slavia Prague': 'سلافيا براج',
    'Slovan Bratislava': 'سلوفان براتيسلافا', Benfica: 'بنفيكا', 'Ferencváros': 'فيرنتسفاروش', 'Union St.Gilloise': 'سانت جيلواز', Olympiacos: 'أولمبياكوس',
    'Dinamo Zagreb': 'دينامو زغرب', 'Levski Sofia': 'ليفسكي صوفيا', 'NEC Nijmegen': 'نيميخن', 'AZ Alkmaar': 'ألكمار', Anderlecht: 'أندرلخت', 'OFI Crete': 'أوفي كريت',
    'Omonia Nicosia': 'أومونيا نيقوسيا', Torreense: 'توريينسي', 'Sparta Prague': 'سبارتا براج', 'Sturm Graz': 'شتورم جراتس', 'NK Celje': 'سيليي', Salzburg: 'سالزبورج',
    'Jagiellonia Białystok': 'ياجيلونيا', 'Viktoria Plzeň': 'فيكتوريا بلزن', Celtic: 'سيلتيك', 'Hapoel Beer Sheva': 'هبوعيل بئر السبع', 'Lillestrøm': 'ليلستروم',
    'Lech Poznań': 'ليخ بوزنان', 'Ararat Armenia': 'أرارات أرمينيا', Lugano: 'لوجانو', Panathinaikos: 'باناثينايكوس', Twente: 'تفينتي', Brann: 'بران', 'København': 'كوبنهاجن',
    'Universitatea Craiova': 'كرايوفا', 'Mjällby': 'ميالبي', 'St.Truiden': 'سانت ترويدن', Ajax: 'أياكس', 'Hajduk Split': 'هايدوك سبليت', 'FK Crvena Zvezda': 'النجم الأحمر',
    Braga: 'براجا', Gent: 'جنت', Rangers: 'رينجرز', 'Heart of Midlothian': 'هارتس', Midtjylland: 'ميتيلاند', 'Pafos FC': 'بافوس', 'CSKA Sofia': 'سسكا صوفيا', AGF: 'آرهوس',
    'Nordsjælland': 'نوردشيلاند', 'Kairat Almaty': 'كايرات ألماتي', 'Young Boys': 'يونج بويز', Basel: 'بازل',
    // Africa
    'Espérance': 'الترجي', 'Espérance Tunis': 'الترجي', 'FC Nouadhibou': 'نواذيبو', Nouadhibou: 'نواذيبو', 'Petro Atlético': 'بترو أتلتيكو', 'Wiliete Benguela': 'ويليتي بنجيلا',
    'Club Africain': 'النادي الأفريقي', 'MC Alger': 'مولودية الجزائر', 'Young Africans': 'يانج أفريكانز', Colombe: 'كولومب', 'Orlando Pirates': 'أورلاندو بايرتس',
    'RS Berkane': 'نهضة بركان', 'Simba SC': 'سيمبا', Simba: 'سيمبا', 'Al Hilal Omdurman': 'الهلال السوداني', 'Horoya AC': 'حوريا', Horoya: 'حوريا', 'MAS Fès': 'المغرب الفاسي',
    'Mamelodi Sundowns': 'صن داونز', 'Wydad Casablanca': 'الوداد', 'Raja Casablanca': 'الرجاء', 'Al Ahli Tripoli': 'الأهلي طرابلس', 'FAR Rabat': 'الجيش الملكي',
    'CS Sfaxien': 'الصفاقسي', 'USM Alger': 'اتحاد العاصمة', 'TP Mazembe': 'مازيمبي', 'Primeiro de Agosto': 'بريميرو دي أجوستو', 'ASKO Kara': 'أسكو كارا', 'Azam FC': 'عزام',
    Azam: 'عزام', 'Durban City': 'ديربان سيتي', 'Maniema Union': 'مانييما يونيون', 'Red Arrows': 'ريد أروز', 'Singida Black Stars': 'سينجيدا بلاك ستارز',
    'Black Bulls Maputo': 'بلاك بولز', 'AS Réal Bamako': 'ريال باماكو', 'CR Belouizdad': 'شباب بلوزداد', 'JS Kabylie': 'شبيبة القبائل', 'Étoile du Sahel': 'النجم الساحلي',
    'Al Merrikh': 'المريخ', 'ASEC Mimosas': 'أسيك ميموزا',
    // National teams
    Egypt: 'مصر', Morocco: 'المغرب', Algeria: 'الجزائر', Tunisia: 'تونس', Senegal: 'السنغال', Nigeria: 'نيجيريا', Ghana: 'غانا', Cameroon: 'الكاميرون', 'Ivory Coast': 'كوت ديفوار',
    'South Africa': 'جنوب أفريقيا', Mali: 'مالي', Comoros: 'جزر القمر', Zambia: 'زامبيا', Angola: 'أنجولا', Zimbabwe: 'زيمبابوي', Tanzania: 'تنزانيا', Uganda: 'أوغندا',
    'DR Congo': 'الكونغو الديمقراطية', Benin: 'بنين', Botswana: 'بوتسوانا', 'Burkina Faso': 'بوركينا فاسو', Sudan: 'السودان', 'Equatorial Guinea': 'غينيا الاستوائية',
    Mozambique: 'موزمبيق', Gabon: 'الجابون', 'Cape Verde': 'الرأس الأخضر', Libya: 'ليبيا', Mauritania: 'موريتانيا', Guinea: 'غينيا', Kenya: 'كينيا',
    'Saudi Arabia': 'السعودية', Qatar: 'قطر', Jordan: 'الأردن', Iraq: 'العراق', Iran: 'إيران', Japan: 'اليابان', 'South Korea': 'كوريا الجنوبية', Australia: 'أستراليا',
    Uzbekistan: 'أوزبكستان', 'United Arab Emirates': 'الإمارات', Oman: 'عمان', Syria: 'سوريا', Palestine: 'فلسطين', Lebanon: 'لبنان', Bahrain: 'البحرين', Kuwait: 'الكويت',
    Mexico: 'المكسيك', USA: 'الولايات المتحدة', Canada: 'كندا', Panama: 'بنما', Haiti: 'هايتي', 'Curacao': 'كوراساو', 'Costa Rica': 'كوستاريكا', Jamaica: 'جامايكا',
    Brazil: 'البرازيل', Argentina: 'الأرجنتين', Uruguay: 'أوروجواي', Colombia: 'كولومبيا', Ecuador: 'الإكوادور', Paraguay: 'باراجواي', Peru: 'بيرو', Chile: 'تشيلي',
    'New Zealand': 'نيوزيلندا', England: 'إنجلترا', France: 'فرنسا', Germany: 'ألمانيا', Spain: 'إسبانيا', Portugal: 'البرتغال', Netherlands: 'هولندا', Belgium: 'بلجيكا',
    Italy: 'إيطاليا', Croatia: 'كرواتيا', Switzerland: 'سويسرا', Austria: 'النمسا', Sweden: 'السويد', Norway: 'النرويج', Denmark: 'الدنمارك', Scotland: 'اسكتلندا',
    Czechia: 'التشيك', 'Bosnia and Herzegovina': 'البوسنة والهرسك', Turkiye: 'تركيا', 'Türkiye': 'تركيا', Turkey: 'تركيا', Poland: 'بولندا', Serbia: 'صربيا', Ukraine: 'أوكرانيا',
    Wales: 'ويلز', Hungary: 'المجر', Romania: 'رومانيا', Slovakia: 'سلوفاكيا', Slovenia: 'سلوفينيا', Greece: 'اليونان', Ireland: 'أيرلندا', Albania: 'ألبانيا', Georgia: 'جورجيا',
  };
  const SUFFIX = /\s+(FC|SC|CF|AFC|AC|SAD|FK|Club)$|^(FC|AFC|SC|AC)\s+/;
  /** The Arabic name of a club or national team, or the name as given when it isn't known */
  const name = (n) => {
    if (!n) return n;
    if (AR[n]) return AR[n];
    const bare = String(n).replace(SUFFIX, '').trim();
    return AR[bare] || n;
  };
  SA.data.clubsAr = { name, map: AR };
})(window.SA);
