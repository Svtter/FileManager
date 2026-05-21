using System.Runtime.InteropServices;

namespace FileManager;

static class Program
{
    [DllImport("shell32.dll")]
    private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

    [STAThread]
    static void Main()
    {
        SetCurrentProcessExplicitAppUserModelID("FileManager.Local.1");
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) =>
        {
            MessageBox.Show($"未处理的异常:\n{e.Exception}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            MessageBox.Show($"致命错误:\n{e.ExceptionObject}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        Application.Run(new MainForm());
    }
}
