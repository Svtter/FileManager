namespace FileManager;

public class CategoryTagDialog : Form
{
    private readonly FolderMetaService _service;
    private ListView _categoryList = null!;
    private ListView _tagList = null!;

    public CategoryTagDialog(FolderMetaService service)
    {
        _service = service;
        InitializeComponents();
        RefreshCategories();
        RefreshTags();
    }

    private void InitializeComponents()
    {
        Text = "管理分类和标签";
        Size = new Size(700, 450);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        _categoryList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = false
        };
        _categoryList.Columns.Add("名称", 180);
        _categoryList.Columns.Add("颜色", 80);

        _tagList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = false
        };
        _tagList.Columns.Add("名称", 180);
        _tagList.Columns.Add("颜色", 80);

        var addCatBtn = new Button { Text = "新增", Size = new Size(60, 28) };
        var editCatBtn = new Button { Text = "编辑", Size = new Size(60, 28) };
        var delCatBtn = new Button { Text = "删除", Size = new Size(60, 28) };
        addCatBtn.Click += (s, e) => AddItem(false);
        editCatBtn.Click += (s, e) => EditItem(false);
        delCatBtn.Click += (s, e) => DeleteItem(false);

        var addTagBtn = new Button { Text = "新增", Size = new Size(60, 28) };
        var editTagBtn = new Button { Text = "编辑", Size = new Size(60, 28) };
        var delTagBtn = new Button { Text = "删除", Size = new Size(60, 28) };
        addTagBtn.Click += (s, e) => AddItem(true);
        editTagBtn.Click += (s, e) => EditItem(true);
        delTagBtn.Click += (s, e) => DeleteItem(true);

        var catBtnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Dock = DockStyle.Bottom };
        catBtnPanel.Controls.AddRange(new Control[] { addCatBtn, editCatBtn, delCatBtn });

        var tagBtnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Dock = DockStyle.Bottom };
        tagBtnPanel.Controls.AddRange(new Control[] { addTagBtn, editTagBtn, delTagBtn });

        var catPanel = new Panel { Dock = DockStyle.Fill };
        catPanel.Controls.Add(_categoryList);
        catPanel.Controls.Add(catBtnPanel);

        var tagPanel = new Panel { Dock = DockStyle.Fill };
        tagPanel.Controls.Add(_tagList);
        tagPanel.Controls.Add(tagBtnPanel);

        var catGroup = new GroupBox { Text = "分类", Dock = DockStyle.Fill };
        catGroup.Controls.Add(catPanel);

        var tagGroup = new GroupBox { Text = "标签", Dock = DockStyle.Fill };
        tagGroup.Controls.Add(tagPanel);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            Panel1MinSize = 100,
            Panel2MinSize = 100
        };
        split.Panel1.Controls.Add(catGroup);
        split.Panel2.Controls.Add(tagGroup);
        split.SplitterDistance = split.Width / 2;

        var closeBtn = new Button { Text = "关闭", Size = new Size(80, 30), Anchor = AnchorStyles.Right };
        closeBtn.Click += (s, e) => Close();
        var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 40 };
        closeBtn.Location = new Point(bottomPanel.Width - 90, 5);
        bottomPanel.Controls.Add(closeBtn);
        bottomPanel.Resize += (s, e) => closeBtn.Location = new Point(bottomPanel.Width - 90, 5);

        Controls.Add(split);
        Controls.Add(bottomPanel);
    }

    private void RefreshCategories()
    {
        _categoryList.BeginUpdate();
        _categoryList.Items.Clear();
        foreach (var cat in _service.GetCategories())
        {
            var item = new ListViewItem(cat.Name) { Tag = cat.Id, BackColor = ColorTranslator.FromHtml(cat.Color), ForeColor = GetContrastColor(cat.Color) };
            item.SubItems.Add(cat.Color);
            _categoryList.Items.Add(item);
        }
        _categoryList.EndUpdate();
    }

    private void RefreshTags()
    {
        _tagList.BeginUpdate();
        _tagList.Items.Clear();
        foreach (var tag in _service.GetTags())
        {
            var item = new ListViewItem(tag.Name) { Tag = tag.Id, BackColor = ColorTranslator.FromHtml(tag.Color), ForeColor = GetContrastColor(tag.Color) };
            item.SubItems.Add(tag.Color);
            _tagList.Items.Add(item);
        }
        _tagList.EndUpdate();
    }

    private static Color GetContrastColor(string hexColor)
    {
        var color = ColorTranslator.FromHtml(hexColor);
        var luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
        return luminance > 0.5 ? Color.Black : Color.White;
    }

    private void AddItem(bool isTag)
    {
        using var dialog = CreateNameColorDialog(isTag ? "新增标签" : "新增分类", "", "#4CAF50");
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            var name = dialog.Controls.Find("nameBox", true).FirstOrDefault() is TextBox tb ? tb.Text.Trim() : "";
            var color = dialog.Controls.Find("colorBtn", true).FirstOrDefault() is Button cb ? cb.Tag as string ?? "#4CAF50" : "#4CAF50";
            if (string.IsNullOrEmpty(name)) return;
            try
            {
                if (isTag) _service.AddTag(name, color);
                else _service.AddCategory(name, color);
                if (isTag) RefreshTags(); else RefreshCategories();
            }
            catch (ArgumentException ex) { MessageBox.Show(ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }

    private void EditItem(bool isTag)
    {
        var list = isTag ? _tagList : _categoryList;
        if (list.SelectedItems.Count == 0) return;
        var selected = list.SelectedItems[0];
        var id = selected.Tag as string;
        var currentName = selected.Text;
        var currentColor = selected.SubItems[1].Text;

        using var dialog = CreateNameColorDialog(isTag ? "编辑标签" : "编辑分类", currentName, currentColor);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            var name = dialog.Controls.Find("nameBox", true).FirstOrDefault() is TextBox tb ? tb.Text.Trim() : "";
            var color = dialog.Controls.Find("colorBtn", true).FirstOrDefault() is Button cb ? cb.Tag as string ?? "#4CAF50" : "#4CAF50";
            if (string.IsNullOrEmpty(name)) return;
            try
            {
                if (isTag) _service.UpdateTag(id!, name, color);
                else _service.UpdateCategory(id!, name, color);
                if (isTag) RefreshTags(); else RefreshCategories();
            }
            catch (ArgumentException ex) { MessageBox.Show(ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }

    private void DeleteItem(bool isTag)
    {
        var list = isTag ? _tagList : _categoryList;
        if (list.SelectedItems.Count == 0) return;
        var name = list.SelectedItems[0].Text;
        if (MessageBox.Show($"确定要删除\"{name}\"吗？关联也将被移除。", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        var id = list.SelectedItems[0].Tag as string;
        try
        {
            if (isTag) _service.DeleteTag(id!);
            else _service.DeleteCategory(id!);
            if (isTag) RefreshTags(); else RefreshCategories();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private static Form CreateNameColorDialog(string title, string name, string color)
    {
        var form = new Form
        {
            Text = title,
            Size = new Size(350, 170),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var nameLabel = new Label { Text = "名称:", Location = new Point(12, 15), AutoSize = true };
        var nameBox = new TextBox { Name = "nameBox", Text = name, Location = new Point(70, 12), Width = 250 };
        nameBox.SelectAll();
        nameBox.Focus();

        var colorLabel = new Label { Text = "颜色:", Location = new Point(12, 50), AutoSize = true };
        var colorBtn = new Button
        {
            Name = "colorBtn",
            Text = color,
            Location = new Point(70, 47),
            Width = 100,
            BackColor = ColorTranslator.FromHtml(color),
            Tag = color
        };
        colorBtn.Click += (s, e) =>
        {
            using var cd = new ColorDialog { Color = ColorTranslator.FromHtml(colorBtn.Tag as string ?? "#4CAF50"), FullOpen = true };
            if (cd.ShowDialog(form) == DialogResult.OK)
            {
                var hex = $"#{cd.Color.R:X2}{cd.Color.G:X2}{cd.Color.B:X2}";
                colorBtn.BackColor = cd.Color;
                colorBtn.Text = hex;
                colorBtn.Tag = hex;
            }
        };

        var okBtn = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(160, 90) };
        var cancelBtn = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(250, 90) };
        form.Controls.AddRange(new Control[] { nameLabel, nameBox, colorLabel, colorBtn, okBtn, cancelBtn });
        form.AcceptButton = okBtn;
        form.CancelButton = cancelBtn;
        return form;
    }
}
