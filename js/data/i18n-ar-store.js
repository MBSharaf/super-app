/** Arabic dictionary — part 16: the store back for sellers' own products (R6.7: print on demand off, card / wallet only, 24-hour returns). */
(function (SA) {
  const D = SA.data.i18nAr;
  Object.assign(D.strings, {
    /* ---------- Paused parts ---------- */
    'The Super App store is coming soon': 'متجر سوبر آب جاي قريب',
    'We’re getting the shop, sellers’ designs and delivery ready. Meanwhile, book a pitch or join a game.': 'بنجهّز المتجر وتصميمات البايعين والتوصيل. لحد ما يجهز، احجز ملعب أو انضم لماتش.',
    'Print on demand is coming soon': 'الطباعة حسب الطلب جاية قريب',
    'Sellers sell their own products for now. Printing your designs on tees and hoodies comes later.': 'البايعين بيبيعوا منتجاتهم بس دلوقتي. طباعة تصميماتك على التيشيرتات والهوديز جاية بعدين.',
    /* ---------- Sell on Super App (own products only) ---------- */
    'Sell products you already have. You keep the stock, our courier delivers every order, and we take 5% of each sale.': 'بيع منتجات عندك. المخزون عندك، والمندوب بتاعنا بيوصّل كل أوردر، وبناخد 5% من كل عملية بيع.',
    'Sell your products': 'بيع منتجاتك',
    'Open a store for the sports gear you already have. Customers pay online, our courier delivers, and your money lands in your wallet.': 'افتح متجر للأدوات الرياضية اللي عندك. العملاء بيدفعوا أونلاين، والمندوب بتاعنا بيوصّل، وفلوسك بتنزل في محفظتك.',
    'We deliver': 'إحنا بنوصّل',
    'Paid to your wallet': 'الفلوس في محفظتك',
    '5% per sale': '5% على كل عملية بيع',
    'Your money becomes available 24 hours after delivery (the return window).': 'فلوسك بتبقى متاحة بعد 24 ساعة من التسليم (مدة المرتجعات).',
    'Only sell genuine products you own and can hand to our courier.': 'بيع بس منتجات أصلية بتاعتك وتقدر تسلّمها للمندوب بتاعنا.',
    'No fakes or copies of other brands. Every product is reviewed before it goes on sale.': 'ممنوع التقليد أو نسخ الماركات التانية. كل منتج بيتراجع قبل ما يتعرض للبيع.',
    'Customers pay online. Our courier collects from you and delivers. Super App takes 5% of each sale.': 'العملاء بيدفعوا أونلاين. المندوب بتاعنا بيستلم منك ويوصّل. سوبر آب بياخد 5% من كل عملية بيع.',
    'A product with a problem can be returned within 24 hours of delivery — the refund and the return delivery are on you.': 'المنتج اللي فيه مشكلة ممكن يرجع خلال 24 ساعة من التسليم — والاسترجاع وشحن الرجوع عليك.',
    'We review your store within one working day.': 'بنراجع متجرك خلال يوم عمل واحد.',
    'Each product is checked before it goes on sale.': 'كل منتج بيتراجع قبل ما يتعرض للبيع.',
    'Customers pay online and our courier collects and delivers.': 'العملاء بيدفعوا أونلاين والمندوب بتاعنا بيستلم ويوصّل.',
    'Get paid': 'استلم فلوسك',
    'Your money is available 24 hours after delivery. Withdraw to InstaPay or your bank.': 'فلوسك بتبقى متاحة بعد 24 ساعة من التسليم. اسحبها على InstaPay أو البنك.',
    /* ---------- Checkout and orders (online payment only) ---------- */
    'Waiting for your payment.': 'مستنيين الدفع.',
    'Pay within 15 minutes to keep the items — after that the order is called off.': 'ادفع خلال 15 دقيقة عشان تحجز المنتجات — بعد كده الطلب بيتلغي.',
    'This order was cancelled.': 'الطلب ده اتلغى.',
    /* ---------- Returns (an item with a problem, 24 hours) ---------- */
    'Within {0} hours of delivery — {1} hour(s) left.': 'خلال {0} ساعة من التسليم — فاضل {1} ساعة.',
    'Returns are for an item with a problem, within 24 hours of delivery. Our courier collects it and the refund goes to your Super App wallet.': 'المرتجعات للمنتج اللي فيه مشكلة بس، خلال 24 ساعة من التسليم. المندوب بتاعنا بيستلمه والفلوس بترجع على محفظة سوبر آب بتاعتك.',
    'Choose what is wrong with the item.': 'اختار إيه المشكلة في المنتج.',
    'e.g. The seam came apart on the first day': 'مثلًا: الخياطة فكّت من أول يوم',
    'Super App courier': 'مندوب سوبر آب',
    'The 24-hour window closed or it is already being returned': 'مدة الـ 24 ساعة خلصت أو المنتج بيرجع بالفعل',
    /* ---------- Seller store products (each one reviewed) ---------- */
    'Waiting for review': 'مستني المراجعة',
    'Fix and send again': 'عدّله وابعته تاني',
    'Super App checks every product before it goes on sale': 'سوبر آب بيراجع كل منتج قبل ما يتعرض للبيع',
    'Super App checks each product before it goes on sale — usually within one working day.': 'سوبر آب بيراجع كل منتج قبل ما يتعرض للبيع — غالبًا خلال يوم عمل واحد.',
    'Sent to Super App — it goes on sale once approved': 'اتبعت لسوبر آب — هيتعرض للبيع أول ما يتوافق عليه',
    /* ---------- Store orders and returns (Super App courier) ---------- */
    'Hand to Super App courier': 'سلّمه لمندوب سوبر آب',
    'Handed to Super App’s courier — the customer has the tracking number': 'اتسلّم لمندوب سوبر آب — العميل معاه رقم التتبع',
    'Available in your wallet 24 hours after delivery (the return window).': 'هتبقى متاحة في محفظتك بعد 24 ساعة من التسليم (مدة المرتجعات).',
    'Super App’s courier marks the order delivered when it reaches the customer.': 'مندوب سوبر آب هو اللي بيعلّم إن الطلب اتسلّم لما يوصل للعميل.',
    'Customers can return a product with a problem within 24 hours of delivery. You pay the refund and the return delivery.': 'العملاء يقدروا يرجّعوا المنتج اللي فيه مشكلة خلال 24 ساعة من التسليم. الاسترجاع وشحن الرجوع عليك.',
    'Answer within 2 working days · {0}-hour return window': 'رد خلال يومين عمل · مدة المرتجعات {0} ساعة',
  });
})(window.SA);
