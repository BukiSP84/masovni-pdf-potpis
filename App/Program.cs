namespace MasovniPdfPotpis;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.ThreadException += (_, e) =>
            MessageBox.Show(e.Exception.Message, "Masovni PDF Potpis — greška", MessageBoxButtons.OK, MessageBoxIcon.Error);
        Application.Run(new GlavniProzor());
    }
}
