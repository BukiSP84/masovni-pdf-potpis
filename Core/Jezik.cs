using System.Globalization;

namespace MasovniPdfPotpis.Core;

/// <summary>Jezik korisničkog interfejsa: BHS (bosanski/hrvatski/srpski, latinica) ili engleski.</summary>
public static class Jezik
{
    /// <summary>true = engleski.</summary>
    public static bool En { get; private set; }

    /// <summary>Tekst na trenutnom jeziku.</summary>
    public static string T(string bhs, string en) => En ? en : bhs;

    /// <summary>"bs", "en" ili "" (automatski prema jeziku Windowsa).</summary>
    public static void Postavi(string izbor)
    {
        if (izbor == "en") En = true;
        else if (izbor == "bs") En = false;
        else En = !JeBhs(CultureInfo.CurrentUICulture);
    }

    static bool JeBhs(CultureInfo c)
    {
        var l = c.TwoLetterISOLanguageName;
        return l is "bs" or "hr" or "sr" or "sh" or "cnr";
    }
}
