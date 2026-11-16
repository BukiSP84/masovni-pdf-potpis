using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace MasovniPdfPotpis.Core;

/// <summary>
/// Trajni dnevnik potpisivanja (CSV, otvara se u Excelu): ko je, kada, kojim certifikatom potpisao koji fajl,
/// te SHA-256 otisak fajla prije i poslije potpisa. Čuva se uz dokumente, u izabranom folderu.
/// </summary>
public static class Dnevnik
{
    public const string ImeFajla = "_dnevnik_potpisivanja.csv";

    static string Zaglavlje => Jezik.En
        ? string.Join(";", "Time", "File", "Result", "Signer", "Certificate issuer", "Certificate serial number", "Thumbprint",
            "Windows user", "Computer", "Time-stamp (TSA)", "SHA-256 before", "SHA-256 after", "Note")
        : string.Join(";", "Vrijeme", "Fajl", "Rezultat", "Potpisnik", "Izdavač certifikata",
            "Serijski broj certifikata", "Thumbprint", "Windows korisnik", "Računar", "Vremenski žig (TSA)",
            "SHA-256 prije", "SHA-256 poslije", "Napomena");

    /// <summary>Dopisuje stavke; vraća putanju dnevnika (u folderu ili, ako nije moguće, u %APPDATA%).</summary>
    public static string Upisi(string korijen, X509Certificate2 cert, IEnumerable<RezultatFajla> stavke)
    {
        var redovi = stavke.Select(r => string.Join(";",
            r.Vrijeme.ToString("yyyy-MM-dd HH:mm:ss"),
            Polje(Path.GetRelativePath(korijen, r.Putanja)),
            r.Uspjeh ? Jezik.T("potpisano", "signed") : Jezik.T("greška", "error"),
            Polje(cert.GetNameInfo(X509NameType.SimpleName, false)),
            Polje(cert.GetNameInfo(X509NameType.SimpleName, true)),
            cert.SerialNumber,
            cert.Thumbprint,
            Polje(Environment.UserName),
            Polje(Environment.MachineName),
            r.VremenskiZig?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
            r.HashPrije,
            r.HashPoslije,
            Polje(r.Poruka))).ToList();
        if (redovi.Count == 0) return null;

        foreach (var putanja in new[]
        {
            Path.Combine(korijen, ImeFajla),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MasovniPdfPotpis", "dnevnik_potpisivanja.csv")
        })
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(putanja)!);
                bool nov = !File.Exists(putanja);
                using var w = new StreamWriter(putanja, append: true, new UTF8Encoding(encoderShouldEmitUTF8Identifier: nov));
                if (nov) w.WriteLine(Zaglavlje);
                foreach (var red in redovi) w.WriteLine(red);
                return putanja;
            }
            catch { /* probaj sljedeće mjesto */ }
        }
        return null;
    }

    static string Polje(string s)
    {
        s ??= "";
        return s.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}
