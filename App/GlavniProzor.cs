using System.Security.Cryptography.X509Certificates;
using MasovniPdfPotpis.Core;

namespace MasovniPdfPotpis;

public sealed class GlavniProzor : Form
{
    readonly Postavke _post = Postavke.Ucitaj();
    readonly List<InfoDokumenta> _dokumenti = new();
    List<X509Certificate2> _certifikati = new();
    CancellationTokenSource _otkazi;

    // kontrole
    readonly TextBox txtFolder = new() { ReadOnly = true, Width = 520 };
    readonly Button btnFolder = new() { Text = "Izaberi folder…", AutoSize = true };
    readonly Button btnOsvjezi = new() { Text = "Osvježi", AutoSize = true };
    readonly CheckBox chkPodfolderi = new() { Text = "S podfolderima", AutoSize = true, Margin = new Padding(12, 6, 3, 3) };

    readonly DataGridView grid = new();
    readonly TextBox txtDetalji = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Consolas", 9f) };

    readonly Pregled pregled = new() { Dock = DockStyle.Fill };
    readonly Button btnPrethodna = new() { Text = "<", Width = 36 };
    readonly Button btnSljedeca = new() { Text = ">", Width = 36 };
    readonly Label lblStranica = new() { AutoSize = true, Margin = new Padding(6, 8, 6, 3) };
    readonly ComboBox cmbMjesto = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
    readonly Button btnZadano = new() { Text = "Zadana pozicija", AutoSize = true };
    readonly Label lblPomoc = new() { AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(6, 8, 3, 3),
        Text = "Klik ili povlačenje mišem na stranici = mjesto pečata (pamti se)" };

