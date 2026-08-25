namespace Hunbjter
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            if (!SingleInstanceGuard.TryAcquire())
            {
                // Another copy is already running (possibly hidden in the tray). Ask it to
                // surface itself instead of starting a second WebView2 session, a second
                // monitor roster and a second set of ffmpeg recordings against the same
                // favorites.json.
                SingleInstanceGuard.WakeRunningInstance();
                return;
            }

            try
            {
                // Must run before any window class is realized, otherwise the common controls
                // (notably the DataGridView scroll bars) keep the light system theme.
                NativeTheme.EnableDarkMode();

                // To customize application configuration such as set high DPI settings or default font,
                // see https://aka.ms/applicationconfiguration.
                ApplicationConfiguration.Initialize();
                Application.Run(new Form1());
            }
            finally
            {
                SingleInstanceGuard.Release();
            }
        }
    }
}