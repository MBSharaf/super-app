/** Arabic dictionary — every fantasy league is free; the Super App League's weekly Super Points; card-only pitch deposits (2026-11-03). */
(function (SA) {
  const D = SA.data.i18nAr;
  const add = (o) => Object.entries(o).forEach(([k, v]) => { if (!(k in D.strings)) D.strings[k] = v; });
  add({
    /* ---------- Fantasy: leagues ---------- */
    'Free · Super App': 'مجاني · Super App', 'Every gameweek': 'كل جولة', 'Top {0} win Super Points': 'أول {0} بيكسبوا Super Points',
    'Every league is free. The Super App League is run by Super App: the top {0} of every gameweek win {1} / {2} / {3} Super Points.': 'كل الدوريات مجانية. دوري Super App بتديره Super App: أول {0} في كل جولة بيكسبوا {1} / {2} / {3} Super Points.',
    'Weekly winners': 'فايزين الجولة', '· {0} pts': '· {0} نقطة', 'The first winners appear when this gameweek is finished.': 'أول فايزين هيظهروا لما الجولة دي تخلص.',
    'Every gameweek, by gameweek points. The Super Points go straight to the winners’ accounts. No cash prizes.': 'كل جولة، على حسب نقط الجولة. الـ Super Points بتنزل على حساب الفايزين على طول. مفيش جوايز فلوس.',
    'Each gameweek is a new race: the {0} highest gameweek scores win': 'كل جولة سباق جديد: أعلى {0} في نقط الجولة بيكسبوا',
    'Free for every manager — no entry fee': 'مجاني لكل المدربين — من غير رسوم اشتراك',
    '{0}st every gameweek': 'المركز {0} كل جولة', '{0}nd every gameweek': 'المركز {0} كل جولة', '{0}rd every gameweek': 'المركز {0} كل جولة',
    'Super App’s own league, run by the Super App team. Everyone who builds a team is in it. The top {0} of every gameweek win Super Points.': 'دوري Super App الرسمي، وبيديره فريق Super App. أي حد بيعمل فريق بيدخله تلقائي. أول {0} في كل جولة بيكسبوا Super Points.',
    'Super App’s own league, run by the Super App team. Everyone who builds an English Premier League team is in it. The top {0} of every gameweek win Super Points.': 'دوري Super App الرسمي، وبيديره فريق Super App. أي حد بيعمل فريق في الدوري الإنجليزي بيدخله تلقائي. أول {0} في كل جولة بيكسبوا Super Points.',
    'You finished #{0} in gameweek {1}': 'إنت جيت رقم {0} في الجولة {1}', '{0} Super Points added for the Super App League.': 'اتضافلك {0} Super Points من دوري Super App.',
    'Pick {0} players with a {1}M budget. Score points every gameweek from their goals, assists and clean sheets — then beat your friends in private groups. Every league is free, and the top {2} of every gameweek in the Super App League win Super Points.': 'اختار {0} لاعب بميزانية {1}M. اكسب نقط كل جولة من أهدافهم وأسيستاتهم والكلين شيت — واغلب صحابك في الجروبات الخاصة. كل الدوريات مجانية، وأول {2} في كل جولة في دوري Super App بيكسبوا Super Points.',
    'Free fantasy leagues. The top {0} of every gameweek in the Super App League win Super Points.': 'دوريات فانتازي مجانية. أول {0} في كل جولة في دوري Super App بيكسبوا Super Points.',
    'Free leagues': 'دوريات مجانية', 'Free · {0} managers': 'مجاني · {0} مدرب', '{0} pts / GW': '{0} نقطة / جولة',
    '— booking deposits, the rest paid at the pitch, games, the shop and tournaments. The points land the moment you pay. The top {0} of every gameweek in the Super App fantasy league win points too.': '— عربون الحجز، والباقي اللي بيتدفع في الملعب، والماتشات، والمتجر والبطولات. النقط بتنزل أول ما تدفع. وأول {0} في كل جولة في دوري Super App للفانتازي بيكسبوا نقط كمان.',
    'Use it at checkout in Super App: games, the shop, printed designs and tournament entries. It can’t be exchanged for cash.': 'استخدمها وإنت بتدفع في Super App: الماتشات، والمتجر، والتصميمات المطبوعة والبطولات. ومينفعش تتحول لفلوس.',
    /* ---------- Admin ---------- */
    'Super App · Free': 'Super App · مجاني', 'Leagues': 'الدوريات', 'Weekly points': 'نقط الجولة', 'Super App League — weekly points': 'دوري Super App — نقط كل جولة',
    'Every league is free. In the Super App League, the {0} highest gameweek scores win Super Points after each gameweek. The points go to their accounts automatically.': 'كل الدوريات مجانية. في دوري Super App، أعلى {0} في نقط الجولة بيكسبوا Super Points بعد كل جولة. والنقط بتنزل على حساباتهم تلقائي.',
    '{0}st place (points)': 'المركز {0} (نقط)', '{0}nd place (points)': 'المركز {0} (نقط)', '{0}rd place (points)': 'المركز {0} (نقط)',
    'Save weekly points': 'احفظ نقط الجولة', 'Gameweeks paid out': 'جولات اتصرفت جوايزها', 'Super Points given': 'Super Points اتوزعت',
    'No entry fees and no cash prizes.': 'من غير رسوم اشتراك ومن غير جوايز فلوس.', '{0} pts · +{1} Super Points': '{0} نقطة · +{1} Super Points',
    'No gameweek finished yet.': 'مفيش جولة خلصت لسه.', 'Weekly Super Points ({0} days)': 'Super Points الجولات ({0} يوم)', 'Weekly points saved': 'نقط الجولة اتحفظت',
    'Free leagues, the Super App League’s weekly points, sponsors, winners and the competitions on the matches page': 'الدوريات المجانية، ونقط جولات دوري Super App، والرعاة، والفايزين والبطولات في صفحة الماتشات',
    'Every fantasy league is free. In the Super App League, the top {0} of every gameweek win {1} / {2} / {3} Super Points.': 'كل دوريات الفانتازي مجانية. في دوري Super App، أول {0} في كل جولة بيكسبوا {1} / {2} / {3} Super Points.',
    'Change weekly points': 'غيّر نقط الجولة', 'Commission, VAT, sellers, fantasy and demo data': 'العمولة، والضريبة، والبائعين، والفانتازي وبيانات الديمو',
    'They can still browse, but can’t book, order or join games.': 'هيقدر يتصفح بس، لكن مش هيقدر يحجز أو يطلب أو يدخل ماتشات.',
  });
})(window.SA);
