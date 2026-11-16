using System.Drawing.Drawing2D;
using MasovniPdfPotpis.Core;

namespace MasovniPdfPotpis;

/// <summary>
/// Prikaz stranice PDF-a s pravougaonikom pečata. Klik = pečat zadane veličine na tom mjestu,
/// povlačenje = pečat proizvoljne veličine. Sve se preračunava u PDF tačke.
/// </summary>
public sealed class Pregled : Control
{
    Bitmap _slika;
    float _sirinaPt = 595, _visinaPt = 842;
    Pravougaonik _pecat;
    bool _prikaziPecat;
    Point? _pocetak;
    Rectangle _povlacenje;
    public string ImePecat { get; set; } = "Ime Prezime";
    public string Poruka { get; set; } = "Izaberi PDF iz liste";
    /// <summary>Samo okvir (bez teksta) — npr. kad dokument već ima pečat.</summary>
    public bool SamoOkvir { get; set; }

    /// <summary>Pozvano kad korisnik promijeni položaj pečata.</summary>
    public event Action<Pravougaonik> PecatPromijenjen;

    public Pregled()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(230, 230, 230);
        Cursor = Cursors.Cross;
    }

    public void Postavi(Bitmap slika, float sirinaPt, float visinaPt, Pravougaonik pecat, bool prikaziPecat)
    {
        _slika?.Dispose();
        _slika = slika;
        _sirinaPt = sirinaPt; _visinaPt = visinaPt;
        _pecat = pecat; _prikaziPecat = prikaziPecat;
        Invalidate();
    }

    public void Ocisti(string poruka)
    {
        _slika?.Dispose(); _slika = null; Poruka = poruka; Invalidate();
    }

    RectangleF PodrucjeStranice()
    {
        if (_slika == null) return RectangleF.Empty;
        float s = Math.Min((Width - 20f) / _sirinaPt, (Height - 20f) / _visinaPt);
        float w = _sirinaPt * s, h = _visinaPt * s;
        return new RectangleF((Width - w) / 2, (Height - h) / 2, w, h);
    }

    float Skala => _slika == null ? 1 : PodrucjeStranice().Width / _sirinaPt;

    RectangleF UEkran(Pravougaonik p)
    {
        var st = PodrucjeStranice(); float s = Skala;
        return new RectangleF(st.X + p.X * s, st.Y + (_visinaPt - p.Y - p.Visina) * s, p.Sirina * s, p.Visina * s);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        if (_slika == null)
        {
            TextRenderer.DrawText(g, Poruka, Font, ClientRectangle, Color.DimGray,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }
        var st = PodrucjeStranice();
        g.FillRectangle(Brushes.Gray, st.X + 3, st.Y + 3, st.Width, st.Height);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(_slika, st);

        if (_prikaziPecat && _pecat != null)
        {
            var r = UEkran(_pecat);
            CrtajPecat(g, r);
        }
        if (_pocetak != null && _povlacenje.Width > 0)
        {
            using var pen = new Pen(Color.RoyalBlue, 1) { DashStyle = DashStyle.Dash };
            g.DrawRectangle(pen, _povlacenje);
        }
    }

    void CrtajPecat(Graphics g, RectangleF r)
    {
        using (var b = new SolidBrush(Color.FromArgb(40, 30, 144, 255))) g.FillRectangle(b, r);
        using (var pen = new Pen(Color.RoyalBlue, 1.5f)) g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
        if (SamoOkvir) return;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        // približan izgled pečata (stvarni crta iText pri potpisivanju)
        var dijelovi = ImePecat.Split(' ', 2);
        float lijevo = r.Width * 0.46f;
        var nf = new StringFormat(StringFormatFlags.NoWrap | StringFormatFlags.NoClip);
        float velIme = Math.Max(4, r.Height / 2.5f);
        using (var probni = new Font("Segoe UI", velIme, GraphicsUnit.Pixel))
        {
            float najsira = dijelovi.Max(d => g.MeasureString(d, probni, PointF.Empty, nf).Width);
            if (najsira > lijevo - 2) velIme *= (lijevo - 2) / najsira;
        }
        using (var f = new Font("Segoe UI", velIme, GraphicsUnit.Pixel))
            for (int i = 0; i < dijelovi.Length; i++)
                g.DrawString(dijelovi[i], f, Brushes.Black, r.X + 1, r.Y + r.Height * 0.5f - velIme * 1.15f * dijelovi.Length / 2 + velIme * 1.15f * i, nf);
        var opis = IzgledPecata.OpisTekst(ImePecat, DateTime.Now).Split('\n');
        float velOpis = Math.Max(3, r.Height / 5.4f);
        using (var probni = new Font("Segoe UI", velOpis, GraphicsUnit.Pixel))
        {
            float najsira = opis.Max(d => g.MeasureString(d, probni, PointF.Empty, nf).Width);
            float dostupno = r.Width - lijevo - 2;
            if (najsira > dostupno) velOpis *= dostupno / najsira;
        }
        using (var f = new Font("Segoe UI", velOpis, GraphicsUnit.Pixel))
            for (int i = 0; i < opis.Length; i++)
                g.DrawString(opis[i], f, Brushes.DimGray, r.X + lijevo, r.Y + r.Height * 0.5f - velOpis * 1.2f * opis.Length / 2 + velOpis * 1.2f * i, nf);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (_slika == null || !_prikaziPecat || e.Button != MouseButtons.Left) return;
        _pocetak = e.Location; _povlacenje = Rectangle.Empty;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_pocetak == null) return;
        var p = _pocetak.Value;
        _povlacenje = Rectangle.FromLTRB(Math.Min(p.X, e.X), Math.Min(p.Y, e.Y), Math.Max(p.X, e.X), Math.Max(p.Y, e.Y));
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_pocetak == null) return;
        var st = PodrucjeStranice(); float s = Skala;
        var p0 = _pocetak.Value;
        _pocetak = null;
        Pravougaonik novi;
        if (_povlacenje.Width < 8 || _povlacenje.Height < 8)
        {
            // klik: pečat zadržava veličinu, centriran na mjestu klika
            float w = _pecat?.Sirina ?? 181, h = _pecat?.Visina ?? 49;
            float cx = (p0.X - st.X) / s, cy = _visinaPt - (p0.Y - st.Y) / s;
            novi = new Pravougaonik { X = cx - w / 2, Y = cy - h / 2, Sirina = w, Visina = h };
        }
        else
        {
            float x = (_povlacenje.Left - st.X) / s;
            float y = _visinaPt - (_povlacenje.Bottom - st.Y) / s;
            float w = Math.Max(60, _povlacenje.Width / s), h = Math.Max(18, _povlacenje.Height / s);
            novi = new Pravougaonik { X = x, Y = y, Sirina = w, Visina = h };
        }
        novi.X = Math.Clamp(novi.X, 0, Math.Max(0, _sirinaPt - novi.Sirina));
        novi.Y = Math.Clamp(novi.Y, 0, Math.Max(0, _visinaPt - novi.Visina));
        novi.X = MathF.Round(novi.X, 1); novi.Y = MathF.Round(novi.Y, 1);
        novi.Sirina = MathF.Round(novi.Sirina, 1); novi.Visina = MathF.Round(novi.Visina, 1);
        _pecat = novi;
        _povlacenje = Rectangle.Empty;
        Invalidate();
        PecatPromijenjen?.Invoke(novi);
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
}