    readonly ComboBox cmbCert = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 520 };
    readonly ComboBox cmbSpremanje = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    readonly Button btnNepotpisani = new() { Text = "Označi nepotpisane", AutoSize = true };
    readonly Button btnPotpisi = new() { Text = "Potpiši označene", AutoSize = true, Font = new Font(Control.DefaultFont, FontStyle.Bold) };
    readonly Button btnStop = new() { Text = "Zaustavi", AutoSize = true, Enabled = false };
    readonly ProgressBar progres = new() { Width = 260, Height = 20 };
    readonly Label lblStatus = new() { AutoSize = true, Margin = new Padding(6, 8, 3, 3) };
    readonly LinkLabel lnkOApp = new() { Text = "O aplikaciji", AutoSize = true, Margin = new Padding(12, 8, 3, 3) };
    public const string IzvorniKodUrl = "https://github.com/BukiSP84/masovni-pdf-potpis";

    string _pregledPutanja;       // fajl koji se prikazuje
    string _pregledTmp;           // privremeni fajl za "nova stranica"
    int _pregledStranica;         // 1-based
    List<(float Sirina, float Visina)> _velicine = new();

    public GlavniProzor()
    {
        Text = "Masovni PDF Potpis " + Application.ProductVersion.Split('+')[0] + " — digitalno potpisivanje PDF-ova";
        Width = 1400; Height = 860;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        // --- gornja traka
        var gore = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6), WrapContents = false };
        gore.Controls.AddRange(new Control[] { new Label { Text = "Folder:", AutoSize = true, Margin = new Padding(3, 8, 3, 3) },
            txtFolder, btnFolder, btnOsvjezi, chkPodfolderi });

        // --- tabela
        PripremiTabelu();
        var lijevo = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        lijevo.Panel1.Controls.Add(grid);
        var grpDetalji = new GroupBox { Text = "Potpisi u izabranom dokumentu", Dock = DockStyle.Fill };
        grpDetalji.Controls.Add(txtDetalji);
        lijevo.Panel2.Controls.Add(grpDetalji);

        // --- pregled
        var traka = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        traka.Controls.AddRange(new Control[] { btnPrethodna, lblStranica, btnSljedeca,
            new Label { Text = "Pečat na:", AutoSize = true, Margin = new Padding(12, 8, 3, 3) }, cmbMjesto, btnZadano, lblPomoc });
        var desno = new Panel { Dock = DockStyle.Fill };
        desno.Controls.Add(pregled);
        desno.Controls.Add(traka);

        var glavni = new SplitContainer { Dock = DockStyle.Fill };
        glavni.Panel1.Controls.Add(lijevo);
        glavni.Panel2.Controls.Add(desno);

        // --- donja traka
        var dole = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(6), WrapContents = true };
        dole.Controls.AddRange(new Control[] {
            new Label { Text = "Certifikat:", AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, cmbCert,
            new Label { Text = "Spremanje:", AutoSize = true, Margin = new Padding(12, 8, 3, 3) }, cmbSpremanje,
            btnNepotpisani, btnPotpisi, btnStop, progres, lblStatus, lnkOApp });

        Controls.Add(glavni);
        Controls.Add(dole);
        Controls.Add(gore);

        cmbMjesto.Items.AddRange(new object[] { "Zadnja stranica", "Prva stranica", "Nova stranica na kraju" });
        cmbSpremanje.Items.AddRange(new object[] { "Zamijeni original (backup u _backup)", "Kopija u podfolder _potpisano" });
        cmbMjesto.SelectedIndex = (int)_post.Mjesto;
        cmbSpremanje.SelectedIndex = (int)_post.Spremanje;
        chkPodfolderi.Checked = _post.Rekurzivno;

        Load += (_, _) =>
        {
            glavni.SplitterDistance = (int)(ClientSize.Width * 0.56);
            lijevo.SplitterDistance = (int)(lijevo.Height * 0.72);
            UcitajCertifikate();
            if (Directory.Exists(_post.ZadnjiFolder)) _ = UcitajFolder(_post.ZadnjiFolder);
        };
        FormClosing += (_, _) => { _post.Sacuvaj(); ObrisiTmp(); };

        btnFolder.Click += (_, _) => IzaberiFolder();
        btnOsvjezi.Click += (_, _) => { if (Directory.Exists(txtFolder.Text)) _ = UcitajFolder(txtFolder.Text); };
        chkPodfolderi.CheckedChanged += (_, _) => { _post.Rekurzivno = chkPodfolderi.Checked; if (Directory.Exists(txtFolder.Text)) _ = UcitajFolder(txtFolder.Text); };
        grid.SelectionChanged += (_, _) => PrikaziIzabrani();
        btnPrethodna.Click += (_, _) => { if (_pregledStranica > 1) { _pregledStranica--; Iscrtaj(); } };
        btnSljedeca.Click += (_, _) => { if (_pregledStranica < _velicine.Count) { _pregledStranica++; Iscrtaj(); } };
        cmbMjesto.SelectedIndexChanged += (_, _) => { _post.Mjesto = (MjestoPecata)cmbMjesto.SelectedIndex; _post.Sacuvaj(); PrikaziIzabrani(); };
        cmbSpremanje.SelectedIndexChanged += (_, _) => { _post.Spremanje = (NacinSpremanja)cmbSpremanje.SelectedIndex; _post.Sacuvaj(); };
        cmbCert.SelectedIndexChanged += (_, _) => PromjenaCertifikata();
        btnZadano.Click += (_, _) =>
        {
            if (_post.Mjesto == MjestoPecata.NovaStranica) _post.PecatNovaStranica = new Pravougaonik { X = 60, Y = 600 };
            else _post.Pecat = new Pravougaonik();
            _post.Sacuvaj(); Iscrtaj();
        };
        pregled.PecatPromijenjen += p =>
        {
            if (_post.Mjesto == MjestoPecata.NovaStranica) _post.PecatNovaStranica = p; else _post.Pecat = p;
            _post.Sacuvaj();
            lblStatus.Text = $"Pozicija pečata zapamćena: x={p.X:0}, y={p.Y:0}, {p.Sirina:0}×{p.Visina:0} pt";
        };
        btnNepotpisani.Click += (_, _) => OznaciNepotpisane();
        btnPotpisi.Click += async (_, _) => await PotpisiOznacene();
        btnStop.Click += (_, _) => _otkazi?.Cancel();
        lnkOApp.LinkClicked += (_, _) => OAplikaciji();
        grid.CellValueChanged += (_, _) => AzurirajDugme();
        grid.CurrentCellDirtyStateChanged += (_, _) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        AzurirajDugme();
    }

    // ------------------------------------------------------------------ tabela
    void PripremiTabelu()
    {
        grid.Dock = DockStyle.Fill;
        grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.MultiSelect = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        grid.BackgroundColor = SystemColors.Window; grid.BorderStyle = BorderStyle.None;
        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "c", HeaderText = "", Width = 28 });
        void Kol(string ime, string naslov, int w, bool fill = false)
        {
            var k = new DataGridViewTextBoxColumn { Name = ime, HeaderText = naslov, Width = w, ReadOnly = true };
            if (fill) k.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns.Add(k);
        }
        Kol("fajl", "Fajl", 300, true);
        Kol("status", "Status", 150);
        Kol("pecat", "Pečat", 50);
        Kol("potpisnik", "Potpisnik", 120);
        Kol("datum", "Datum potpisa", 115);
        Kol("cert", "Certifikat", 100);
    }

    void PopuniRed(int i)
    {
        var d = _dokumenti[i];
        var red = grid.Rows[i];
        var zadnji = d.Potpisi.LastOrDefault();
        red.Cells["fajl"].Value = Path.GetRelativePath(txtFolder.Text, d.Putanja);
        red.Cells["status"].Value = d.StatusTekst;
        red.Cells["pecat"].Value = d.Potpisi.Count == 0 ? "" : d.ImaPecat ? "Da" : "Ne";
        red.Cells["potpisnik"].Value = zadnji?.Potpisnik ?? "";
        red.Cells["datum"].Value = zadnji?.Datum?.ToString("dd.MM.yyyy HH:mm") ?? "";
        red.Cells["cert"].Value = zadnji == null ? "" : zadnji.ZaPotpisivanje ? "potpisni" : "NIJE potpisni";

        Color boja = d.Status switch
        {
            StatusDokumenta.Nepotpisan => SystemColors.Window,
            StatusDokumenta.Potpisan when zadnji is { ZaPotpisivanje: false } => Color.FromArgb(255, 245, 200),
            StatusDokumenta.Potpisan => Color.FromArgb(220, 245, 220),
            StatusDokumenta.PotpisanMijenjanPoslije => Color.FromArgb(255, 225, 200),
            _ => Color.FromArgb(255, 210, 210)
        };
        red.DefaultCellStyle.BackColor = boja;
    }

    void AzurirajDugme()
    {
        int n = grid.Rows.Cast<DataGridViewRow>().Count(r => r.Cells["c"].Value is true);
        btnPotpisi.Text = $"Potpiši označene ({n})";
        btnPotpisi.Enabled = n > 0 && _otkazi == null;
    }

    void OznaciNepotpisane()
    {
        for (int i = 0; i < _dokumenti.Count; i++)
            grid.Rows[i].Cells["c"].Value = _dokumenti[i].Status == StatusDokumenta.Nepotpisan;
        AzurirajDugme();
    }

    // ------------------------------------------------------------------ folder
    void IzaberiFolder()
    {
        using var dlg = new FolderBrowserDialog { Description = "Izaberi folder s PDF dokumentima", UseDescriptionForTitle = true };
        if (Directory.Exists(_post.ZadnjiFolder)) dlg.InitialDirectory = _post.ZadnjiFolder;
        if (dlg.ShowDialog(this) == DialogResult.OK) _ = UcitajFolder(dlg.SelectedPath);
    }

    async Task UcitajFolder(string folder)
    {
        txtFolder.Text = folder;
        _post.ZadnjiFolder = folder; _post.Sacuvaj();
        grid.Rows.Clear(); _dokumenti.Clear();
        txtDetalji.Clear(); pregled.Ocisti("Učitavam…");
        btnOsvjezi.Enabled = btnFolder.Enabled = false;
        try
        {
            var fajlovi = await Task.Run(() => Obrada.NadjiPdf(folder, chkPodfolderi.Checked));
            progres.Maximum = Math.Max(1, fajlovi.Count); progres.Value = 0;
            lblStatus.Text = $"Provjeravam {fajlovi.Count} PDF-ova…";
            var rezultati = await Task.Run(() =>
            {
                var l = new InfoDokumenta[fajlovi.Count];
                int gotovo = 0;
                Parallel.For(0, fajlovi.Count, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
                {
                    l[i] = Provjera.Analiziraj(fajlovi[i]);
                    int g = Interlocked.Increment(ref gotovo);
                    if (g % 10 == 0) BeginInvoke(() => progres.Value = Math.Min(progres.Maximum, g));
                });
                return l;
            });
            _dokumenti.AddRange(rezultati);
            if (_dokumenti.Count > 0) grid.Rows.Add(_dokumenti.Count);
            for (int i = 0; i < _dokumenti.Count; i++)
            {
                PopuniRed(i);
                grid.Rows[i].Cells["c"].Value = _dokumenti[i].Status == StatusDokumenta.Nepotpisan;
            }
            progres.Value = progres.Maximum;
            int nep = _dokumenti.Count(d => d.Status == StatusDokumenta.Nepotpisan);
            int bezPecata = _dokumenti.Count(d => d.Potpisi.Count > 0 && !d.ImaPecat);
            int problem = _dokumenti.Count(d => d.Status is StatusDokumenta.Neispravan or StatusDokumenta.PotpisanMijenjanPoslije or StatusDokumenta.Greska);
            lblStatus.Text = $"{_dokumenti.Count} PDF-ova: {nep} nepotpisanih, {_dokumenti.Count - nep} potpisanih ({bezPecata} bez pečata)" +
                             (problem > 0 ? $", {problem} s problemom" : "");
            if (_dokumenti.Count == 0) pregled.Ocisti("U ovom folderu nema PDF-ova.");
            else { grid.ClearSelection(); grid.Rows[0].Selected = true; PrikaziIzabrani(); }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Greška", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnOsvjezi.Enabled = btnFolder.Enabled = true;
            AzurirajDugme();
        }
    }

    // ------------------------------------------------------------------ detalji + pregled
    InfoDokumenta Izabrani =>
        grid.SelectedRows.Count == 1 && grid.SelectedRows[0].Index < _dokumenti.Count ? _dokumenti[grid.SelectedRows[0].Index] : null;

    void PrikaziIzabrani()
    {
        var d = Izabrani;
        if (d == null) return;
        txtDetalji.Text = OpisDetalja(d);
        PripremiPregled(d);
    }

    static string OpisDetalja(InfoDokumenta d)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(Path.GetFileName(d.Putanja) + $"   ({d.BrojStranica} str.)");
        sb.AppendLine("Status: " + d.StatusTekst);
        if (d.Status == StatusDokumenta.Greska) sb.AppendLine("Greška: " + d.Greska);
        if (d.Status == StatusDokumenta.PotpisanMijenjanPoslije)
            sb.AppendLine("PAŽNJA: dokument je mijenjan nakon zadnjeg potpisa.");
        int n = 1;
        foreach (var p in d.Potpisi)
        {
            sb.AppendLine();
            sb.AppendLine($"Potpis {n++}  ({p.NazivPolja})");
            sb.AppendLine($"  Potpisao:     {p.Potpisnik}");
            sb.AppendLine($"  Izdavač:      {p.Izdavac}");
            sb.AppendLine($"  Datum:        {p.Datum:dd.MM.yyyy HH:mm:ss}");
            sb.AppendLine($"  Ispravan:     {(p.Ispravan ? "DA — sadržaj nije mijenjan od potpisa" : "NE — " + p.Greska)}");
            sb.AppendLine($"  Certifikat:   {(p.ZaPotpisivanje ? "potpisni (NonRepudiation)" : "NIJE potpisni: " + p.NamjenaKljuca)}");
            sb.AppendLine($"  Thumbprint:   {p.Thumbprint}");
            if (p.CertVaziDo != null) sb.AppendLine($"  Važi do:      {p.CertVaziDo:dd.MM.yyyy}");
            sb.AppendLine($"  Pečat:        {(p.Vidljiv ? "vidljiv, stranica " + p.Stranica : "nevidljiv potpis")}");
        }
        return sb.ToString();
    }

    void PripremiPregled(InfoDokumenta d)
    {
        ObrisiTmp();
        _pregledPutanja = d.Putanja;
        try
        {
            if (_post.Mjesto == MjestoPecata.NovaStranica && d.Potpisi.Count == 0)
            {
                _pregledTmp = Path.Combine(Path.GetTempPath(), "mpp_pregled_" + Guid.NewGuid().ToString("N") + ".pdf");
                Potpisnik.DodajStranicuZaPotpis(d.Putanja, _pregledTmp);
                _pregledPutanja = _pregledTmp;
            }
            _velicine = Stranice.Velicine(_pregledPutanja);
            _pregledStranica = CiljnaStranica(d);
            Iscrtaj();
        }
        catch (Exception ex)
        {
            pregled.Ocisti("Ne mogu prikazati: " + ex.Message);
        }
    }

    int CiljnaStranica(InfoDokumenta d) => _post.Mjesto == MjestoPecata.PrvaStranica ? 1 : _velicine.Count;

    void Iscrtaj()
    {
        if (_pregledPutanja == null || _velicine.Count == 0) return;
        var d = Izabrani;
        lblStranica.Text = $"{_pregledStranica} / {_velicine.Count}";
        var (w, h) = _velicine[_pregledStranica - 1];
        bool ciljna = d != null && _pregledStranica == CiljnaStranica(d);
        bool nova = _post.Mjesto == MjestoPecata.NovaStranica && _pregledPutanja == _pregledTmp;
        var pecat = nova ? _post.PecatNovaStranica : _post.Pecat;
        try
        {
            var bmp = Renderiraj(_pregledPutanja, _pregledStranica - 1, w, h);
            pregled.ImePecat = ImeIzCertifikata();
            pregled.SamoOkvir = d?.ImaPecat == true;
            pregled.Postavi(bmp, w, h, pecat, ciljna);
            lblPomoc.Text = ciljna ? "Klik ili povlačenje mišem na stranici = mjesto pečata (pamti se)"
                                   : "Pečat ide na stranicu " + CiljnaStranica(d);
        }
        catch (Exception ex)
        {
            pregled.Ocisti("Ne mogu prikazati stranicu: " + ex.Message);
        }
    }

    static Bitmap Renderiraj(string putanja, int stranica, float wPt, float hPt)
    {
        int dpi = 110;
        using var fs = File.OpenRead(putanja);
        using var sk = PDFtoImage.Conversion.ToImage(fs, page: (Index)stranica,
            options: new PDFtoImage.RenderOptions(Dpi: dpi, WithAnnotations: true, WithFormFill: true));
        using var data = sk.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        using var ms = new MemoryStream(data.ToArray());
        return new Bitmap(ms);
    }

    void ObrisiTmp()
    {
        if (_pregledTmp != null) { try { File.Delete(_pregledTmp); } catch { } _pregledTmp = null; }
    }

    // ------------------------------------------------------------------ certifikati
    void UcitajCertifikate()
    {
        _certifikati = Certifikati.ZaPotpisivanje();
#if MPP_TEST
        // samo za testiranje: MPP_TEST_PFX=putanja|lozinka
        var testPfx = Environment.GetEnvironmentVariable("MPP_TEST_PFX");
        if (!string.IsNullOrEmpty(testPfx) && testPfx.Contains('|'))
        {
            try { var d = testPfx.Split('|', 2); _certifikati.Insert(0, new X509Certificate2(d[0], d[1], X509KeyStorageFlags.Exportable)); } catch { }
        }
#endif
        cmbCert.Items.Clear();
        foreach (var c in _certifikati) cmbCert.Items.Add(Certifikati.Opis(c));
        if (_certifikati.Count == 0)
        {
            cmbCert.Items.Add("Nema certifikata za potpis — je li token/kartica utaknut(a) i instaliran softver izdavača?");
            cmbCert.SelectedIndex = 0;
            return;
        }
        int idx = _certifikati.FindIndex(c => c.Thumbprint.Equals(_post.CertifikatThumbprint, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) idx = 0;
        cmbCert.SelectedIndex = idx;
    }

    X509Certificate2 IzabraniCert => cmbCert.SelectedIndex >= 0 && cmbCert.SelectedIndex < _certifikati.Count ? _certifikati[cmbCert.SelectedIndex] : null;

    string ImeIzCertifikata() => IzabraniCert?.GetNameInfo(X509NameType.SimpleName, false) ?? "Ime Prezime";

    void PromjenaCertifikata()
    {
        var c = IzabraniCert;
        if (c == null) return;
        _post.CertifikatThumbprint = c.Thumbprint; _post.Sacuvaj();
        if (!Certifikati.JeZaPotpis(c))
            lblStatus.Text = "Upozorenje: izabrani certifikat nije potpisni (nema NonRepudiation).";
        pregled.ImePecat = ImeIzCertifikata(); pregled.Invalidate();
    }

    void OAplikaciji()
    {
        string tekst = $"Masovni PDF Potpis {Application.ProductVersion.Split('+')[0]}\n" +
            "Masovno digitalno potpisivanje PDF dokumenata kvalifikovanim certifikatom.\n\n" +
            "Licenca: GNU AGPL v3.0 — slobodan softver, bez ikakve garancije.\n" +
            "Koristi: iText Core (AGPL), BouncyCastle (MIT), PDFtoImage/PDFium (MIT/BSD), SkiaSharp (MIT).\n\n" +
            "Izvorni kod: " + IzvorniKodUrl + "\n\n" +
            "Font pečata: " + (string.IsNullOrEmpty(FontPecata.IzabraniFont) ? "(još nije korišten)" : FontPecata.IzabraniFont) + "\n" +
            "Postavke: %APPDATA%\\MasovniPdfPotpis\\postavke.json\n\n" +
            "Otvoriti stranicu s izvornim kodom?";
        if (MessageBox.Show(this, tekst, "O aplikaciji", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(IzvorniKodUrl) { UseShellExecute = true }); } catch { }
    }

    // ------------------------------------------------------------------ potpisivanje
    async Task PotpisiOznacene()
    {
        var cert = IzabraniCert;
        if (cert == null) { MessageBox.Show(this, "Nije izabran certifikat.", "Masovni PDF Potpis"); return; }
        var indeksi = grid.Rows.Cast<DataGridViewRow>().Where(r => r.Cells["c"].Value is true).Select(r => r.Index).ToList();
        if (indeksi.Count == 0) return;

        int vecPotpisanih = indeksi.Count(i => _dokumenti[i].Potpisi.Count > 0);
        string poruka = $"Potpisati {indeksi.Count} dokument(a) certifikatom:\n{Certifikati.Opis(cert)}\n\n" +
            $"Pečat: {cmbMjesto.Text}\nSpremanje: {cmbSpremanje.Text}";
        if (vecPotpisanih > 0)
            poruka += $"\n\n{vecPotpisanih} od njih je već potpisano — dobit će još jedan potpis s pečatom (postojeći ostaje).";
        if (!Certifikati.JeZaPotpis(cert))
            poruka += "\n\nPAŽNJA: izabrani certifikat NIJE potpisni certifikat.";
        if (MessageBox.Show(this, poruka, "Potvrda", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;

        var op = new OpcijePotpisa
        {
            Mjesto = _post.Mjesto, Pecat = _post.Pecat, PecatNovaStranica = _post.PecatNovaStranica,
            Vidljiv = true, Razlog = _post.Razlog, Lokacija = _post.Lokacija
        };
        string korijen = txtFolder.Text;
        string backup = Path.Combine(korijen, "_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        var nacin = _post.Spremanje;
        string ime = ImeIzCertifikata();

        _otkazi = new CancellationTokenSource();
        var token = _otkazi.Token;
        btnPotpisi.Enabled = btnFolder.Enabled = btnOsvjezi.Enabled = btnNepotpisani.Enabled = false;
        btnStop.Enabled = true;
        progres.Maximum = indeksi.Count; progres.Value = 0;
        int ok = 0, greske = 0;
        var log = new List<string>();

        // potpisivanje u STA niti (driver tokena prikazuje prozor za PIN)
        var gotovo = new TaskCompletionSource();
        var nit = new Thread(() =>
        {
            try
            {
                using var potpis = new PotpisCertifikatom(cert);
                var lanac = potpis.Lanac();
                foreach (var i in indeksi)
                {
                    if (token.IsCancellationRequested) break;
                    var d = _dokumenti[i];
                    Invoke(() => lblStatus.Text = "Potpisujem: " + Path.GetFileName(d.Putanja));
                    var r = Obrada.PotpisiJedan(d.Putanja, korijen, nacin, backup, potpis, ime, op, lanac);
                    if (r.Uspjeh) ok++; else greske++;
                    log.Add($"{(r.Uspjeh ? "OK    " : "GREŠKA")} {Path.GetRelativePath(korijen, d.Putanja)} — {r.Poruka}");
                    var novi = nacin == NacinSpremanja.ZamijeniOriginal && r.Uspjeh ? Provjera.Analiziraj(d.Putanja) : null;
                    Invoke(() =>
                    {
                        if (novi != null) { _dokumenti[i] = novi; PopuniRed(i); }
                        grid.Rows[i].Cells["c"].Value = !r.Uspjeh;
                        if (!r.Uspjeh) grid.Rows[i].DefaultCellStyle.BackColor = Color.FromArgb(255, 210, 210);
                        progres.Value++;
                    });
                    // ako token odbije (pogrešan PIN, izvađen), ne nastavljaj s ostalima
                    if (!r.Uspjeh && r.Poruka.StartsWith("certifikat/token")) break;
                }
            }
            catch (Exception ex) { log.Add("GREŠKA: " + ex.Message); greske++; }
            finally { gotovo.SetResult(); }
        });
        nit.SetApartmentState(ApartmentState.STA);
        nit.IsBackground = true;
        nit.Start();
        await gotovo.Task;

        _otkazi = null;
        btnFolder.Enabled = btnOsvjezi.Enabled = btnNepotpisani.Enabled = true;
        btnStop.Enabled = false;
        AzurirajDugme();
        PrikaziIzabrani();
        lblStatus.Text = $"Gotovo: potpisano {ok}, grešaka {greske}.";

        string logPutanja = Path.Combine(Path.GetTempPath(), "MasovniPdfPotpis_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
        try { File.WriteAllLines(logPutanja, log); } catch { }
        string kraj = $"Potpisano: {ok}\nGrešaka: {greske}";
        if (nacin == NacinSpremanja.ZamijeniOriginal && ok > 0) kraj += $"\n\nOriginali su sačuvani u:\n{backup}";
        if (nacin == NacinSpremanja.KopijaUPotpisano && ok > 0) kraj += $"\n\nPotpisane kopije su u:\n{Path.Combine(korijen, "_potpisano")}";
        if (greske > 0) kraj += "\n\nDetalji grešaka:\n" + string.Join("\n", log.Where(l => !l.StartsWith("OK")).Take(15));
        MessageBox.Show(this, kraj, "Masovni PDF Potpis", MessageBoxButtons.OK, greske > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }
}
