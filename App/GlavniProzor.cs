using System.Security.Cryptography.X509Certificates;
using MasovniPdfPotpis.Core;
using static MasovniPdfPotpis.Core.Jezik;

namespace MasovniPdfPotpis;

public sealed class GlavniProzor : Form
{
    readonly Postavke _post = Postavke.Ucitaj();
    readonly List<InfoDokumenta> _dokumenti = new();
    List<X509Certificate2> _certifikati = new();
    CancellationTokenSource _otkazi;

    // kontrole
    readonly TextBox txtFolder = new() { ReadOnly = true, Width = 520 };
    readonly Button btnFolder = new() { Text = T("Izaberi folder…", "Choose folder…"), AutoSize = true };
    readonly Button btnOsvjezi = new() { Text = T("Osvježi", "Refresh"), AutoSize = true };
    readonly CheckBox chkPodfolderi = new() { Text = T("S podfolderima", "Include subfolders"), AutoSize = true, Margin = new Padding(12, 6, 3, 3) };

    readonly DataGridView grid = new();
    readonly TextBox txtDetalji = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Consolas", 9f) };

    readonly Pregled pregled = new() { Dock = DockStyle.Fill };
    readonly Button btnPrethodna = new() { Text = "<", Width = 36 };
    readonly Button btnSljedeca = new() { Text = ">", Width = 36 };
    readonly Label lblStranica = new() { AutoSize = true, Margin = new Padding(6, 8, 6, 3) };
    readonly ComboBox cmbMjesto = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
    readonly Button btnZadano = new() { Text = T("Zadana pozicija", "Default position"), AutoSize = true };
    readonly Label lblPomoc = new() { AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(6, 8, 3, 3),
        Text = T("Klik ili povlačenje mišem na stranici = mjesto pečata (pamti se)", "Click or drag on the page = stamp position (remembered)") };

