using System.Security.Cryptography.X509Certificates;
using iText.Forms;
using iText.Kernel.Pdf;
using iText.Signatures;

namespace MasovniPdfPotpis.Core;

/// <summary>Čita i provjerava digitalne potpise u PDF-u.</summary>
public static class Provjera
{
    public static InfoDokumenta Analiziraj(string putanja)
    {
        var info = new InfoDokumenta { Putanja = putanja };
        try
        {
            using var reader = new PdfReader(putanja);
            reader.SetUnethicalReading(true);
            using var doc = new PdfDocument(reader);
            info.BrojStranica = doc.GetNumberOfPages();

            var util = new SignatureUtil(doc);
            var imena = util.GetSignatureNames();
            if (imena.Count == 0)
            {
                info.Status = StatusDokumenta.Nepotpisan;
                return info;
            }

            var forma = PdfAcroForm.GetAcroForm(doc, false);
            foreach (var ime in imena)
                info.Potpisi.Add(CitajPotpis(doc, util, forma, ime));

            if (info.Potpisi.Any(p => !p.Ispravan))
                info.Status = StatusDokumenta.Neispravan;
            else if (!info.Potpisi.Any(p => p.PokrivaCijeliDokument))
                info.Status = StatusDokumenta.PotpisanMijenjanPoslije;
            else
                info.Status = StatusDokumenta.Potpisan;
        }
        catch (Exception ex)
        {
            info.Status = StatusDokumenta.Greska;
            info.Greska = ex.Message;
        }
        return info;
    }

    static InfoPotpisa CitajPotpis(PdfDocument doc, SignatureUtil util, PdfAcroForm forma, string ime)
    {
        var p = new InfoPotpisa { NazivPolja = ime };
        try
        {
            p.PokrivaCijeliDokument = util.SignatureCoversWholeDocument(ime);
            PdfPKCS7 pkcs7 = util.ReadSignatureData(ime);
            p.Ispravan = pkcs7.VerifySignatureIntegrityAndAuthenticity();
            var datum = pkcs7.GetSignDate();
            if (datum != DateTime.MinValue && datum.Year > 1900) p.Datum = datum.ToLocalTime();

            var cert = new X509Certificate2(pkcs7.GetSigningCertificate().GetEncoded());
            p.Potpisnik = cert.GetNameInfo(X509NameType.SimpleName, false);
            p.Izdavac = cert.GetNameInfo(X509NameType.SimpleName, true);
            p.Thumbprint = cert.Thumbprint;
            p.CertVaziDo = cert.NotAfter;
            var ku = cert.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault();
            if (ku != null)
            {
                p.NamjenaKljuca = ku.KeyUsages.ToString();
                p.ZaPotpisivanje = ku.KeyUsages.HasFlag(X509KeyUsageFlags.NonRepudiation);
            }
        }
        catch (Exception ex)
        {
            p.Ispravan = false;
            p.Greska = ex.Message;
        }

        try
        {
            var polje = forma?.GetField(ime);
            var widget = polje?.GetWidgets()?.FirstOrDefault();
            if (widget != null)
            {
                var r = widget.GetRectangle()?.ToRectangle();
                p.Vidljiv = r != null && r.GetWidth() > 1 && r.GetHeight() > 1 && (widget.GetFlags() & iText.Kernel.Pdf.Annot.PdfAnnotation.HIDDEN) == 0;
                var stranica = widget.GetPage();
                if (stranica != null) p.Stranica = doc.GetPageNumber(stranica);
            }
        }
        catch { /* vidljivost nije kritična */ }
        return p;
    }
}
