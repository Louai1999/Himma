using System.Net;
using Himma.Api.Infrastructure.Localization;

namespace Himma.Api.Infrastructure.Email;

/// <summary>The few emails Himma sends, in the language of the request. Codes expire in about 10 minutes.</summary>
public static class EmailTemplates
{
    public static EmailMessage ConfirmEmail(string to, string code) => Build(
        to,
        Lang.T("رمز تأكيد بريدك في هِمّة", "Your Himma email confirmation code"),
        Lang.T("رمز تأكيد بريدك الإلكتروني:", "Your email confirmation code:"),
        code,
        Lang.T("اكتب هذا الرمز في التطبيق خلال 10 دقائق. إذا لم تطلبه، تجاهل هذه الرسالة.",
            "Enter this code in the app within 10 minutes. If you did not ask for it, ignore this email."));

    public static EmailMessage ResetPassword(string to, string username, string code) => Build(
        to,
        Lang.T("استرجاع حسابك في هِمّة", "Recover your Himma account"),
        Lang.T($"اسم المستخدم: {username}\nرمز تغيير كلمة المرور:", $"Username: {username}\nPassword reset code:"),
        code,
        Lang.T("اكتب الرمز في صفحة «نسيت كلمة المرور» خلال 10 دقائق. إذا لم تطلبه، تجاهل هذه الرسالة؛ حسابك بأمان.",
            "Enter the code on the “Forgot password” page within 10 minutes. If you did not ask for it, ignore this email; your account is safe."));

    private static EmailMessage Build(string to, string subject, string intro, string code, string footer)
    {
        var rtl = !Lang.IsEnglish;
        var text = $"{intro}\n\n{code}\n\n{footer}\n\nهِمّة · Himma";
        var introHtml = WebUtility.HtmlEncode(intro).Replace("\n", "<br>");
        var html = $"""
            <div dir="{(rtl ? "rtl" : "ltr")}" style="font-family:Tahoma,Arial,sans-serif;background:#F6F1E7;padding:24px">
              <div style="max-width:460px;margin:0 auto;background:#fff;border-radius:16px;padding:24px;color:#2B2620">
                <h2 style="color:#2F4A3C;margin:0 0 12px">هِمّة · Himma</h2>
                <p style="font-size:16px;line-height:1.7;margin:0 0 16px">{introHtml}</p>
                <p style="font-size:34px;letter-spacing:8px;font-weight:bold;text-align:center;color:#1F6B47;background:#E6EEE9;border-radius:12px;padding:12px;margin:0 0 16px" dir="ltr">{WebUtility.HtmlEncode(code)}</p>
                <p style="font-size:13px;color:#6E655A;line-height:1.7;margin:0">{WebUtility.HtmlEncode(footer)}</p>
              </div>
            </div>
            """;
        return new EmailMessage(to, subject, text, html);
    }
}