    readonly ComboBox cmbCert = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 520 };
    readonly ComboBox cmbSpremanje = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    readonly Button btnNepotpisani = new() { Text = T("Označi nepotpisane", "Select unsigned"), AutoSize = true };
    readonly Button btnPotpisi = new() { Text = T("Potpiši označene", "Sign selected"), AutoSize = true, Font = new Font(Control.DefaultFont, FontStyle.Bold) };
    readonly Button btnStop = new() { Text = T("Zaustavi", "Stop"), AutoSize = true, Enabled = false };
    readonly ProgressBar progres = new() { Width = 260, Height = 20 };
    readonly Label lblStatus = new() { AutoSize = true, Margin = new Padding(6, 8, 3, 3) };
    readonly CheckBox chkTsa = new() { Text = T("Vremenski žig:", "Time-stamp:"), AutoSize = true, Margin = new Padding(12, 6, 0, 3) };
    readonly TextBox txtTsa = new() { Width = 210, Margin = new Padding(0, 4, 3, 3) };
    readonly ToolTip savjet = new() { AutoPopDelay = 20000 };
    readonly ComboBox cmbJezik = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140, Margin = new Padding(3, 4, 3, 3) };
    readonly LinkLabel lnkOApp = new() { Text = T("O aplikaciji", "About"), AutoSize = true, Margin = new Padding(12, 8, 3, 3) };
    public const string IzvorniKodUrl = "https://github.com/BukiSP84/masovni-pdf-potpis";

    string _pregledPutanja;       // fajl koji se prikazuje
    string _pregledTmp;           // privremeni fajl za "nova stranica"
    int _pregledStranica;         // 1-based
    List<(float Sirina, float Visina)> _velicine = new();

    public GlavniProzor()
    {
        Text = "Masovni PDF Potpis " + Application.ProductVersion.Split('+')[0] + T(" — digitalno potpisivanje PDF-ova", " — bulk digital signing of PDFs");
        Width = 1400; Height = 860;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        // --- gornja traka
        var gore = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6), WrapContents = false };
        gore.Controls.AddRange(new Control[] { new Label { Text = T("Folder:", "Folder:"), AutoSize = true, Margin = new Padding(3, 8, 3, 3) },
            txtFolder, btnFolder, btnOsvjezi, chkPodfolderi,
            new Label { Text = T("Jezik:", "Language:"), AutoSize = true, Margin = new Padding(24, 8, 3, 3) }, cmbJezik });

        // --- tabela
        PripremiTabelu();
        var lijevo = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        lijevo.Panel1.Controls.Add(grid);
        var grpDetalji = new GroupBox { Text = T("Potpisi u izabranom dokumentu", "Signatures in the selected document"), Dock = DockStyle.Fill };
        grpDetalji.Controls.Add(txtDetalji);
        lijevo.Panel2.Controls.Add(grpDetalji);

        // --- pregled
        var traka = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        traka.Controls.AddRange(new Control[] { btnPrethodna, lblStranica, btnSljedeca,
            new Label { Text = T("Pečat na:", "Stamp on:"), AutoSize = true, Margin = new Padding(12, 8, 3, 3) }, cmbMjesto, btnZadano, lblPomoc });
        var desno = new Panel { Dock = DockStyle.Fill };
        desno.Controls.Add(pregled);
        desno.Controls.Add(traka);

        var glavni = new SplitContainer { Dock = DockStyle.Fill };
        glavni.Panel1.Controls.Add(lijevo);
        glavni.Panel2.Controls.Add(desno);

        // --- donja traka
        var dole = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(6), WrapContents = true };
        dole.Controls.AddRange(new Control[] {
            new Label { Text = T("Certifikat:", "Certificate:"), AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, cmbCert,
            new Label { Text = T("Spremanje:", "Saving:"), AutoSize = true, Margin = new Padding(12, 8, 3, 3) }, cmbSpremanje,
            chkTsa, txtTsa, btnNepotpisani, btnPotpisi, btnStop, progres, lblStatus, lnkOApp });
        savjet.SetToolTip(chkTsa, T("Dodaje vremenski žig (RFC 3161) i podatak o statusu certifikata (OCSP).\n" +
            "Dokazuje vrijeme potpisa nezavisno od sata računara i produžava provjerljivost potpisa.\n" +
            "Traži internet; šalje se samo otisak (hash) dokumenta, ne sam dokument.",
            "Adds an RFC 3161 time-stamp and certificate status (OCSP).\n" +
            "Proves the signing time independently of the computer clock and extends long-term verifiability.\n" +
            "Requires internet; only a hash is sent, never the document itself."));
        savjet.SetToolTip(txtTsa, T("Adresa servisa za vremenski žig. Zadano: ", "Time-stamp server address. Default: ") + Postavke.ZadaniTsa +
            T(" (besplatan, javni).\nAko vaš izdavač certifikata nudi kvalifikovani vremenski žig, upišite njegovu adresu.",
              " (free, public).\nIf your certificate issuer offers a qualified time-stamp service, enter its address."));

        Controls.Add(glavni);
        Controls.Add(dole);
        Controls.Add(gore);

        cmbMjesto.Items.AddRange(new object[] { T("Zadnja stranica", "Last page"), T("Prva stranica", "First page"), T("Nova stranica na kraju", "New page at the end") });
        cmbSpremanje.Items.AddRange(new object[] { T("Zamijeni original (backup u _backup)", "Replace original (backup in _backup)"), T("Kopija u podfolder _potpisano", "Copy to subfolder _potpisano") });
        cmbMjesto.SelectedIndex = (int)_post.Mjesto;
        cmbSpremanje.SelectedIndex = (int)_post.Spremanje;
        chkPodfolderi.Checked = _post.Rekurzivno;
        cmbJezik.Items.AddRange(new object[] { T("Automatski", "Automatic"), "Bosanski / Hrvatski / Srpski", "English" });
        cmbJezik.SelectedIndex = _post.Jezik switch { "bs" => 1, "en" => 2, _ => 0 };
        cmbJezik.DropDownWidth = 200;
        cmbJezik.SelectedIndexChanged += (_, _) =>
        {
            string novi = cmbJezik.SelectedIndex switch { 1 => "bs", 2 => "en", _ => "" };
            if (novi == _post.Jezik) return;
            _post.Jezik = novi; _post.Sacuvaj();
            MessageBox.Show(this, "Jezik će se promijeniti kad ponovo pokreneš aplikaciju.\nThe language will change when you restart the app.",
                "Masovni PDF Potpis", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        chkTsa.Checked = _post.VremenskiZig;
        txtTsa.Text = string.IsNullOrWhiteSpace(_post.TsaUrl) ? Postavke.ZadaniTsa : _post.TsaUrl;
        txtTsa.Enabled = chkTsa.Checked;
        chkTsa.CheckedChanged += (_, _) => { _post.VremenskiZig = chkTsa.Checked; txtTsa.Enabled = chkTsa.Checked; _post.Sacuvaj(); };
        txtTsa.Leave += (_, _) => { _post.TsaUrl = txtTsa.Text.Trim(); _post.Sacuvaj(); };

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
            lblStatus.Text = T("Pozicija pečata zapamćena", "Stamp position saved") + $": x={p.X:0}, y={p.Y:0}, {p.Sirina:0}×{p.Visina:0} pt";
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
        Kol("fajl", T("Fajl", "File"), 300, true);
        Kol("status", "Status", 150);
        Kol("pecat", T("Pečat", "Stamp"), 50);
        Kol("potpisnik", T("Potpisnik", "Signer"), 120);
        Kol("datum", T("Datum potpisa", "Signed on"), 115);
        Kol("cert", T("Certifikat", "Certificate"), 100);
    }

    void PopuniRed(int i)
    {
        var d = _dokumenti[i];
        var red = grid.Rows[i];
        var zadnji = d.Potpisi.LastOrDefault();
        red.Cells["fajl"].Value = Path.GetRelativePath(txtFolder.Text, d.Putanja);
        red.Cells["status"].Value = d.StatusTekst;
        red.Cells["pecat"].Value = d.Potpisi.Count == 0 ? "" : d.ImaPecat ? T("Da", "Yes") : T("Ne", "No");
        red.Cells["potpisnik"].Value = zadnji?.Potpisnik ?? "";
        red.Cells["datum"].Value = zadnji?.Datum?.ToString("dd.MM.yyyy HH:mm") ?? "";
        red.Cells["cert"].Value = zadnji == null ? "" : zadnji.ZaPotpisivanje ? T("potpisni", "signing") : T("NIJE potpisni", "NOT signing");

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
        btnPotpisi.Text = T("Potpiši označene", "Sign selected") + $" ({n})";
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
        using var dlg = new FolderBrowserDialog { Description = T("Izaberi folder s PDF dokumentima", "Choose a folder with PDF documents"), UseDescriptionForTitle = true };
        if (Directory.Exists(_post.ZadnjiFolder)) dlg.InitialDirectory = _post.ZadnjiFolder;
        if (dlg.ShowDialog(this) == DialogResult.OK) _ = UcitajFolder(dlg.SelectedPath);
    }

    async Task UcitajFolder(string folder)
    {
        txtFolder.Text = folder;
        _post.ZadnjiFolder = folder; _post.Sacuvaj();
        grid.Rows.Clear(); _dokumenti.Clear();
        txtDetalji.Clear(); pregled.Ocisti(T("Učitavam…", "Loading…"));
        btnOsvjezi.Enabled = btnFolder.Enabled = false;
        try
        {
            var fajlovi = await Task.Run(() => Obrada.NadjiPdf(folder, chkPodfolderi.Checked));
            progres.Maximum = Math.Max(1, fajlovi.Count); progres.Value = 0;
            lblStatus.Text = T($"Provjeravam {fajlovi.Count} PDF-ova…", $"Checking {fajlovi.Count} PDFs…");
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
            lblStatus.Text = T($"{_dokumenti.Count} PDF-ova: {nep} nepotpisanih, {_dokumenti.Count - nep} potpisanih ({bezPecata} bez pečata)",
                               $"{_dokumenti.Count} PDFs: {nep} unsigned, {_dokumenti.Count - nep} signed ({bezPecata} without stamp)") +
                             (problem > 0 ? T($", {problem} s problemom", $", {problem} with problems") : "");
            if (_dokumenti.Count == 0) pregled.Ocisti(T("U ovom folderu nema PDF-ova.", "There are no PDFs in this folder."));
            else { grid.ClearSelection(); grid.Rows[0].Selected = true; PrikaziIzabrani(); }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, T("Greška", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        sb.AppendLine(Path.GetFileName(d.Putanja) + $"   ({d.BrojStranica} {T("str.", "p.")})");
        sb.AppendLine("Status: " + d.StatusTekst);
        if (d.Status == StatusDokumenta.Greska) sb.AppendLine(T("Greška: ", "Error: ") + d.Greska);
        if (d.Status == StatusDokumenta.PotpisanMijenjanPoslije)
            sb.AppendLine(T("PAŽNJA: dokument je mijenjan nakon zadnjeg potpisa.", "WARNING: the document was modified after the last signature."));
        int n = 1;
        foreach (var p in d.Potpisi)
        {
            sb.AppendLine();
            sb.AppendLine($"{T("Potpis", "Signature")} {n++}  ({p.NazivPolja})");
            sb.AppendLine(T("  Potpisao:     ", "  Signed by:    ") + p.Potpisnik);
            sb.AppendLine(T("  Izdavač:      ", "  Issuer:       ") + p.Izdavac);
            sb.AppendLine(T("  Datum:        ", "  Date:         ") + $"{p.Datum:dd.MM.yyyy HH:mm:ss}");
            sb.AppendLine(T("  Ispravan:     ", "  Valid:        ") + (p.Ispravan ? T("DA — sadržaj nije mijenjan od potpisa", "YES — content unchanged since signing") : T("NE — ", "NO — ") + p.Greska));
            sb.AppendLine(T("  Certifikat:   ", "  Certificate:  ") + (p.ZaPotpisivanje ? T("potpisni (NonRepudiation)", "signing (NonRepudiation)") : T("NIJE potpisni: ", "NOT signing: ") + p.NamjenaKljuca));
            sb.AppendLine("  Thumbprint:   " + p.Thumbprint);
            if (p.CertVaziDo != null) sb.AppendLine(T("  Važi do:      ", "  Valid until:  ") + $"{p.CertVaziDo:dd.MM.yyyy}");
            sb.AppendLine(T("  Pečat:        ", "  Stamp:        ") + (p.Vidljiv ? T("vidljiv, stranica ", "visible, page ") + p.Stranica : T("nevidljiv potpis", "invisible signature")));
            sb.AppendLine(T("  Vrem. žig:    ", "  Time-stamp:   ") + (p.VremenskiZig == null ? T("nema (vrijeme je sa računara potpisnika)", "none (time from the signer's computer)") : $"{p.VremenskiZig:dd.MM.yyyy HH:mm:ss}" + (p.VremenskiZigIspravan ? T(" — ispravan", " — valid") : T(" — NEISPRAVAN", " — INVALID"))));
            if (p.ImaOcsp) sb.AppendLine(T("  Status cert.: ugrađen OCSP odgovor", "  Cert status:  OCSP response embedded"));
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
            pregled.Ocisti(T("Ne mogu prikazati: ", "Cannot display: ") + ex.Message);
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
            lblPomoc.Text = ciljna ? T("Klik ili povlačenje mišem na stranici = mjesto pečata (pamti se)", "Click or drag on the page = stamp position (remembered)")
                                   : T("Pečat ide na stranicu ", "The stamp goes on page ") + CiljnaStranica(d);
        }
        catch (Exception ex)
        {
            pregled.Ocisti(T("Ne mogu prikazati stranicu: ", "Cannot display page: ") + ex.Message);
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
            cmbCert.Items.Add(T("Nema certifikata za potpis — je li token/kartica utaknut(a) i instaliran softver izdavača?", "No signing certificate — is the token/card plugged in and the issuer's software installed?"));
            cmbCert.SelectedIndex = 0;
            return;
        }
        int idx = _certifikati.FindIndex(c => c.Thumbprint.Equals(_post.CertifikatThumbprint, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) idx = 0;
        cmbCert.SelectedIndex = idx;
    }

    X509Certificate2 IzabraniCert => cmbCert.SelectedIndex >= 0 && cmbCert.SelectedIndex < _certifikati.Count ? _certifikati[cmbCert.SelectedIndex] : null;

    string ImeIzCertifikata() => IzabraniCert?.GetNameInfo(X509NameType.SimpleName, false) ?? T("Ime Prezime", "First Last");

    void PromjenaCertifikata()
    {
        var c = IzabraniCert;
        if (c == null) return;
        _post.CertifikatThumbprint = c.Thumbprint; _post.Sacuvaj();
        if (!Certifikati.JeZaPotpis(c))
            lblStatus.Text = T("Upozorenje: izabrani certifikat nije potpisni (nema NonRepudiation).", "Warning: the selected certificate is not a signing certificate (no NonRepudiation).");
        pregled.ImePecat = ImeIzCertifikata(); pregled.Invalidate();
    }

    void OAplikaciji()
    {
        string tekst = $"Masovni PDF Potpis {Application.ProductVersion.Split('+')[0]}\n" +
            T("Masovno digitalno potpisivanje PDF dokumenata kvalifikovanim certifikatom.\n\n",
              "Bulk digital signing of PDF documents with a qualified certificate.\n\n") +
            T("Licenca: GNU AGPL v3.0 — slobodan softver, bez ikakve garancije.\n",
              "License: GNU AGPL v3.0 — free software, without any warranty.\n") +
            T("Koristi: ", "Uses: ") + "iText Core (AGPL), BouncyCastle (MIT), PDFtoImage/PDFium (MIT/BSD), SkiaSharp (MIT).\n\n" +
            T("Izvorni kod: ", "Source code: ") + IzvorniKodUrl + "\n\n" +
            T("Pravna napomena: aplikacija pravi elektronski potpis fizičkog lica (vlasnika certifikata), " +
              "ne elektronski pečat firme. Potpisuj samo dokumente koje si pregledao/la. Nije namijenjena " +
              "aktima koji traže notarsku/sudsku formu ili vlastoručni potpis. Da li primalac prihvata " +
              "e-potpisan dokument, provjeri kod primaoca. Potpisane dokumente arhiviraj u elektronskom obliku.\n\n",
              "Legal notice: the app creates an electronic signature of a natural person (the certificate holder), " +
              "not a company e-seal. Only sign documents you have reviewed. Not intended for acts that require " +
              "a notarial/court form or a handwritten signature. Check with the recipient whether they accept " +
              "e-signed documents. Archive signed documents in electronic form.\n\n") +
            T("Font pečata: ", "Stamp font: ") + (string.IsNullOrEmpty(FontPecata.IzabraniFont) ? T("(još nije korišten)", "(not used yet)") : FontPecata.IzabraniFont) + "\n" +
            T("Postavke: ", "Settings: ") + "%APPDATA%\\MasovniPdfPotpis\\postavke.json\n\n" +
            T("Otvoriti stranicu s izvornim kodom?", "Open the source code page?");
        if (MessageBox.Show(this, tekst, T("O aplikaciji", "About"), MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(IzvorniKodUrl) { UseShellExecute = true }); } catch { }
    }

    // ------------------------------------------------------------------ potpisivanje
    async Task PotpisiOznacene()
    {
        var cert = IzabraniCert;
        if (cert == null) { MessageBox.Show(this, T("Nije izabran certifikat.", "No certificate selected."), "Masovni PDF Potpis"); return; }
        var indeksi = grid.Rows.Cast<DataGridViewRow>().Where(r => r.Cells["c"].Value is true).Select(r => r.Index).ToList();
        if (indeksi.Count == 0) return;

        int vecPotpisanih = indeksi.Count(i => _dokumenti[i].Potpisi.Count > 0);
        string poruka = T($"Potpisati {indeksi.Count} dokument(a) certifikatom:", $"Sign {indeksi.Count} document(s) with certificate:") + $"\n{Certifikati.Opis(cert)}\n\n" +
            T("Pečat: ", "Stamp: ") + cmbMjesto.Text + "\n" + T("Spremanje: ", "Saving: ") + cmbSpremanje.Text;
        if (vecPotpisanih > 0)
            poruka += "\n\n" + T($"{vecPotpisanih} od njih je već potpisano — dobit će još jedan potpis s pečatom (postojeći ostaje).", $"{vecPotpisanih} of them are already signed — they will get an additional signature with a stamp (the existing one stays).");
        if (!Certifikati.JeZaPotpis(cert))
            poruka += "\n\n" + T("PAŽNJA: izabrani certifikat NIJE potpisni certifikat.", "WARNING: the selected certificate is NOT a signing certificate.");
        if (_post.VremenskiZig)
            poruka += "\n" + T("Vremenski žig: ", "Time-stamp: ") + txtTsa.Text.Trim() + T(" (traži internet)", " (requires internet)");
        if (!PotvrdiPotpisivanje(poruka, indeksi.Select(i => _dokumenti[i].Putanja).ToList())) return;

        var op = new OpcijePotpisa
        {
            Mjesto = _post.Mjesto, Pecat = _post.Pecat, PecatNovaStranica = _post.PecatNovaStranica,
            Vidljiv = true, Razlog = _post.Razlog, Lokacija = _post.Lokacija,
            TsaUrl = _post.VremenskiZig ? txtTsa.Text.Trim() : ""
        };
        _post.TsaUrl = txtTsa.Text.Trim(); _post.Sacuvaj();
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
        var rezultati = new List<RezultatFajla>();

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
                    Invoke(() => lblStatus.Text = T("Potpisujem: ", "Signing: ") + Path.GetFileName(d.Putanja));
                    var r = Obrada.PotpisiJedan(d.Putanja, korijen, nacin, backup, potpis, ime, op, lanac);
                    if (r.Uspjeh) ok++; else greske++;
                    rezultati.Add(r);
                    log.Add($"{(r.Uspjeh ? "OK    " : T("GREŠKA", "ERROR"))} {Path.GetRelativePath(korijen, d.Putanja)} — {r.Poruka}");
                    var novi = nacin == NacinSpremanja.ZamijeniOriginal && r.Uspjeh ? Provjera.Analiziraj(d.Putanja) : null;
                    Invoke(() =>
                    {
                        if (novi != null) { _dokumenti[i] = novi; PopuniRed(i); }
                        grid.Rows[i].Cells["c"].Value = !r.Uspjeh;
                        if (!r.Uspjeh) grid.Rows[i].DefaultCellStyle.BackColor = Color.FromArgb(255, 210, 210);
                        progres.Value++;
                    });
                    // ako token odbije (pogrešan PIN, izvađen), ne nastavljaj s ostalima
                    if (!r.Uspjeh && r.Prekini) break;
                }
            }
            catch (Exception ex) { log.Add(T("GREŠKA: ", "ERROR: ") + ex.Message); greske++; }
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
        lblStatus.Text = T($"Gotovo: potpisano {ok}, grešaka {greske}.", $"Done: {ok} signed, {greske} errors.");

        string dnevnik = null;
        try { dnevnik = Dnevnik.Upisi(korijen, cert, rezultati); } catch { }
        string kraj = T($"Potpisano: {ok}\nGrešaka: {greske}", $"Signed: {ok}\nErrors: {greske}");
        if (dnevnik != null) kraj += "\n\n" + T("Dnevnik potpisivanja:", "Signing log:") + "\n" + dnevnik;
        if (nacin == NacinSpremanja.ZamijeniOriginal && ok > 0) kraj += "\n\n" + T("Originali su sačuvani u:", "Originals were saved to:") + "\n" + backup;
        if (nacin == NacinSpremanja.KopijaUPotpisano && ok > 0) kraj += "\n\n" + T("Potpisane kopije su u:", "Signed copies are in:") + "\n" + Path.Combine(korijen, "_potpisano");
        if (greske > 0) kraj += "\n\n" + T("Detalji grešaka:", "Error details:") + "\n" + string.Join("\n", log.Where(l => !l.StartsWith("OK")).Take(15));
        MessageBox.Show(this, kraj, "Masovni PDF Potpis", MessageBoxButtons.OK, greske > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }

    /// <summary>Potvrda s izjavom potpisnika; dugme „Potpiši“ se uključi tek kad se izjava označi.</summary>
    bool PotvrdiPotpisivanje(string poruka, List<string> fajlovi)
    {
        string spisak = string.Join("\n", fajlovi.Take(25).Select(Path.GetFileName));
        if (fajlovi.Count > 25) spisak += "\n… " + T($"i još {fajlovi.Count - 25}", $"and {fajlovi.Count - 25} more");
        try
        {
            var potpisi = new TaskDialogButton(T("Potpiši", "Sign")) { Enabled = false };
            var odustani = TaskDialogButton.Cancel;
            var izjava = new TaskDialogVerificationCheckBox(T("Pregledao/la sam ove dokumente i potpisujem ih svojom voljom.", "I have reviewed these documents and I sign them of my own free will."));
            izjava.CheckedChanged += (_, _) => potpisi.Enabled = izjava.Checked;
            var strana = new TaskDialogPage
            {
                Caption = T("Potvrda potpisivanja", "Confirm signing"),
                Heading = T($"Potpisati {fajlovi.Count} dokument(a)?", $"Sign {fajlovi.Count} document(s)?"),
                Text = poruka,
                Icon = TaskDialogIcon.ShieldBlueBar,
                Verification = izjava,
                Expander = new TaskDialogExpander(spisak) { CollapsedButtonText = T("Prikaži dokumente", "Show documents"), ExpandedButtonText = T("Sakrij dokumente", "Hide documents") },
                Buttons = { potpisi, odustani },
                DefaultButton = odustani,
                AllowCancel = true
            };
            return TaskDialog.ShowDialog(this, strana) == potpisi;
        }
        catch
        {
            // ako TaskDialog nije dostupan
            return MessageBox.Show(this, poruka + "\n\n" + T("Potvrđujem da sam pregledao/la dokumente i potpisujem ih svojom voljom.", "I confirm that I have reviewed the documents and sign them of my own free will."),
                T("Potvrda potpisivanja", "Confirm signing"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK;
        }
    }
}
