using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using iText.Bouncycastle.X509;
using iText.Commons.Bouncycastle.Cert;
using iText.Commons.Bouncycastle.Crypto;
using iText.IO.Font;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Forms.Form.Element;
using iText.Signatures;
using iText.Layout;

namespace MasovniPdfPotpis.Core;

/// <summary>
/// Potpis privatnim ključem certifikata iz Windows spremišta (token, pametna kartica ili instaliran certifikat).
/// PIN traži driver tokena/kartice pri potpisu.
/// </summary>
public sealed class PotpisCertifikatom : IExternalSignature, IDisposable
{
    readonly X509Certificate2 _cert;
    RSA _rsa;
    ECDsa _ec;

    public PotpisCertifikatom(X509Certificate2 cert)
    {
        _cert = cert;
        if (!cert.HasPrivateKey) throw new InvalidOperationException("Certifikat nema privatni ključ.");
    }

    public string GetDigestAlgorithmName() => "SHA256";

    public string GetSignatureAlgorithmName()
    {
        // ne otvaramo ključ ovdje (to bi moglo tražiti PIN), gledamo javni ključ
        return _cert.GetECDsaPublicKey() != null ? "ECDSA" : "RSA";
    }

    public ISignatureMechanismParams GetSignatureMechanismParameters() => null;

#if MPP_TEST
    /// <summary>Samo za testiranje (bez Windows kriptografije): putanja|lozinka do .pfx.</summary>
    public static string TestPfx = Environment.GetEnvironmentVariable("MPP_TEST_PFX");
#endif

