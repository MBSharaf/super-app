namespace SuperApp.Application.Security;

/// <summary>One permission: a code checked on the server, its area, names for the CEO matrix, and whether it moves money or data.</summary>
public sealed record PermissionInfo(string Code, string Area, string NameEn, string NameAr, bool IsSensitive = false);

/// <summary>
/// Every permission code (API document §7.1). Endpoints check these codes; the seed writes this list to
/// auth.Permissions so the CEO permission matrix shows the same names. Add a code here, never only in the database.
/// </summary>
public static class Permissions
{
    public static readonly IReadOnlyList<PermissionInfo> All =
    [
        new("approvals.partners", "Approvals", "Approve pitch partners", "الموافقة على الشركاء", true),
        new("approvals.sellers", "Approvals", "Approve sellers", "الموافقة على البائعين", true),
        new("approvals.factories", "Approvals", "Approve factories", "الموافقة على المصانع", true),
        new("approvals.pitches", "Approvals", "Approve new pitches", "الموافقة على الملاعب الجديدة"),
        new("approvals.packages", "Approvals", "Approve package discounts", "الموافقة على خصومات الباقات"),
        new("approvals.catalogue", "Approvals", "Approve catalogue changes", "الموافقة على تغييرات الكتالوج"),

        new("customers.read", "People", "See customers", "عرض العملاء"),
        new("customers.manage", "People", "Manage customers", "إدارة العملاء", true),
        new("partners.read", "People", "See partners", "عرض الشركاء"),
        new("partners.manage", "People", "Manage partners", "إدارة الشركاء", true),
        new("sellers.read", "People", "See sellers", "عرض البائعين"),
        new("sellers.manage", "People", "Manage sellers", "إدارة البائعين", true),
        new("kyc.read", "People", "See ID documents", "عرض مستندات الهوية", true),
        new("kyc.verify", "People", "Verify ID documents", "توثيق مستندات الهوية", true),
        new("factories.read", "People", "See factories", "عرض المصانع"),
        new("factories.manage", "People", "Manage factories", "إدارة المصانع", true),

        new("orders.read", "Orders", "See orders", "عرض الطلبات"),
        new("orders.manage", "Orders", "Change orders", "تعديل الطلبات"),
        new("bookings.read", "Orders", "See bookings", "عرض الحجوزات"),
        new("bookings.manage", "Orders", "Change bookings", "تعديل الحجوزات"),
        new("returns.manage", "Orders", "Handle returns", "إدارة المرتجعات"),
        new("shipping.read", "Orders", "See shipments", "عرض الشحنات"),
        new("shipping.manage", "Orders", "Manage shipping", "إدارة الشحن"),

        new("payouts.read", "Money", "See withdrawals", "عرض طلبات السحب", true),
        new("payouts.prepare", "Money", "Prepare withdrawals", "تجهيز طلبات السحب", true),
        new("payouts.send", "Money", "Send withdrawals", "إرسال طلبات السحب", true),
        new("refunds.issue", "Money", "Refund customers", "رد المبالغ للعملاء", true),
        new("credits.issue", "Money", "Credit customers", "إضافة رصيد للعملاء", true),
        new("revenue.read", "Money", "See revenue", "عرض الإيرادات", true),
        new("finance.approve", "Money", "Approve money requests", "اعتماد الطلبات المالية", true),

        new("support.read", "Support", "See tickets", "عرض التذاكر"),
        new("support.reply", "Support", "Reply to tickets", "الرد على التذاكر"),
        new("support.assign", "Support", "Assign tickets", "توزيع التذاكر"),
        new("disputes.decide", "Support", "Decide disputes", "الفصل في النزاعات", true),

        new("designs.review", "POD", "Review designs", "مراجعة التصميمات"),
        new("designs.reports", "POD", "Handle design reports", "إدارة بلاغات التصميمات"),
        new("levels.review", "POD", "Run the monthly level review", "مراجعة المستويات الشهرية"),
        new("samples.manage", "POD", "Manage samples", "إدارة العينات"),
        new("catalogue.manage", "POD", "Manage the printable catalogue", "إدارة كتالوج الطباعة"),

        new("codes.manage", "Marketing", "Create promo codes", "إنشاء أكواد الخصم"),
        new("broadcasts.send", "Marketing", "Send broadcasts", "إرسال الرسائل الجماعية"),

        new("content.banners", "Content", "Edit banners", "تعديل البانرات"),
        new("content.popups", "Content", "Edit pop-ups", "تعديل النوافذ المنبثقة"),
        new("content.news", "Content", "Edit news", "تعديل الأخبار"),
        new("content.help", "Content", "Edit help articles", "تعديل مقالات المساعدة"),
        new("content.legal", "Content", "Edit legal documents", "تعديل المستندات القانونية", true),
        new("content.categories", "Content", "Edit categories", "تعديل الفئات"),
        new("content.holidays", "Content", "Confirm public holidays", "تأكيد الإجازات الرسمية"),
        new("games.moderate", "Content", "Moderate games", "الإشراف على الماتشات"),

        new("community.moderate", "Community", "Moderate communities", "الإشراف على المجتمعات"),
        new("community.manage", "Community", "Create and edit communities", "إنشاء وتعديل المجتمعات"),
        new("community.postAsAdmin", "Community", "Post as Super App", "النشر باسم سوبر آب"),

        new("fantasy.manage", "Fantasy", "Run fantasy", "إدارة الفانتازي"),
        new("fantasy.groups.paid", "Fantasy", "Manage paid groups", "إدارة المجموعات المدفوعة", true),

        new("tournaments.manage", "Tournaments", "Run tournaments", "إدارة البطولات"),
        new("store.official", "Official store", "Run the official store", "إدارة المتجر الرسمي"),

        new("analytics.read", "Analytics", "See reports", "عرض التقارير"),
        new("analytics.export", "Analytics", "Export to Excel", "التصدير إلى إكسل", true),

        new("settings.platform", "Platform", "Change platform settings", "تعديل إعدادات المنصة", true),
        new("audit.read", "Platform", "Read the audit log", "قراءة سجل العمليات", true),
        new("maintenance.toggle", "Platform", "Switch maintenance mode", "تشغيل وضع الصيانة", true),
    ];

    /// <summary>True when <paramref name="code"/> exists in the catalogue.</summary>
    public static bool Exists(string code) => All.Any(p => p.Code == code);
}
