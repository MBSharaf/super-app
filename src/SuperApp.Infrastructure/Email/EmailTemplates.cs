using System.Net;

namespace SuperApp.Infrastructure.Email;

/// <summary>
/// The email texts in Arabic and English. Each template has a subject and paragraphs; {name}, {link} and other
/// model values are filled in (HTML-encoded). A template with a link shows it as a button.
/// </summary>
public static class EmailTemplates
{
    private sealed record Text(string Subject, string[] Paragraphs, string? Button);

    private static readonly Dictionary<(string Template, string Language), Text> All = new()
    {
        [("confirm-email", "en")] = new("Confirm your email — Super App",
            ["Hi {name},", "Welcome to Super App! Please confirm your email so we can send you booking and order updates.", "This link works for 2 days."], "Confirm my email"),
        [("confirm-email", "ar")] = new("أكّد إيميلك — Super App",
            ["أهلاً {name}،", "أهلاً بيك في Super App! أكّد إيميلك عشان نقدر نبعتلك تحديثات الحجوزات والطلبات.", "اللينك ده شغال يومين."], "تأكيد الإيميل"),
        [("reset-password", "en")] = new("Reset your password — Super App",
            ["Hi {name},", "Someone asked to reset the password of your Super App account. If it was you, use the button below.", "The link works for 30 minutes. If you did not ask for this, ignore this email — your password stays the same."], "Choose a new password"),
        [("reset-password", "ar")] = new("تغيير الباسورد — Super App",
            ["أهلاً {name}،", "فيه طلب لتغيير باسورد حسابك على Super App. لو إنت اللي طلبت، دوس على الزرار.", "اللينك شغال نص ساعة بس. لو ماطلبتش ده، تجاهل الإيميل والباسورد هيفضل زي ما هو."], "اختار باسورد جديد"),
        [("admin-invite", "en")] = new("You are invited to the Super App team",
            ["Hi {name},", "The CEO added you to the Super App team as: {roles}.", "Choose your password with the button below. At your first sign-in you will set up an authenticator app (Google or Microsoft Authenticator) — it is required for every team account.", "The invitation works for 7 days."], "Accept the invitation"),
        [("admin-invite", "ar")] = new("دعوة لفريق Super App",
            ["أهلاً {name}،", "المدير التنفيذي ضافك لفريق Super App بدور: {roles}.", "اختار الباسورد بتاعك من الزرار تحت. أول ما تدخل هتفعّل تطبيق التحقق (Google أو Microsoft Authenticator)، وده إجباري لكل حسابات الفريق.", "الدعوة شغالة 7 أيام."], "قبول الدعوة"),
    };

    /// <summary>Adds templates from other features (admin invitations, application decisions…).</summary>
    public static void Register(string template, string language, string subject, string[] paragraphs, string? button) =>
        All[(template, language)] = new Text(subject, paragraphs, button);

    public static (string Subject, string Html) Render(string template, string language, IReadOnlyDictionary<string, string> model)
    {
        var text = All.GetValueOrDefault((template, language)) ?? All.GetValueOrDefault((template, "en"))
            ?? throw new InvalidOperationException($"Email template '{template}' does not exist.");
        var paragraphs = string.Concat(text.Paragraphs.Select(p => $"<p style=\"margin:0 0 14px\">{Fill(p, model)}</p>"));
        var button = text.Button is null || !model.TryGetValue("link", out var link) ? "" :
            $"<p style=\"margin:22px 0\"><a href=\"{WebUtility.HtmlEncode(link)}\" style=\"background:#0B6E4F;color:#fff;padding:12px 22px;border-radius:10px;text-decoration:none;font-weight:700\">{WebUtility.HtmlEncode(text.Button)}</a></p>"
            + $"<p style=\"font-size:12px;color:#667\">{WebUtility.HtmlEncode(link)}</p>";
        var dir = language == "ar" ? "rtl" : "ltr";
        var html = $"<!doctype html><html lang=\"{language}\" dir=\"{dir}\"><meta charset=\"utf-8\"><body style=\"font-family:Cairo,Segoe UI,Arial,sans-serif;background:#F4F6F5;padding:24px;color:#14201B\">"
            + $"<div style=\"max-width:560px;margin:auto;background:#fff;border-radius:16px;padding:28px\"><h2 style=\"margin:0 0 18px;color:#0B6E4F\">SUPER APP</h2>{paragraphs}{button}</div></body></html>";
        return (Fill(text.Subject, model), html);
    }

    private static string Fill(string text, IReadOnlyDictionary<string, string> model) =>
        model.Aggregate(WebUtility.HtmlEncode(text), (s, kv) => s.Replace("{" + kv.Key + "}", WebUtility.HtmlEncode(kv.Value), StringComparison.Ordinal));
}