    public byte[] Sign(byte[] message)
    {
#if MPP_TEST
        if (!string.IsNullOrEmpty(TestPfx) && TestPfx.Contains('|'))
        {
            var d = TestPfx.Split('|', 2);
            using var fs = File.OpenRead(d[0]);
            var store = new Org.BouncyCastle.Pkcs.Pkcs12StoreBuilder().Build();
            store.Load(fs, d[1].ToCharArray());
            var alias = store.Aliases.Cast<string>().First(a => store.IsKeyEntry(a));
            var signer = Org.BouncyCastle.Security.SignerUtilities.GetSigner("SHA256withRSA");
            signer.Init(true, store.GetKey(alias).Key);
            signer.BlockUpdate(message, 0, message.Length);
            return signer.GenerateSignature();
        }
#endif
        if (_cert.GetECDsaPublicKey() != null)
        {
            for (int pokusaj = 1; ; pokusaj++)
            {
                try
                {
                    _ec ??= _cert.GetECDsaPrivateKey();
                    return _ec.SignData(message, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
                }
                catch (CryptographicException) when (pokusaj < 3)
                {
                    try { _ec?.Dispose(); } catch { }
                    _ec = null;
                    Thread.Sleep(300 * pokusaj);
                }
            }
        }
        // Neki driveri tokena (npr. Halcom) ponekad odbiju ponovnu upotrebu istog ključa ("internal consistency check failed"),
        // zato: pokušaj, a ako ne uspije, ponovo otvori ključ i pokušaj još jednom (do 3 puta).
        for (int pokusaj = 1; ; pokusaj++)
        {
            try
            {
                _rsa ??= _cert.GetRSAPrivateKey();   // ovdje driver tokena traži PIN
                return _rsa.SignData(message, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            catch (CryptographicException) when (pokusaj < 3)
            {
                try { _rsa?.Dispose(); } catch { }
                _rsa = null;
                Thread.Sleep(300 * pokusaj);
            }
        }
    }

    public IX509Certificate[] Lanac()
    {
        var lista = new List<X509Certificate2> { _cert };
        try
        {
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;
            chain.Build(_cert);
            foreach (var el in chain.ChainElements)
                if (!el.Certificate.Thumbprint.Equals(_cert.Thumbprint, StringComparison.OrdinalIgnoreCase))
                    lista.Add(el.Certificate);
        }
        catch { /* bez lanca potpis i dalje vrijedi */ }

        var parser = new Org.BouncyCastle.X509.X509CertificateParser();
        return lista.Select(c => (IX509Certificate)new X509CertificateBC(parser.ReadCertificate(c.RawData))).ToArray();
    }

    public void Dispose()
    {
        _rsa?.Dispose();
        _ec?.Dispose();
    }
}

public static class Certifikati
{
    /// <summary>Certifikati s privatnim ključem iz Windows spremišta (CurrentUser\My), potpisni prvi.</summary>
    public static List<X509Certificate2> ZaPotpisivanje()
    {
        var rezultat = new List<X509Certificate2>();
        try
        {
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            foreach (var c in store.Certificates)
                if (c.HasPrivateKey && c.NotAfter > DateTime.Now && c.NotBefore <= DateTime.Now
                    && c.SubjectName.RawData.AsSpan().SequenceEqual(c.IssuerName.RawData) == false   // bez samopotpisanih (npr. "Your Phone")
                    && ZaDokumente(c))
                    rezultat.Add(c);
        }
        catch { }
        return rezultat
            .OrderByDescending(JeZaPotpis)
            .ThenBy(c => c.GetNameInfo(X509NameType.SimpleName, false))
            .ToList();
    }

    /// <summary>Certifikat smije potpisivati (DigitalSignature ili NonRepudiation; bez KeyUsage = dozvoljeno).</summary>
    static bool ZaDokumente(X509Certificate2 c)
    {
        var ku = c.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault();
        return ku == null || ku.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature) || ku.KeyUsages.HasFlag(X509KeyUsageFlags.NonRepudiation);
    }

    public static bool JeZaPotpis(X509Certificate2 c) =>
        c.Extensions.OfType<X509KeyUsageExtension>().Any(k => k.KeyUsages.HasFlag(X509KeyUsageFlags.NonRepudiation));

    public static string Opis(X509Certificate2 c)
    {
        string ime = c.GetNameInfo(X509NameType.SimpleName, false);
        string izd = c.GetNameInfo(X509NameType.SimpleName, true);
        string vrsta = JeZaPotpis(c) ? "potpisni" : "NIJE potpisni";
        return $"{ime} — {izd} — {vrsta} — do {c.NotAfter:dd.MM.yyyy} — {c.Thumbprint[..8]}";
    }
}

/// <summary>Font za pečat: Myriad Pro iz Adobe instalacije (kao Acrobat), inače Segoe UI / Arial.</summary>
public static class FontPecata
{
    static FontProgram _program;
    static readonly object _lock = new();
    public static string IzabraniFont { get; private set; } = "";

    public static string[] Kandidati =
    {
        @"C:\Program Files\Adobe\Acrobat DC\Resource\Font\MyriadPro-Regular.otf",
        @"C:\Program Files (x86)\Adobe\Acrobat DC\Resource\Font\MyriadPro-Regular.otf",
        @"C:\Program Files\Adobe\Acrobat Reader DC\Resource\Font\MyriadPro-Regular.otf",
        @"C:\Program Files (x86)\Adobe\Acrobat Reader DC\Resource\Font\MyriadPro-Regular.otf",
        @"C:\Program Files\Adobe\Acrobat Reader\Resource\Font\MyriadPro-Regular.otf",
        @"C:\Program Files\Adobe\Acrobat\Resource\Font\MyriadPro-Regular.otf",
        @"C:\Windows\Fonts\segoeui.ttf",
        @"C:\Windows\Fonts\arial.ttf",
        // za testiranje na Linuxu
        "/usr/share/fonts/opentype/inter/Inter-Regular.otf",
        "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
    };

    public static PdfFont Napravi()
    {
        lock (_lock)
        {
            if (_program == null)
            {
                foreach (var k in Kandidati.Concat(TraziAdobe()))
                {
                    try
                    {
                        if (!File.Exists(k)) continue;
                        _program = FontProgramFactory.CreateFont(k);
                        IzabraniFont = k;
                        break;
                    }
                    catch { }
                }
            }
            if (_program == null)
                return PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA);
            return PdfFontFactory.CreateFont(_program, PdfEncodings.IDENTITY_H,
                PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
        }
    }

    static IEnumerable<string> TraziAdobe()
    {
        foreach (var baza in new[] { @"C:\Program Files\Adobe", @"C:\Program Files (x86)\Adobe" })
        {
            string[] nadjeni = Array.Empty<string>();
            try
            {
                if (Directory.Exists(baza))
                    nadjeni = Directory.GetFiles(baza, "MyriadPro-Regular.otf", SearchOption.AllDirectories);
            }
            catch { }
            foreach (var f in nadjeni) yield return f;
        }
    }
}

public sealed class OpcijePotpisa
{
    public MjestoPecata Mjesto { get; set; } = MjestoPecata.ZadnjaStranica;
    public Pravougaonik Pecat { get; set; } = new();
    public Pravougaonik PecatNovaStranica { get; set; } = new() { X = 60, Y = 600 };
    public bool Vidljiv { get; set; } = true;
    public string Razlog { get; set; } = "";
    public string Lokacija { get; set; } = "";
    /// <summary>URL servisa za vremenski žig (RFC 3161); prazno = bez vremenskog žiga.</summary>
    public string TsaUrl { get; set; } = "";
    /// <summary>Uz vremenski žig ugradi i OCSP odgovor (podatak da certifikat nije opozvan u trenutku potpisa).</summary>
    public bool UgradiOpoziv { get; set; } = true;

    public OpcijePotpisa Kopija() => (OpcijePotpisa)MemberwiseClone();
}

/// <summary>Greška pri dobijanju vremenskog žiga (nema interneta, pogrešan URL, servis ne radi).</summary>
public sealed class VremenskiZigException : Exception
{
    public VremenskiZigException(string poruka, Exception inner) : base(poruka, inner) { }
}

/// <summary>Omotač oko iText TSA klijenta s jasnom porukom greške.</summary>
sealed class TsaKlijent : ITSAClient
{
    readonly TSAClientBouncyCastle _tsa;
    readonly string _url;
    public TsaKlijent(string url) { _url = url; _tsa = new TSAClientBouncyCastle(url); }
    public int GetTokenSizeEstimate() => _tsa.GetTokenSizeEstimate();
    public iText.Commons.Digest.IMessageDigest GetMessageDigest() => _tsa.GetMessageDigest();
    public byte[] GetTimeStampToken(byte[] imprint)
    {
        try { return _tsa.GetTimeStampToken(imprint); }
        catch (Exception ex)
        {
            throw new VremenskiZigException($"vremenski žig nije dobijen od {_url} (provjeri internet i URL): {ex.Message}", ex);
        }
    }
}

public static class Potpisnik
{
    /// <summary>
    /// Potpisuje <paramref name="ulaz"/> i rezultat piše u <paramref name="izlaz"/>.
    /// Uvijek inkrementalno (append): postojeći sadržaj i postojeći potpisi ostaju netaknuti.
    /// </summary>
    public static void Potpisi(string ulaz, string izlaz, PotpisCertifikatom potpis, string imePotpisnika,
                               OpcijePotpisa op, IX509Certificate[] lanac = null)
    {
        lanac ??= potpis.Lanac();
        string izvor = ulaz;
        string privremeni = null;

        try
        {
            bool vecPotpisan = ImaPotpis(ulaz);
            bool novaStranica = op.Vidljiv && op.Mjesto == MjestoPecata.NovaStranica && !vecPotpisan;
            if (novaStranica)
            {
                privremeni = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mpp_" + Guid.NewGuid().ToString("N") + ".pdf");
                DodajStranicuZaPotpis(ulaz, privremeni);
                izvor = privremeni;
            }

            var sada = DateTime.Now;
            using (var reader = new PdfReader(izvor))
            using (var os = new FileStream(izlaz, FileMode.Create, FileAccess.Write))
            {
                var signer = new PdfSigner(reader, os, new StampingProperties().UseAppendMode());
                var doc = signer.GetDocument();
                int brojStr = doc.GetNumberOfPages();

                var props = new SignerProperties()
                    .SetFieldName(SlobodnoIme(doc))
                    .SetClaimedSignDate(sada);
                if (!string.IsNullOrWhiteSpace(op.Razlog)) props.SetReason(op.Razlog);
                if (!string.IsNullOrWhiteSpace(op.Lokacija)) props.SetLocation(op.Lokacija);

                if (op.Vidljiv)
                {
                    int stranica = op.Mjesto switch
                    {
                        MjestoPecata.PrvaStranica => 1,
                        _ => brojStr   // zadnja, ili nova stranica (koja je sada zadnja)
                    };
                    var p = novaStranica ? op.PecatNovaStranica : op.Pecat;
                    var stranicaRect = doc.GetPage(stranica).GetPageSize();
                    float x = Math.Clamp(p.X, 0, Math.Max(0, stranicaRect.GetWidth() - p.Sirina));
                    float y = Math.Clamp(p.Y, 0, Math.Max(0, stranicaRect.GetHeight() - p.Visina));
                    var rect = new Rectangle(stranicaRect.GetLeft() + x, stranicaRect.GetBottom() + y, p.Sirina, p.Visina);

                    props.SetPageNumber(stranica).SetPageRect(rect);
                    var izgled = new SignatureFieldAppearance(SignerProperties.IGNORED_ID)
                        .SetContent(IzgledPecata.Sadrzaj(imePotpisnika, sada, p.Sirina, p.Visina));
                    izgled.SetBorder(Border.NO_BORDER);
                    props.SetSignatureAppearance(izgled);
                }

                signer.SetSignerProperties(props);
                ITSAClient tsa = string.IsNullOrWhiteSpace(op.TsaUrl) ? null : new TsaKlijent(op.TsaUrl.Trim());
                IOcspClient ocsp = tsa != null && op.UgradiOpoziv ? new OcspClientBouncyCastle() : null;
                signer.SignDetached(new BouncyCastleDigest(), potpis, lanac, null, ocsp, tsa, 0,
                    PdfSigner.CryptoStandard.CMS);
            }
        }
        finally
        {
            if (privremeni != null) try { File.Delete(privremeni); } catch { }
        }
    }

    public static bool ImaPotpis(string putanja)
    {
        using var reader = new PdfReader(putanja);
        reader.SetUnethicalReading(true);
        using var doc = new PdfDocument(reader);
        return new SignatureUtil(doc).GetSignatureNames().Count > 0;
    }

    static string SlobodnoIme(PdfDocument doc)
    {
        var postojeca = new HashSet<string>(new SignatureUtil(doc).GetSignatureNames(), StringComparer.OrdinalIgnoreCase);
        var forma = iText.Forms.PdfAcroForm.GetAcroForm(doc, false);
        if (forma != null) foreach (var k in forma.GetAllFormFields().Keys) postojeca.Add(k);
        for (int i = 1; ; i++)
        {
            string ime = "Signature" + i;
            if (!postojeca.Contains(ime)) return ime;
        }
    }

    /// <summary>Doda praznu stranicu (iste veličine kao zadnja) s naslovom za elektronski potpis.</summary>
    public static void DodajStranicuZaPotpis(string ulaz, string izlaz)
    {
        using var reader = new PdfReader(ulaz);
        using var writer = new PdfWriter(izlaz);
        using var doc = new PdfDocument(reader, writer, new StampingProperties().UseAppendMode());
        var zadnja = doc.GetPage(doc.GetNumberOfPages()).GetPageSize();
        var str = doc.AddNewPage(new PageSize(zadnja));
        var font = FontPecata.Napravi();
        var canvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(str);
        float lijevo = zadnja.GetLeft() + 60, vrh = zadnja.GetTop() - 80;
        canvas.BeginText().SetFontAndSize(font, 14).MoveText(lijevo, vrh)
              .ShowText("Elektronski potpis dokumenta").EndText();
        canvas.BeginText().SetFontAndSize(font, 9).MoveText(lijevo, vrh - 22)
              .ShowText(System.IO.Path.GetFileName(ulaz)).EndText();
        canvas.SetLineWidth(0.5f).MoveTo(lijevo, vrh - 32).LineTo(zadnja.GetRight() - 60, vrh - 32).Stroke();
        canvas.Release();
    }
}

/// <summary>Izgled pečata kao u Adobe Acrobatu: ime krupno lijevo, opis desno.</summary>
public static class IzgledPecata
{
    public static string OpisTekst(string ime, DateTime d)
    {
        var tz = d.ToString("zzz").Replace(":", "'") + "'";   // +01:00 -> +01'00'
        return $"Digitally signed by\n{ime}\nDate: {d:yyyy.MM.dd}\n{d:HH:mm:ss} {tz}";
    }

    public static Div Sadrzaj(string ime, DateTime d, float w, float h)
    {
        var font = FontPecata.Napravi();
        float lijevaSirina = w * 0.46f, desnaSirina = w - lijevaSirina;

        // ime u dva reda (ime / prezime), krupno, koliko stane
        var dijelovi = ime.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var redoviImena = dijelovi.Length == 2 ? dijelovi : new[] { ime };
        float velicinaImena = Uklopi(font, redoviImena, lijevaSirina - 3, h - 6, 1.08f);

        var opis = OpisTekst(ime, d).Split('\n');
        float velicinaOpisa = Uklopi(font, opis, desnaSirina - 3, h - 8, 1.18f);

        var tabela = new Table(UnitValue.CreatePointArray(new[] { lijevaSirina, desnaSirina }))
            .SetWidth(w).SetBorder(Border.NO_BORDER).SetMargin(0).SetPadding(0);

        var pImena = new Paragraph(string.Join("\n", redoviImena))
            .SetFont(font).SetFontSize(velicinaImena).SetFixedLeading(velicinaImena * 1.05f)
            .SetMargin(0).SetPadding(0);
        var pOpisa = new Paragraph(string.Join("\n", opis))
            .SetFont(font).SetFontSize(velicinaOpisa).SetFixedLeading(velicinaOpisa * 1.15f)
            .SetMargin(0).SetPadding(0);

        tabela.AddCell(new Cell().Add(pImena).SetBorder(Border.NO_BORDER).SetPadding(0)
            .SetVerticalAlignment(VerticalAlignment.MIDDLE));
        tabela.AddCell(new Cell().Add(pOpisa).SetBorder(Border.NO_BORDER).SetPadding(0).SetPaddingLeft(1)
            .SetVerticalAlignment(VerticalAlignment.MIDDLE));

        var div = new Div().SetMargin(0).SetPadding(0).SetWidth(w);
        div.Add(tabela);
        return div;
    }

    /// <summary>Najveća veličina fonta pri kojoj svi redovi staju u zadanu širinu i visinu.</summary>
    static float Uklopi(PdfFont font, string[] redovi, float sirina, float visina, float prored)
    {
        float poVisini = visina / (redovi.Length * prored);
        float poSirini = float.MaxValue;
        foreach (var r in redovi)
        {
            float w1 = font.GetWidth(r, 1f);
            if (w1 > 0) poSirini = Math.Min(poSirini, sirina / w1);
        }
        return Math.Max(4f, Math.Min(poVisini, poSirini) * 0.98f);
    }
}
