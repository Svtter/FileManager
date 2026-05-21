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

    private const string AppId = "FileManager.Local.1";

    public MainForm()
    {
        _fileIcons = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
        InitializeComponents();
        LoadDrives();
        NavigateTo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        Load += (_, _) => SaveIconToFile();
    }

    private ContextMenuStrip _fileListContextMenu = null!;
    private ContextMenuStrip _treeContextMenu = null!;
    private ContextMenuStrip _backgroundContextMenu = null!;

    private void InitializeComponents()
    {
        Text = "文件管理器";
        Size = new Size(1400, 900);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(800, 600);
        KeyPreview = true;
        Icon = GenerateAppIcon();

        InitializeContextMenus();

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
        _fileListView.MouseClick += FileListView_MouseClick;

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
        _treeView.NodeMouseClick += TreeView_NodeMouseClick;

        var treePanel = new Panel { Dock = DockStyle.Fill };
        treePanel.Controls.Add(_treeView);

        _previewSplitter = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
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
            Panel1MinSize = 150,
            FixedPanel = FixedPanel.Panel1,
            BackColor = SystemColors.Control
        };
        _mainSplitter.Panel1.Controls.Add(treePanel);
        _mainSplitter.Panel2.Controls.Add(_previewSplitter);

        Controls.AddRange(new Control[] { _mainSplitter, toolbar, _statusLabel });

        _mainSplitter.SplitterDistance = 300;
        _previewSplitter.SplitterDistance = 400;

        AddSystemIcons();
    }

    private void InitializeContextMenus()
    {
        _fileListContextMenu = new ContextMenuStrip();
        _fileListContextMenu.Items.Add("打开", null, (_, _) => OpenSelectedItem());
        _fileListContextMenu.Items.Add(new ToolStripSeparator());
        _fileListContextMenu.Items.Add("复制", null, (_, _) => CopySelectedItem());
        _fileListContextMenu.Items.Add("剪切", null, (_, _) => CutSelectedItem());
        _fileListContextMenu.Items.Add(new ToolStripSeparator());
        _fileListContextMenu.Items.Add("复制路径", null, (_, _) => CopySelectedItemPath());
        _fileListContextMenu.Items.Add(new ToolStripSeparator());
        _fileListContextMenu.Items.Add("重命名", null, (_, _) => RenameSelectedItem());
        _fileListContextMenu.Items.Add("删除", null, (_, _) => DeleteSelectedItem());
        _fileListContextMenu.Items.Add(new ToolStripSeparator());
        _fileListContextMenu.Items.Add("属性", null, (_, _) => ShowSelectedItemProperties());

        _treeContextMenu = new ContextMenuStrip();
        _treeContextMenu.Items.Add("展开全部", null, (_, _) => ExpandAllTreeNode());
        _treeContextMenu.Items.Add("折叠全部", null, (_, _) => CollapseAllTreeNode());
        _treeContextMenu.Items.Add(new ToolStripSeparator());
        _treeContextMenu.Items.Add("新建文件夹", null, (_, _) => CreateNewFolder(GetTreeViewSelectedPath()));
        _treeContextMenu.Items.Add(new ToolStripSeparator());
        _treeContextMenu.Items.Add("刷新", null, (_, _) => RefreshTreeView());

        _backgroundContextMenu = new ContextMenuStrip();
        _backgroundContextMenu.Items.Add("新建文件夹", null, (_, _) => CreateNewFolder(_currentPath));
        _backgroundContextMenu.Items.Add(new ToolStripSeparator());
        _backgroundContextMenu.Items.Add("在终端中打开", null, (_, _) => OpenTerminal());
        _backgroundContextMenu.Items.Add(new ToolStripSeparator());
        _backgroundContextMenu.Items.Add("刷新", null, (_, _) => RefreshFileList());
    }

    private void FileListView_MouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;

        var hitTest = _fileListView.HitTest(e.Location);
        if (hitTest.Item != null)
        {
            if (!_fileListView.SelectedItems.Contains(hitTest.Item))
            {
                _fileListView.SelectedItems.Clear();
                hitTest.Item.Selected = true;
            }
            _fileListContextMenu.Show(_fileListView, e.Location);
        }
        else
        {
            _fileListView.SelectedItems.Clear();
            _backgroundContextMenu.Show(_fileListView, e.Location);
        }
    }

    private void TreeView_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        _treeView.SelectedNode = e.Node;
        _treeContextMenu.Show(_treeView, e.Location);
    }

    private void OpenSelectedItem()
    {
        if (_fileListView.SelectedItems.Count == 0) return;
        var path = _fileListView.SelectedItems[0].Tag as string;
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

    private void CopySelectedItem()
    {
        var path = GetSelectedFilePath();
        if (path == null) return;
        Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection { path });
    }

    private void CutSelectedItem()
    {
        var path = GetSelectedFilePath();
        if (path == null) return;

        var files = new System.Collections.Specialized.StringCollection { path };
        byte[] moveEffect = [2, 0, 0, 0];
        var dropEffect = new MemoryStream();
        dropEffect.Write(moveEffect, 0, moveEffect.Length);
        var data = new DataObject(DataFormats.FileDrop, files);
        data.SetData("Preferred DropEffect", dropEffect);
        Clipboard.SetDataObject(data, true);
    }

    private void CopySelectedItemPath()
    {
        var path = GetSelectedFilePath();
        if (path != null)
            Clipboard.SetText(path);
    }

    private void RenameSelectedItem()
    {
        if (_fileListView.SelectedItems.Count == 0) return;
        var oldPath = _fileListView.SelectedItems[0].Tag as string;
        if (oldPath == null) return;

        var oldName = Path.GetFileName(oldPath);
        var dialog = new Form
        {
            Text = "重命名",
            Size = new Size(400, 130),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };
        var label = new Label { Text = "新名称:", Location = new Point(12, 15), AutoSize = true };
        var textBox = new TextBox { Text = oldName, Location = new Point(12, 35), Width = 360 };
        var okBtn = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(200, 65) };
        var cancelBtn = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(290, 65) };
        dialog.Controls.AddRange(new Control[] { label, textBox, okBtn, cancelBtn });
        dialog.AcceptButton = okBtn;
        dialog.CancelButton = cancelBtn;
        textBox.SelectAll();
        textBox.Focus();

        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var newName = textBox.Text.Trim();
        if (string.IsNullOrEmpty(newName) || newName == oldName) return;

        try
        {
            var dir = Path.GetDirectoryName(oldPath)!;
            var newPath = Path.Combine(dir, newName);
            if (Directory.Exists(oldPath))
                Directory.Move(oldPath, newPath);
            else
                File.Move(oldPath, newPath);
            LoadFiles(_currentPath!);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"重命名失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteSelectedItem()
    {
        var path = GetSelectedFilePath();
        if (path == null) return;

        var name = Path.GetFileName(path);
        var result = MessageBox.Show($"确定要删除 \"{name}\" 吗？", "确认删除",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result != DialogResult.Yes) return;

        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
            else
                File.Delete(path);
            ClearPreview();
            LoadFiles(_currentPath!);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"删除失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowSelectedItemProperties()
    {
        var path = GetSelectedFilePath();
        if (path == null) return;

        ShellExecute(IntPtr.Zero, "properties", path, "", "", SW_SHOW);
    }

    private void CreateNewFolder(string? parentPath)
    {
        if (parentPath == null || !Directory.Exists(parentPath)) return;

        var folderName = "新建文件夹";
        var fullPath = Path.Combine(parentPath, folderName);
        int counter = 1;
        while (Directory.Exists(fullPath))
        {
            fullPath = Path.Combine(parentPath, $"{folderName} ({counter})");
            counter++;
        }

        try
        {
            Directory.CreateDirectory(fullPath);
            if (parentPath == _currentPath)
                LoadFiles(_currentPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"创建文件夹失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenTerminal()
    {
        if (_currentPath == null) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe")
            {
                WorkingDirectory = _currentPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法打开终端: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshFileList()
    {
        if (_currentPath != null)
            LoadFiles(_currentPath);
    }

    private void RefreshTreeView()
    {
        var selectedPath = _treeView.SelectedNode?.Tag as string;
        LoadDrives();
        if (selectedPath != null)
            SelectTreeNode(selectedPath);
    }

    private void ExpandAllTreeNode()
    {
        if (_treeView.SelectedNode != null)
            _treeView.SelectedNode.ExpandAll();
    }

    private void CollapseAllTreeNode()
    {
        if (_treeView.SelectedNode != null)
            _treeView.SelectedNode.Collapse();
    }

    private string? GetSelectedFilePath()
    {
        if (_fileListView.SelectedItems.Count == 0) return null;
        return _fileListView.SelectedItems[0].Tag as string;
    }

    private string? GetTreeViewSelectedPath()
    {
        return _treeView.SelectedNode?.Tag as string;
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

    private const int SW_SHOW = 5;

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr ShellExecute(IntPtr hwnd, string lpOperation, string lpFile, string? lpParameters, string? lpDirectory, int nShowCmd);

    private static Icon GenerateAppIcon()
    {
        var sizes = new[] { 16, 32, 48, 256 };

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.Default, leaveOpen: true))
        {
            writer.Write((short)0);
            writer.Write((short)1);
            writer.Write((short)sizes.Length);
            var headerSize = 6 + sizes.Length * 16;
            var dataOffset = headerSize;

            using var imageData = new MemoryStream();
            foreach (var sz in sizes)
            {
                using var bmp = new Bitmap(sz, sz, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using var g = Graphics.FromImage(bmp);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                DrawIcon(g, sz);

                var pngData = BitmapToPng(bmp);
                var width = sz >= 256 ? (byte)0 : (byte)sz;
                writer.Write(width);
                writer.Write(width);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((short)1);
                writer.Write((short)32);
                writer.Write(pngData.Length);
                writer.Write(dataOffset);
                dataOffset += pngData.Length;
                imageData.Write(pngData);
            }

            writer.Flush();
            writer.Write(imageData.ToArray());
        }

        ms.Position = 0;
        return new Icon(ms);
    }

    private static void DrawIcon(Graphics g, int size)
    {
        var scale = size / 256f;
        var folderRect = new RectangleF(30 * scale, 50 * scale, 196 * scale, 150 * scale);
        var tabRect = new RectangleF(30 * scale, 35 * scale, 80 * scale, 25 * scale);

        using (var path = new System.Drawing.Drawing2D.GraphicsPath())
        {
            path.AddRectangle(tabRect);
            g.FillPath(new SolidBrush(Color.FromArgb(255, 200, 60)), path);
        }

        using (var path = new System.Drawing.Drawing2D.GraphicsPath())
        {
            path.AddRectangle(folderRect);
            g.FillPath(new SolidBrush(Color.FromArgb(255, 210, 70)), path);
            using var pen = new Pen(Color.FromArgb(200, 160, 30), 3 * scale);
            g.DrawPath(pen, path);
        }

        using (var pen = new Pen(Color.FromArgb(200, 160, 30), 3 * scale))
        {
            g.DrawRectangle(pen, tabRect.X, tabRect.Y, tabRect.Width, tabRect.Height);
        }

        using (var magnifier = new System.Drawing.Drawing2D.GraphicsPath())
        {
            magnifier.AddEllipse(130 * scale, 90 * scale, 70 * scale, 70 * scale);
            g.FillPath(new SolidBrush(Color.FromArgb(100, 180, 255)), magnifier);
            using var pen = new Pen(Color.FromArgb(40, 100, 200), 3 * scale);
            g.DrawPath(pen, magnifier);

            using var handlePen = new Pen(Color.FromArgb(40, 100, 200), 5 * scale) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            g.DrawLine(handlePen, new PointF(182 * scale, 155 * scale), new PointF(210 * scale, 185 * scale));
        }
    }

    private static byte[] BitmapToPng(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }

    private void SaveIconToFile()
    {
        try
        {
            var exeDir = Path.GetDirectoryName(Environment.ProcessPath!)!;
            var icoPath = Path.Combine(exeDir, "app.ico");
            if (!File.Exists(icoPath))
            {
                using var fs = new FileStream(icoPath, FileMode.Create);
                Icon.Save(fs);
            }

            var lnkPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "Windows", "Start Menu", "Programs", "FileManager.lnk");
            if (!File.Exists(lnkPath)) return;

            var shl = (IShellLinkForIcon)new ShellLinkForIcon();
            var persistFile = (IPersistFileForIcon)shl;
            persistFile.Load(lnkPath, 2);
            shl.SetIconLocation(icoPath, 0);
            persistFile.Save(lnkPath, false);
            Marshal.ReleaseComObject(shl);
        }
        catch { }
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkForIcon;

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkForIcon
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cch, out int iIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFileForIcon
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
}
