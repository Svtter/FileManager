using System.Runtime.InteropServices;

namespace FileManager;

public partial class MainForm : Form
{
    private readonly ImageList _fileIcons;
    private readonly List<string> _navigationHistory = new();
    private int _historyIndex = -1;
    private string? _currentPath;

    private TextBox _addressBar = null!;
    private TreeView _treeView = null!;
    private ListView _fileListView = null!;
    private SplitContainer _mainSplitter = null!;
    private SplitContainer _previewSplitter = null!;
    private Panel _previewPanel = null!;
    private PictureBox _pictureBox = null!;
    private Microsoft.Web.WebView2.WinForms.WebView2 _webView = null!;
    private Label _statusLabel = null!;
    private Label _previewPlaceholder = null!;
    private ToolStripButton _backButton = null!;
    private ToolStripButton _forwardButton = null!;
    private ToolStripButton _upButton = null!;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".ico", ".tiff", ".tif", ".webp"
    };

    private static readonly HashSet<string> PdfExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf"
    };

    public MainForm()
    {
        _fileIcons = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
        InitializeComponents();
        LoadDrives();
        NavigateTo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    private void InitializeComponents()
    {
        Text = "文件管理器";
        Size = new Size(1200, 800);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(800, 600);
        KeyPreview = true;

        var toolbar = new ToolStrip
        {
            Dock = DockStyle.Top,
            Padding = new Padding(2),
            GripStyle = ToolStripGripStyle.Hidden
        };

        _backButton = new ToolStripButton("← 后退") { Enabled = false };
        _backButton.Click += (_, _) => GoBack();
        _forwardButton = new ToolStripButton("前进 →") { Enabled = false };
        _forwardButton.Click += (_, _) => GoForward();
        _upButton = new ToolStripButton("↑ 上级");
        _upButton.Click += (_, _) => GoUp();
        var goButton = new ToolStripButton("前往");
        goButton.Click += (_, _) => NavigateTo(_addressBar.Text);

        _addressBar = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10F) };
        _addressBar.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) NavigateTo(_addressBar.Text);
        };

        var addressItem = new ToolStripControlHost(_addressBar) { AutoSize = false };

        toolbar.Items.AddRange(new ToolStripItem[]
        {
            _backButton, _forwardButton, _upButton,
            new ToolStripSeparator(),
            addressItem,
            goButton
        });
        toolbar.Resize += (_, _) =>
        {
            int totalButtonsWidth = 0;
            foreach (ToolStripItem item in toolbar.Items)
            {
                if (item != addressItem)
                    totalButtonsWidth += item.Width;
            }
            addressItem.Width = toolbar.Width - totalButtonsWidth - 30;
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4),
            BackColor = SystemColors.Control,
            BorderStyle = BorderStyle.Fixed3D
        };

        _pictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.White,
            Visible = false
        };

        _webView = new Microsoft.Web.WebView2.WinForms.WebView2
        {
            Dock = DockStyle.Fill,
            Visible = false
        };

        _previewPlaceholder = new Label
        {
            Dock = DockStyle.Fill,
            Text = "选择图片或 PDF 文件以预览",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 12F),
            ForeColor = SystemColors.GrayText,
            BackColor = Color.White
        };

        _previewPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        _previewPanel.Controls.AddRange(new Control[] { _webView, _pictureBox, _previewPlaceholder });

        _fileListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = false
        };
        _fileListView.Columns.Add("名称", 300);
        _fileListView.Columns.Add("大小", 100);
        _fileListView.Columns.Add("类型", 150);
        _fileListView.Columns.Add("修改日期", 180);
        _fileListView.SmallImageList = _fileIcons;
        _fileListView.SelectedIndexChanged += FileListView_SelectedIndexChanged;
        _fileListView.DoubleClick += FileListView_DoubleClick;

        _treeView = new TreeView
        {
            Dock = DockStyle.Fill,
            ShowRootLines = true,
            ShowPlusMinus = true,
            HideSelection = false,
            ImageList = _fileIcons
        };
        _treeView.AfterSelect += TreeView_AfterSelect;
        _treeView.BeforeExpand += TreeView_BeforeExpand;

        var treePanel = new Panel { Dock = DockStyle.Fill };
        treePanel.Controls.Add(_treeView);

        _previewSplitter = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 400,
            Panel1MinSize = 200,
            Panel2MinSize = 150,
            FixedPanel = FixedPanel.Panel2,
            BackColor = SystemColors.Control
        };
        _previewSplitter.Panel1.Controls.Add(_fileListView);
        _previewSplitter.Panel2.Controls.Add(_previewPanel);

        _mainSplitter = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 250,
            Panel1MinSize = 150,
            FixedPanel = FixedPanel.Panel1,
            BackColor = SystemColors.Control
        };
        _mainSplitter.Panel1.Controls.Add(treePanel);
        _mainSplitter.Panel2.Controls.Add(_previewSplitter);

        Controls.AddRange(new Control[] { _mainSplitter, toolbar, _statusLabel });

        AddSystemIcons();
    }

    private void AddSystemIcons()
    {
        foreach (var ext in new[] { ".txt", ".pdf", ".jpg", ".png", ".bmp", ".gif", ".doc", ".docx", ".xls", ".xlsx", ".mp3", ".mp4", ".zip", ".exe", ".dll", ".cs", ".html" })
        {
            _fileIcons.Images.Add(ext, GetFileIcon(ext));
        }

        _fileIcons.Images.Add("folder", SystemIcons.GetStockIcon(StockIconId.Folder));
        _fileIcons.Images.Add("folder_open", SystemIcons.GetStockIcon(StockIconId.FolderOpen));
        _fileIcons.Images.Add("drive", SystemIcons.GetStockIcon(StockIconId.DriveFixed));
        _fileIcons.Images.Add("cdrom", SystemIcons.GetStockIcon(StockIconId.DriveCD));
        _fileIcons.Images.Add("removable", SystemIcons.GetStockIcon(StockIconId.DriveRemovable));
    }

    private static Icon GetFileIcon(string extension)
    {
        try
        {
            var tmp = Path.GetTempFileName();
            var filePath = tmp + extension;
            try
            {
                File.Move(tmp, filePath);
                File.WriteAllText(filePath, "");
                var shFileInfo = new SHFILEINFO();
                SHGetFileInfo(filePath, 0, ref shFileInfo, (uint)Marshal.SizeOf(shFileInfo),
                    SHGFI_ICON | SHGFI_SMALLICON);
                if (shFileInfo.hIcon != IntPtr.Zero)
                {
                    var icon = Icon.FromHandle(shFileInfo.hIcon);
                    var result = (Icon)icon.Clone();
                    DestroyIcon(shFileInfo.hIcon);
                    return result;
                }
            }
            finally
            {
                try { File.Delete(filePath); } catch { }
            }
        }
        catch { }

        return SystemIcons.GetStockIcon(StockIconId.Application);
    }

    private void LoadDrives()
    {
        _treeView.BeginUpdate();
        _treeView.Nodes.Clear();

        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            var imageKey = drive.DriveType switch
            {
                DriveType.CDRom => "cdrom",
                DriveType.Removable => "removable",
                _ => "drive"
            };
            var idx = _fileIcons.Images.IndexOfKey(imageKey);
            var node = new TreeNode($"{drive.Name} {drive.VolumeLabel}".Trim(), idx, idx)
            {
                Tag = drive.RootDirectory.FullName
            };
            node.Nodes.Add(new TreeNode("...") { Tag = "placeholder" });
            _treeView.Nodes.Add(node);
        }

        _treeView.EndUpdate();
    }

    private void TreeView_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        if (e.Node == null) return;
        var node = e.Node;
        if (node.Nodes.Count == 1 && node.Nodes[0].Tag as string == "placeholder")
        {
            node.Nodes.Clear();
            try
            {
                var path = node.Tag as string;
                if (path == null) return;

                var folderIdx = _fileIcons.Images.IndexOfKey("folder");
                var folderOpenIdx = _fileIcons.Images.IndexOfKey("folder_open");

                foreach (var dir in Directory.GetDirectories(path))
                {
                    var dirInfo = new DirectoryInfo(dir);
                    var child = new TreeNode(dirInfo.Name, folderIdx, folderOpenIdx) { Tag = dir };
                    child.Nodes.Add(new TreeNode("...") { Tag = "placeholder" });
                    node.Nodes.Add(child);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch { }
        }
    }

    private void TreeView_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag is string path && path != "placeholder")
        {
            NavigateTo(path, fromTree: true);
        }
    }

    private void NavigateTo(string path, bool fromTree = false)
    {
        try
        {
            path = Path.GetFullPath(path);
            if (!Directory.Exists(path)) return;

            if (!fromTree && _historyIndex >= 0 && _historyIndex < _navigationHistory.Count && _navigationHistory[_historyIndex] == path)
                return;

            if (!fromTree)
            {
                if (_historyIndex < _navigationHistory.Count - 1)
                    _navigationHistory.RemoveRange(_historyIndex + 1, _navigationHistory.Count - _historyIndex - 1);
                _navigationHistory.Add(path);
                _historyIndex = _navigationHistory.Count - 1;
            }

            _currentPath = path;
            _addressBar.Text = path;
            UpdateNavigationButtons();
            LoadFiles(path);
            SelectTreeNode(path);
            ClearPreview();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法访问路径: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadFiles(string path)
    {
        _fileListView.BeginUpdate();
        _fileListView.Items.Clear();

        try
        {
            var dirInfo = new DirectoryInfo(path);
            var folderIdx = _fileIcons.Images.IndexOfKey("folder");

            foreach (var dir in dirInfo.GetDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var item = new ListViewItem(dir.Name, folderIdx) { Tag = dir.FullName };
                    item.SubItems.Add("");
                    item.SubItems.Add("文件夹");
                    item.SubItems.Add(dir.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    _fileListView.Items.Add(item);
                }
                catch { }
            }

            foreach (var file in dirInfo.GetFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var ext = file.Extension.ToLowerInvariant();
                    var imageKey = _fileIcons.Images.ContainsKey(ext) ? ext : ".txt";
                    var imgIdx = _fileIcons.Images.IndexOfKey(imageKey);
                    var item = new ListViewItem(file.Name, imgIdx) { Tag = file.FullName };
                    item.SubItems.Add(FormatFileSize(file.Length));
                    item.SubItems.Add(GetFileTypeDescription(ext));
                    item.SubItems.Add(file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    _fileListView.Items.Add(item);
                }
                catch { }
            }
        }
        catch (UnauthorizedAccessException) { }
        catch { }

        _fileListView.EndUpdate();
    }

    private void FileListView_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_fileListView.SelectedItems.Count == 0)
        {
            ClearPreview();
            return;
        }

        var selected = _fileListView.SelectedItems[0];
        var filePath = selected.Tag as string;
        if (filePath == null) return;

        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        if (ImageExtensions.Contains(ext))
        {
            PreviewImage(filePath);
        }
        else if (PdfExtensions.Contains(ext))
        {
            PreviewPdf(filePath);
        }
        else if (Directory.Exists(filePath))
        {
            ClearPreview();
            _previewPlaceholder.Text = "双击文件夹以打开";
        }
        else
        {
            ClearPreview();
            _previewPlaceholder.Text = $"不支持预览此文件类型 ({ext})";
        }

        UpdateStatus();
    }

    private void FileListView_DoubleClick(object? sender, EventArgs e)
    {
        if (_fileListView.SelectedItems.Count == 0) return;

        var selected = _fileListView.SelectedItems[0];
        var path = selected.Tag as string;
        if (path == null) return;

        if (Directory.Exists(path))
        {
            NavigateTo(path);
        }
        else if (File.Exists(path))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void PreviewImage(string filePath)
    {
        try
        {
            _pictureBox.Image?.Dispose();
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            _pictureBox.Image = Image.FromStream(fs);
            _pictureBox.Visible = true;
            _webView.Visible = false;
            _previewPlaceholder.Visible = false;
        }
        catch (Exception ex)
        {
            _pictureBox.Visible = false;
            _previewPlaceholder.Visible = true;
            _previewPlaceholder.Text = $"无法预览图片: {ex.Message}";
        }
    }

    private async void PreviewPdf(string filePath)
    {
        try
        {
            _pictureBox.Visible = false;
            _previewPlaceholder.Visible = false;
            _webView.Visible = true;

            if (_webView.CoreWebView2 == null)
            {
                await _webView.EnsureCoreWebView2Async(null);
            }

            var uri = new Uri(filePath);
            _webView.CoreWebView2.Navigate(uri.AbsoluteUri);
        }
        catch (Exception ex)
        {
            _webView.Visible = false;
            _previewPlaceholder.Visible = true;
            _previewPlaceholder.Text = $"无法预览 PDF: {ex.Message}";
        }
    }

    private void ClearPreview()
    {
        _pictureBox.Image?.Dispose();
        _pictureBox.Image = null;
        _pictureBox.Visible = false;
        _webView.Visible = false;
        _previewPlaceholder.Visible = true;
        _previewPlaceholder.Text = "选择图片或 PDF 文件以预览";
    }

    private void GoBack()
    {
        if (_historyIndex > 0)
        {
            _historyIndex--;
            NavigateTo(_navigationHistory[_historyIndex], fromTree: true);
        }
    }

    private void GoForward()
    {
        if (_historyIndex < _navigationHistory.Count - 1)
        {
            _historyIndex++;
            NavigateTo(_navigationHistory[_historyIndex], fromTree: true);
        }
    }

    private void GoUp()
    {
        if (_currentPath == null) return;
        try
        {
            var parent = Path.GetDirectoryName(_currentPath);
            if (parent != null && Directory.Exists(parent))
                NavigateTo(parent);
        }
        catch { }
    }

    private void UpdateNavigationButtons()
    {
        _backButton.Enabled = _historyIndex > 0;
        _forwardButton.Enabled = _historyIndex < _navigationHistory.Count - 1;
        _upButton.Enabled = _currentPath != null && Path.GetDirectoryName(_currentPath) != null;
    }

    private void SelectTreeNode(string path)
    {
        _treeView.BeginUpdate();
        try
        {
            var node = FindTreeNode(_treeView.Nodes, path);
            if (node != null)
            {
                _treeView.SelectedNode = node;
                node.Expand();
            }
        }
        finally
        {
            _treeView.EndUpdate();
        }
    }

    private static TreeNode? FindTreeNode(TreeNodeCollection nodes, string path)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Tag as string == path)
                return node;

            var found = FindTreeNode(node.Nodes, path);
            if (found != null) return found;
        }
        return null;
    }

    private void UpdateStatus()
    {
        int fileCount = 0, dirCount = 0;
        foreach (ListViewItem item in _fileListView.Items)
        {
            var tag = item.Tag as string;
            if (tag != null)
            {
                if (File.Exists(tag)) fileCount++;
                else if (Directory.Exists(tag)) dirCount++;
            }
        }

        var statusText = $"{_currentPath}  |  {dirCount} 个文件夹, {fileCount} 个文件";

        if (_fileListView.SelectedItems.Count > 0)
        {
            var selected = _fileListView.SelectedItems[0];
            statusText += $"  |  已选择: {selected.Text}";
        }

        _statusLabel.Text = statusText;
    }

    private static string FormatFileSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }
        return $"{size:0.##} {units[unitIndex]}";
    }

    private static string GetFileTypeDescription(string ext)
    {
        return ext switch
        {
            ".pdf" => "PDF 文档",
            ".jpg" or ".jpeg" => "JPEG 图片",
            ".png" => "PNG 图片",
            ".bmp" => "BMP 图片",
            ".gif" => "GIF 图片",
            ".tiff" or ".tif" => "TIFF 图片",
            ".webp" => "WebP 图片",
            ".txt" => "文本文件",
            ".doc" or ".docx" => "Word 文档",
            ".xls" or ".xlsx" => "Excel 文件",
            ".mp3" => "MP3 音频",
            ".mp4" => "MP4 视频",
            ".zip" => "ZIP 压缩包",
            ".exe" => "应用程序",
            ".dll" => "DLL 文件",
            ".cs" => "C# 源文件",
            ".html" => "HTML 文件",
            _ => $"{ext.TrimStart('.').ToUpper()} 文件"
        };
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Back || keyData == Keys.BrowserBack)
        {
            GoBack();
            return true;
        }
        if (keyData == Keys.BrowserForward)
        {
            GoForward();
            return true;
        }
        if (keyData == (Keys.Alt | Keys.Up))
        {
            GoUp();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _pictureBox.Image?.Dispose();
        _fileIcons.Dispose();
        base.OnFormClosed(e);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_SMALLICON = 0x1;
}
