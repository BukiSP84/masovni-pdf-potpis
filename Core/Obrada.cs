namespace MasovniPdfPotpis.Core;

public sealed class RezultatFajla
{
    public string Putanja { get; set; } = "";
    public bool Uspjeh { get; set; }
    public string Poruka { get; set; } = "";
    public DateTime Vrijeme { get; set; } = DateTime.Now;
    public string HashPrije { get; set; } = "";
    public string HashPoslije { get; set; } = "";
    public DateTime? VremenskiZig { get; set; }
}

/// <summary>Pronalaženje PDF-ova i masovno potpisivanje s provjerom rezultata.</summary>
public static class Obrada
{
    /// <summary>PDF-ovi u folderu; podfolderi čije ime počinje s "_" (backup, _potpisano) se preskaču.</summary>
    public static List<string> NadjiPdf(string folder, bool rekurzivno)
    {
        var lista = new List<string>();
        void Idi(string dir)
        {
            try { lista.AddRange(Directory.GetFiles(dir, "*.pdf").OrderBy(f => f, StringComparer.OrdinalIgnoreCase)); } catch { }
            if (!rekurzivno) return;
            string[] pod;
            try { pod = Directory.GetDirectories(dir); } catch { return; }
            foreach (var d in pod.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                if (!System.IO.Path.GetFileName(d).StartsWith("_")) Idi(d);
        }
        Idi(folder);
        return lista;
    }

    /// <summary>
    /// Potpisuje jedan fajl. Rezultat se prvo napravi kao privremeni fajl, provjeri
    /// (potpis ispravan, pokriva cijeli dokument) i tek onda zamijeni original / spremi kopiju.
    /// Ako bilo šta ne uspije, original ostaje netaknut.
    /// </summary>
    public static RezultatFajla PotpisiJedan(string putanja, string korijen, NacinSpremanja nacin, string backupFolder,
        PotpisCertifikatom potpis, string ime, OpcijePotpisa op, iText.Commons.Bouncycastle.Cert.IX509Certificate[] lanac)
    {
        var rez = new RezultatFajla { Putanja = putanja };
        string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mpp_" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var prije = Provjera.Analiziraj(putanja);
            if (prije.Status == StatusDokumenta.Greska)
                throw new Exception("PDF se ne može pročitati: " + prije.Greska);

            rez.HashPrije = Sha256(putanja);
            var opFajl = op;
            if (op.Mjesto == MjestoPecata.NovaStranica && prije.Potpisi.Count > 0)
            {
                // u već potpisan PDF ne dodajemo stranicu (Adobe bi to javio kao izmjenu)
                opFajl = op.Kopija();
                opFajl.Mjesto = MjestoPecata.ZadnjaStranica;
                rez.Poruka = "već potpisan: pečat na zadnjoj stranici, bez nove stranice. ";
            }

            Potpisnik.Potpisi(putanja, tmp, potpis, ime, opFajl, lanac);

            // provjera rezultata
            var poslije = Provjera.Analiziraj(tmp);
            var novi = poslije.Potpisi.LastOrDefault();
            if (poslije.Status != StatusDokumenta.Potpisan || novi == null || !novi.Ispravan || !novi.PokrivaCijeliDokument
                || poslije.Potpisi.Count != prije.Potpisi.Count + 1)
                throw new Exception("provjera potpisanog fajla nije prošla (" + poslije.StatusTekst + ")");
            if (prije.Potpisi.Count > 0 && poslije.Potpisi.Take(prije.Potpisi.Count).Any(p => !p.Ispravan))
                throw new Exception("raniji potpis bi postao neispravan");
            if (!string.IsNullOrWhiteSpace(op.TsaUrl) && (novi.VremenskiZig == null || !novi.VremenskiZigIspravan))
                throw new Exception("vremenski žig nije ispravno ugrađen");
            rez.HashPoslije = Sha256(tmp);
            rez.VremenskiZig = novi.VremenskiZig;

            string rel = System.IO.Path.GetRelativePath(korijen, putanja);
            if (nacin == NacinSpremanja.ZamijeniOriginal)
            {
                string b = System.IO.Path.Combine(backupFolder, rel);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(b)!);
                File.Copy(putanja, b, true);
                var attr = File.GetAttributes(putanja);
                if (attr.HasFlag(FileAttributes.ReadOnly)) File.SetAttributes(putanja, attr & ~FileAttributes.ReadOnly);
                File.Copy(tmp, putanja, true);
            }
            else
            {
                string cilj = System.IO.Path.Combine(korijen, "_potpisano", rel);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(cilj)!);
                File.Copy(tmp, cilj, true);
            }
            rez.Uspjeh = true;
            rez.Poruka += "potpisano";
        }
        catch (Exception ex)
        {
            rez.Uspjeh = false;
            rez.Poruka = Poruka(ex) + " — original nije diran";
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        }
        return rez;
    }

    public static string Sha256(string putanja)
    {
        using var f = File.OpenRead(putanja);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(f));
    }

    static string Poruka(Exception ex)
    {
        for (var x = ex; x != null; x = x.InnerException)
            if (x is VremenskiZigException) return x.Message;
        var e = ex;
        while (e.InnerException != null) e = e.InnerException;
        if (e is System.Security.Cryptography.CryptographicException)
            return "certifikat/token: " + e.Message + " (je li token utaknut i PIN ispravan?)";
        return e.Message;
    }
}

public static class Stranice
{
    /// <summary>Broj stranica i veličina (u tačkama) svake stranice.</summary>
    public static List<(float Sirina, float Visina)> Velicine(string putanja)
    {
        using var reader = new iText.Kernel.Pdf.PdfReader(putanja);
        reader.SetUnethicalReading(true);
        using var doc = new iText.Kernel.Pdf.PdfDocument(reader);
        var l = new List<(float, float)>();
        for (int i = 1; i <= doc.GetNumberOfPages(); i++)
        {
            var r = doc.GetPage(i).GetPageSize();
            l.Add((r.GetWidth(), r.GetHeight()));
        }
        return l;
    }
}
