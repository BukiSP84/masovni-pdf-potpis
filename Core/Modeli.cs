using System.Text.Json;

namespace MasovniPdfPotpis.Core;

/// <summary>Gdje ide pečat.</summary>
public enum MjestoPecata
{
    ZadnjaStranica = 0,
    PrvaStranica = 1,
    NovaStranica = 2
}

/// <summary>Kako se sprema rezultat.</summary>
public enum NacinSpremanja
{
    ZamijeniOriginal = 0,   // original se prvo kopira u _backup_<datum>
    KopijaUPotpisano = 1    // originali ostaju, potpisane kopije idu u _potpisano
}

/// <summary>Pravougaonik pečata u PDF tačkama, od donjeg lijevog ugla stranice.</summary>
public sealed class Pravougaonik
{
    public float X { get; set; } = 40;
    public float Y { get; set; } = 55;
    public float Sirina { get; set; } = 181;   // ista veličina kao Acrobat pečat
    public float Visina { get; set; } = 49;
}

public sealed class Postavke
{
    public string ZadnjiFolder { get; set; } = "";
    public string CertifikatThumbprint { get; set; } = "";
    public MjestoPecata Mjesto { get; set; } = MjestoPecata.ZadnjaStranica;
    public NacinSpremanja Spremanje { get; set; } = NacinSpremanja.ZamijeniOriginal;
    public Pravougaonik Pecat { get; set; } = new();
    /// <summary>Pozicija pečata na novoj (dodanoj) stranici.</summary>
    public Pravougaonik PecatNovaStranica { get; set; } = new() { X = 60, Y = 600, Sirina = 181, Visina = 49 };
    public bool Rekurzivno { get; set; } = true;
    public string Razlog { get; set; } = "";
    public string Lokacija { get; set; } = "";

    static string Putanja =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MasovniPdfPotpis", "postavke.json");

    // ranija verzija (pod drugim imenom) — postavke se jednom prenesu
    static string StaraPutanja =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SaviPotpis", "postavke.json");

    static readonly JsonSerializerOptions Opcije = new() { WriteIndented = true };

    public static Postavke Ucitaj()
    {
        try
        {
            if (!File.Exists(Putanja) && File.Exists(StaraPutanja))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Putanja)!);
                File.Copy(StaraPutanja, Putanja);
            }
            if (File.Exists(Putanja))
                return JsonSerializer.Deserialize<Postavke>(File.ReadAllText(Putanja)) ?? new Postavke();
        }
        catch { /* pokvarene postavke -> zadane */ }
        return new Postavke();
    }

    public void Sacuvaj()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Putanja)!);
            File.WriteAllText(Putanja, JsonSerializer.Serialize(this, Opcije));
        }
        catch { /* nije kritično */ }
    }
}

/// <summary>Podaci o jednom potpisu u PDF-u.</summary>
public sealed class InfoPotpisa
{
    public string NazivPolja { get; set; } = "";
    public string Potpisnik { get; set; } = "";
    public string Izdavac { get; set; } = "";
    public DateTime? Datum { get; set; }
    public string Thumbprint { get; set; } = "";
    public string NamjenaKljuca { get; set; } = "";
    public bool ZaPotpisivanje { get; set; }          // NonRepudiation / ContentCommitment
    public bool Ispravan { get; set; }                // kriptografski ispravan, sadržaj nije mijenjan
    public bool PokrivaCijeliDokument { get; set; }   // nakon ovog potpisa nije bilo izmjena
    public bool Vidljiv { get; set; }
    public int Stranica { get; set; }
    public string Greska { get; set; } = "";
    public DateTime? CertVaziDo { get; set; }
}

public enum StatusDokumenta
{
    Nepotpisan,
    Potpisan,
    PotpisanMijenjanPoslije,   // potpis ok, ali dokument je mijenjan nakon zadnjeg potpisa
    Neispravan,                // potpis ne odgovara sadržaju
    Greska                     // ne može se pročitati
}

public sealed class InfoDokumenta
{
    public string Putanja { get; set; } = "";
    public int BrojStranica { get; set; }
    public StatusDokumenta Status { get; set; }
    public List<InfoPotpisa> Potpisi { get; set; } = new();
    public string Greska { get; set; } = "";
    public bool ImaPecat => Potpisi.Any(p => p.Vidljiv);

    public string StatusTekst => Status switch
    {
        StatusDokumenta.Nepotpisan => "Nepotpisan",
        StatusDokumenta.Potpisan => "Potpisan",
        StatusDokumenta.PotpisanMijenjanPoslije => "Potpisan, mijenjan poslije",
        StatusDokumenta.Neispravan => "NEISPRAVAN potpis",
        _ => "Greška pri čitanju"
    };
}
