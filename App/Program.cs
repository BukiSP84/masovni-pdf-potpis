using MasovniPdfPotpis.Core;

namespace MasovniPdfPotpis;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Jezik.Postavi(Postavke.Ucitaj().Jezik);
        Application.ThreadException += (_, e) =>
            MessageBox.Show(e.Exception.Message, "Masovni PDF Potpis — " + Jezik.T("greška", "error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        Application.Run(new GlavniProzor());
    }
}
