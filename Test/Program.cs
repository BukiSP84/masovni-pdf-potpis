using System.Security.Cryptography.X509Certificates;
using MasovniPdfPotpis.Core;

// upotreba: Test <pfx> <lozinka> <izlazniFolder> <fajl1.pdf> ...
var cert = new X509Certificate2(args[0], args[1], X509KeyStorageFlags.Exportable);
string izlaz = args[2]; Directory.CreateDirectory(izlaz);
using var potpis = new PotpisCertifikatom(cert);
var lanac = potpis.Lanac();
string ime = cert.GetNameInfo(X509NameType.SimpleName, false);
// MPP_TSA=local -> lokalni test TSA; MPP_TSA=<url> -> stvarni; MPP_TSA=bad -> nedostupan URL
string tsaEnv = Environment.GetEnvironmentVariable("MPP_TSA") ?? "";
using var lokalni = tsaEnv == "local" ? new LokalniTsa(8099) : null;
string tsaUrl = tsaEnv == "local" ? lokalni!.Url : tsaEnv == "bad" ? "http://127.0.0.1:1/" : tsaEnv;
var svi = new List<RezultatFajla>();
foreach (var mjesto in new[] { MjestoPecata.ZadnjaStranica, MjestoPecata.NovaStranica })
foreach (var f in args.Skip(3))
{
    string cilj = Path.Combine(izlaz, mjesto + "_" + Path.GetFileName(f));
    File.Copy(f, cilj, true);
    var r = Obrada.PotpisiJedan(cilj, izlaz, NacinSpremanja.ZamijeniOriginal, Path.Combine(izlaz, "_backup"),
        potpis, ime, new OpcijePotpisa { Mjesto = mjesto, TsaUrl = tsaUrl }, lanac);
    svi.Add(r);
    var info = Provjera.Analiziraj(cilj);
    Console.WriteLine($"{Path.GetFileName(cilj)}: {(r.Uspjeh ? "OK" : "GRESKA")} {r.Poruka} | {info.StatusTekst}, str={info.BrojStranica}, pecat={info.ImaPecat}");
    foreach (var p in info.Potpisi)
        Console.WriteLine($"    {p.NazivPolja}: {p.Potpisnik} / {p.Izdavac} {p.Datum} ispravan={p.Ispravan} cijeli={p.PokrivaCijeliDokument} vidljiv={p.Vidljiv} str={p.Stranica} potpisni={p.ZaPotpisivanje} {p.Greska} tsa={p.VremenskiZig:HH:mm:ss}/{p.VremenskiZigIspravan} ocsp={p.ImaOcsp}");
}
Console.WriteLine("Font: " + FontPecata.IzabraniFont);
Console.WriteLine("Dnevnik: " + Dnevnik.Upisi(izlaz, cert, svi));
