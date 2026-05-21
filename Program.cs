using System.Runtime.InteropServices;
using System.Text;

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
        EnsureShortcut();
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

    private static void EnsureShortcut()
    {
        var lnkPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Start Menu", "Programs", "FileManager.lnk");

        if (File.Exists(lnkPath)) return;

        var exePath = Environment.ProcessPath;
        if (exePath == null) return;

        var shl = (IShellLinkW)new CShellLink();
        shl.SetPath(exePath);
        shl.SetWorkingDirectory(Path.GetDirectoryName(exePath));
        shl.SetDescription("文件管理器");

        var persistFile = (IPersistFile)shl;
        persistFile.Save(lnkPath, false);

        var propStore = (IPropertyStore)shl;
        var appId = "FileManager.Local.1";
        PropVariant pv = new() { vt = 31, pwstr = Marshal.StringToCoTaskMemUni(appId) };
        var propKey = new PropertyKey(new Guid("{9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}"), 5);
        propStore.SetValue(ref propKey, ref pv);
        propStore.Commit();

        Marshal.FreeCoTaskMem(pv.pwstr);
        Marshal.ReleaseComObject(shl);
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class CShellLink;

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    private interface IPropertyStore
    {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PropertyKey pkey);
        void GetValue(ref PropertyKey key, out PropVariant pv);
        void SetValue(ref PropertyKey key, ref PropVariant pv);
        void Commit();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid fmtid;
        public int pid;
        public PropertyKey(Guid guid, int id) { fmtid = guid; pid = id; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr pwstr;
    }
}
