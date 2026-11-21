using RetroBar.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace RetroBar
{
    internal sealed class Program
    {
        private const string MutexName = "RetroBar";
        private const int MutexAttempts = 10;
        private const int MutexWaitMs = 1000;

        private static System.Threading.Mutex _retroBarMutex;

        /// <summary>Main entry point. No Settings, WPF or ManagedShell objects
        /// may be created until evaluation mode and the singleton are secured.</summary>
        [STAThread]
        public static int Main(string[] args)
        {
            bool marked = File.Exists(Path.Combine(AppContext.BaseDirectory, EvaluationSession.PackageMarker));
            if (!EvaluationSession.TryResolveArguments(args, marked, out bool isolated, out string error))
            {
                MessageBox.Show(error, "RetroBar launch refused", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 2;
            }

            EvaluationSession.Configure(isolated);

            try
            {
                if (isolated)
                {
                    // Fail immediately, never wait for an installed copy to exit
                    // and unexpectedly take over Explorer in mid-workflow.
                    if (!GetMutex())
                    {
                        _retroBarMutex.Dispose();
                        _retroBarMutex = null;
                        MessageBox.Show("Another RetroBar is already running. Close it before using " +
                            "the isolated evaluator. No taskbars or settings have been changed.",
                            "RetroBar evaluator blocked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return 3;
                    }

                    if (AnotherRetroBarProcessExists())
                    {
                        MessageBox.Show("A separate RetroBar process is already running. The evaluator " +
                            "will not replace its taskbar. Close the other process before testing.",
                            "RetroBar evaluator blocked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return 3;
                    }
                }
                else if (!SingleInstanceCheck())
                {
                    return 1;
                }

                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                App app = new App();
                app.InitializeComponent();
                return app.Run();
            }
            catch (Exception ex) when (isolated && (ex is UnauthorizedAccessException ||
                ex is System.ComponentModel.Win32Exception))
            {
                MessageBox.Show("Could not securely check whether another RetroBar is running: " +
                    ex.Message + ". Evaluation launch refused.", "RetroBar evaluator blocked",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 3;
            }
            finally
            {
                if (_retroBarMutex != null)
                {
                    try { _retroBarMutex.ReleaseMutex(); }
                    catch (System.ApplicationException) { /* Not owned by this thread. */ }
                    _retroBarMutex.Dispose();
                    _retroBarMutex = null;
                }
            }
        }

        private static bool AnotherRetroBarProcessExists()
        {
            int self = Environment.ProcessId;
            foreach (string name in new[] { "RetroBar", "RetroBar-Extreme", "RetroBarPortable" })
            {
                foreach (Process process in Process.GetProcessesByName(name))
                {
                    using (process)
                    {
                        if (process.Id != self) return true;
                    }
                }
            }
            return false;
        }

        private static bool GetMutex()
        {
            _retroBarMutex = new System.Threading.Mutex(true, MutexName, out bool createdNew);
            return createdNew;
        }

        private static bool SingleInstanceCheck()
        {
            for (int i = 0; i < MutexAttempts; i++)
            {
                if (!GetMutex())
                {
                    _retroBarMutex.Dispose();
                    _retroBarMutex = null;
                    System.Threading.Thread.Sleep(MutexWaitMs);
                }
                else
                {
                    return true;
                }
            }
            return false;
        }
    }
}
