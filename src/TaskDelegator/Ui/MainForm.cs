using System.Diagnostics;
using System.Drawing;
using TaskDelegator.Models;
using TaskDelegator.Services;

namespace TaskDelegator.Ui;

public sealed class MainForm : Form
{
    private static readonly Color Accent = Color.FromArgb(15, 108, 189);

    private readonly TextBox _filter = new();
    private readonly ListView _software = NewListView();
    private readonly ListView _users = NewListView();

    private readonly TextBox _exe = new();
    private readonly Button _browse = new() { Text = "Browse..." };
    private readonly TextBox _args = new();
    private readonly TextBox _shortcut = new();
    private readonly RadioButton _modeSystem = new() { Text = "SYSTEM (no password)", Checked = true, AutoSize = true };
    private readonly RadioButton _modeAccount = new() { Text = "Admin account", AutoSize = true };
    // Explains the identity trap behind SQL Server "CREATE DATABASE permission denied" (Error 262):
    // SYSTEM mode runs the app AS NT AUTHORITY\SYSTEM, which Windows-authenticated services treat
    // as a non-admin login. The full reason is in the hover tooltip set in BuildSettings().
    private readonly Label _modeHint = new()
    {
        AutoSize = true,
        ForeColor = Color.DimGray,
        Margin = new Padding(2, 2, 0, 0),
        Text = "SYSTEM runs the app as NT AUTHORITY\\SYSTEM. For SQL Server tools and similar, " +
               "pick \"Admin account\" instead (hover for why)."
    };
    private readonly ToolTip _tips = new() { AutoPopDelay = 30000, InitialDelay = 350, ReshowDelay = 120, ShowAlways = true };
    private readonly TextBox _account = new();
    private readonly TextBox _password = new() { UseSystemPasswordChar = true };
    private readonly Label _accountLbl = new() { Text = "Account:", AutoSize = true };
    private readonly Label _passwordLbl = new() { Text = "Password:", AutoSize = true };
    private readonly CheckBox _makeShortcut = new() { Text = "Add a direct desktop shortcut for this app", Checked = true, AutoSize = true };
    private readonly CheckBox _makeLauncher = new() { Text = "Add the 'Allowed Programs' launcher to the user's desktop", Checked = true, AutoSize = true };
    private readonly Button _create = new() { Text = "Create delegation" };

    private readonly ListView _delegations = NewListView();
    private readonly Button _refresh = new() { Text = "Refresh" };
    private readonly Button _remove = new() { Text = "Remove selected" };

    private readonly Label _status = new() { Text = "", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 8, 0) };

    private List<InstalledApp> _allApps = new();

    public MainForm()
    {
        Text = $"Privileged App Delegation {AppInfo.Version} — Administrator console";
        Width = 920;
        Height = 730;
        MinimumSize = new Size(820, 672);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);

        Controls.Add(BuildTabs());
        Controls.Add(BuildCredits());
        Controls.Add(BuildStatusBar());
        Controls.Add(BuildHeader());

        Load += OnLoad;
        _filter.TextChanged += (_, _) => PopulateSoftware();
        _software.SelectedIndexChanged += OnSoftwareSelected;
        _browse.Click += OnBrowse;
        _modeSystem.CheckedChanged += (_, _) => UpdateModeFields();
        _modeAccount.CheckedChanged += (_, _) => UpdateModeFields();
        _create.Click += OnCreate;
        _refresh.Click += (_, _) => RefreshDelegations();
        _remove.Click += OnRemove;
    }

    // ---------- layout ----------

    private Control BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 66, BackColor = Accent };
        var title = new Label
        {
            Text = "Privileged Application Delegation",
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(14, 10)
        };
        var who = new Label
        {
            Text = $"Signed in as administrator: {Environment.UserDomainName}\\{Environment.UserName}   (configuration requires elevation)",
            ForeColor = Color.FromArgb(220, 236, 255),
            AutoSize = true,
            Location = new Point(16, 40)
        };
        var version = new Label
        {
            Text = AppInfo.Version,
            Dock = DockStyle.Right,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = false,
            Width = 130,
            Padding = new Padding(0, 0, 14, 0)
        };
        header.Controls.Add(version);
        header.Controls.Add(title);
        header.Controls.Add(who);
        return header;
    }

    private Control BuildStatusBar()
    {
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = SystemColors.ControlLight };
        bar.Controls.Add(_status);
        return bar;
    }

    // Creator credit + links to the author's GitHub and the project repository.
    private Control BuildCredits()
    {
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = SystemColors.ControlLightLight };
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(8, 4, 8, 0)
        };
        flow.Controls.Add(new Label { Text = "Created by Kamran Najafi —", AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(0, 1, 2, 0) });
        flow.Controls.Add(NewLink("github.com/KamranNajafi", "https://github.com/KamranNajafi"));
        flow.Controls.Add(new Label { Text = "·", AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(3, 1, 3, 0) });
        flow.Controls.Add(NewLink("Project repository", "https://github.com/KamranNajafi/Task-Scheduler-Windows"));
        bar.Controls.Add(flow);
        return bar;
    }

    private static LinkLabel NewLink(string text, string url)
    {
        var link = new LinkLabel { Text = text, AutoSize = true, Margin = new Padding(0, 1, 0, 0), LinkColor = Accent };
        link.LinkClicked += (_, _) => OpenUrl(url);
        return link;
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* ignore — opening a browser is best-effort */ }
    }

    private Control BuildTabs()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(14, 6) };
        tabs.TabPages.Add(BuildCreateTab());
        tabs.TabPages.Add(BuildManageTab());
        return tabs;
    }

    private TabPage BuildCreateTab()
    {
        var page = new TabPage("Create delegation") { Padding = new Padding(10) };

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 286));

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };

        // left: installed software
        var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _filter.Dock = DockStyle.Fill;
        _software.Dock = DockStyle.Fill;
        _software.Columns.Add("Application", 300);
        _software.Columns.Add("Version", 110);
        left.Controls.Add(new Label { Text = "1. Installed application (type to filter)", AutoSize = true, Margin = new Padding(0, 4, 0, 2) }, 0, 0);
        left.Controls.Add(_filter, 0, 1);
        left.Controls.Add(_software, 0, 2);

        // right: target user
        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _users.Dock = DockStyle.Fill;
        _users.Columns.Add("User", 150);
        _users.Columns.Add("Role", 90);
        _users.Columns.Add("Enabled", 70);
        right.Controls.Add(new Label { Text = "2. Target Windows user", AutoSize = true, Margin = new Padding(0, 4, 0, 2) }, 0, 0);
        right.Controls.Add(_users, 0, 1);

        split.Panel1.Controls.Add(left);
        split.Panel2.Controls.Add(right);

        root.Controls.Add(split, 0, 0);
        root.Controls.Add(BuildSettings(), 0, 1);
        page.Controls.Add(root);
        return page;
    }

    private Control BuildSettings()
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 7, Padding = new Padding(2, 6, 2, 2) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        for (int i = 0; i < 7; i++) t.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _exe.Dock = DockStyle.Fill;
        _args.Dock = DockStyle.Fill;
        _shortcut.Dock = DockStyle.Fill;
        _account.Dock = DockStyle.Fill;
        _password.Dock = DockStyle.Fill;
        _browse.Width = 84;
        _accountLbl.Anchor = AnchorStyles.Left;
        _passwordLbl.Anchor = AnchorStyles.Left;

        var modeRow = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0), FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        modeRow.Controls.Add(_modeSystem);
        modeRow.Controls.Add(_modeAccount);

        // Radios on top, a gray one-line hint underneath — all within the single "Run as" cell.
        var modeFlow = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        modeFlow.Controls.Add(modeRow);
        modeFlow.Controls.Add(_modeHint);

        string systemTip =
            "SYSTEM (no password): the app runs as NT AUTHORITY\\SYSTEM.\r\n" +
            "Apps that sign in to another service using your Windows identity — e.g. SQL Server\r\n" +
            "Management Studio — then connect AS SYSTEM, which since SQL Server 2012 is NOT a\r\n" +
            "sysadmin by default. That causes errors such as \"CREATE DATABASE permission denied\r\n" +
            "in database 'master'\" (SQL Server Error 262).\r\n\r\n" +
            "For those apps pick \"Admin account\" below and enter a Windows account that is a SQL\r\n" +
            "Server administrator — or grant that login the needed rights inside SQL Server.";
        _tips.SetToolTip(_modeSystem, systemTip);
        _tips.SetToolTip(_modeHint, systemTip);
        _tips.SetToolTip(_modeAccount,
            "Admin account: the app runs as the Windows account you enter (its password is stored\r\n" +
            "encrypted with DPAPI, readable only by SYSTEM / Administrators).\r\n" +
            "Use this when the app must act as a specific privileged identity — e.g. a user who is\r\n" +
            "a SQL Server administrator.");

        _create.Size = new Size(200, 30);
        _create.Anchor = AnchorStyles.Right;
        _create.BackColor = Accent;
        _create.ForeColor = Color.White;
        _create.FlatStyle = FlatStyle.Flat;
        _create.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);

        var exeLbl = new Label { Text = "Executable:", AutoSize = true, Anchor = AnchorStyles.Left };
        var argsLbl = new Label { Text = "Arguments:", AutoSize = true, Anchor = AnchorStyles.Left };
        var scLbl = new Label { Text = "Shortcut name:", AutoSize = true, Anchor = AnchorStyles.Left };
        var runLbl = new Label { Text = "Run as:", AutoSize = true, Anchor = AnchorStyles.Left };

        t.Controls.Add(exeLbl, 0, 0);
        t.Controls.Add(_exe, 1, 0);
        t.SetColumnSpan(_exe, 2);
        t.Controls.Add(_browse, 3, 0);

        t.Controls.Add(argsLbl, 0, 1);
        t.Controls.Add(_args, 1, 1);
        t.SetColumnSpan(_args, 3);

        t.Controls.Add(scLbl, 0, 2);
        t.Controls.Add(_shortcut, 1, 2);
        t.SetColumnSpan(_shortcut, 3);

        t.Controls.Add(runLbl, 0, 3);
        t.Controls.Add(modeFlow, 1, 3);
        t.SetColumnSpan(modeFlow, 3);

        t.Controls.Add(_accountLbl, 0, 4);
        t.Controls.Add(_account, 1, 4);
        t.Controls.Add(_passwordLbl, 2, 4);
        t.Controls.Add(_password, 3, 4);

        t.Controls.Add(_makeShortcut, 1, 5);
        t.SetColumnSpan(_makeShortcut, 3);

        t.Controls.Add(_makeLauncher, 1, 6);
        t.SetColumnSpan(_makeLauncher, 2);
        t.Controls.Add(_create, 3, 6);

        return t;
    }

    private TabPage BuildManageTab()
    {
        var page = new TabPage("Existing delegations") { Padding = new Padding(10) };

        _delegations.Columns.Add("Task", 170);
        _delegations.Columns.Add("Name", 120);
        _delegations.Columns.Add("Application", 260);
        _delegations.Columns.Add("Runs as", 90);
        _delegations.Columns.Add("User", 90);
        _delegations.Columns.Add("Last run", 120);
        _delegations.Dock = DockStyle.Fill;

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.LeftToRight };
        _refresh.Width = 100;
        _remove.Width = 140;
        buttons.Controls.Add(_refresh);
        buttons.Controls.Add(_remove);

        page.Controls.Add(_delegations);
        page.Controls.Add(buttons);
        return page;
    }

    private static ListView NewListView() => new()
    {
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = false,
        HideSelection = false,
        GridLines = true
    };

    // ---------- behavior ----------

    private void OnLoad(object? sender, EventArgs e)
    {
        UpdateModeFields();
        _account.Text = $"{Environment.UserDomainName}\\{Environment.UserName}";
        Cursor = Cursors.WaitCursor;
        try
        {
            _allApps = SoftwareInventory.GetAll();
            PopulateSoftware();
            PopulateUsers();
            RefreshDelegations();
            SetStatus($"Loaded {_allApps.Count} applications.");
        }
        catch (Exception ex) { SetStatus("Load error: " + ex.Message, true); }
        finally { Cursor = Cursors.Default; }
    }

    private void PopulateSoftware()
    {
        string q = _filter.Text.Trim();
        IEnumerable<InstalledApp> src = _allApps;
        if (q.Length > 0)
            src = _allApps.Where(a => a.DisplayName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);

        _software.BeginUpdate();
        _software.Items.Clear();
        foreach (var a in src)
        {
            var item = new ListViewItem(new[] { a.DisplayName, a.Version ?? "" }) { Tag = a };
            _software.Items.Add(item);
        }
        _software.EndUpdate();
    }

    private void PopulateUsers()
    {
        _users.BeginUpdate();
        _users.Items.Clear();
        foreach (var u in UserInventory.GetAll())
        {
            var item = new ListViewItem(new[] { u.Name, u.Role, u.Enabled ? "Yes" : "No" }) { Tag = u };
            _users.Items.Add(item);
        }
        _users.EndUpdate();
    }

    private void OnSoftwareSelected(object? sender, EventArgs e)
    {
        if (_software.SelectedItems.Count == 0) return;
        if (_software.SelectedItems[0].Tag is not InstalledApp app) return;

        if (!string.IsNullOrEmpty(app.Executable)) _exe.Text = app.Executable;
        if (string.IsNullOrWhiteSpace(_shortcut.Text)) _shortcut.Text = app.DisplayName;

        if (string.IsNullOrEmpty(app.Executable))
            SetStatus($"No executable auto-detected for '{app.DisplayName}'. Use Browse... to pick the .exe.", true);
        else
            SetStatus("");
    }

    private void OnBrowse(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _exe.Text = dlg.FileName;
            if (string.IsNullOrWhiteSpace(_shortcut.Text))
                _shortcut.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
        }
    }

    private void UpdateModeFields()
    {
        bool account = _modeAccount.Checked;
        _account.Enabled = account;
        _password.Enabled = account;
        _accountLbl.Enabled = account;
        _passwordLbl.Enabled = account;
    }

    private void OnCreate(object? sender, EventArgs e)
    {
        try
        {
            string exe = NormalizeExePath(_exe.Text);
            if (exe.Length == 0 || !File.Exists(exe)) { SetStatus("Choose a valid executable (.exe).", true); return; }
            _exe.Text = exe; // reflect the cleaned path (surrounding quotes / ,0 index / %vars% removed)
            if (_users.SelectedItems.Count == 0 || _users.SelectedItems[0].Tag is not LocalUserInfo user)
            { SetStatus("Select a target Windows user.", true); return; }

            var mode = _modeAccount.Checked ? RunAsMode.Account : RunAsMode.System;
            if (mode == RunAsMode.Account &&
                (_account.Text.Trim().Length == 0 || _password.Text.Length == 0))
            { SetStatus("Account mode needs an administrator account and password.", true); return; }

            if (!DelegationManager.IsTrustedLocation(exe))
            {
                var w = MessageBox.Show(this,
                    "The selected executable is not under Program Files or Windows.\n\n" +
                    "If this folder is writable by the user, they could replace the program and " +
                    "run arbitrary code with elevated rights. Continue anyway?",
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (w != DialogResult.Yes) return;
            }

            SetStatus("Creating delegation...");
            Cursor = Cursors.WaitCursor;

            var result = DelegationManager.Create(
                exe, _args.Text.Trim(), _shortcut.Text.Trim(), user,
                mode, _account.Text.Trim(), _password.Text,
                _makeShortcut.Checked, _makeLauncher.Checked);

            _password.Clear();
            RefreshDelegations();
            SetStatus($"Done. '{user.Name}' can now run '{_shortcut.Text.Trim()}' elevated via {result.TaskPath}.{result.Note}");

            MessageBox.Show(this,
                $"Delegation created.\n\nUser '{user.Name}' can now launch '{_shortcut.Text.Trim()}' " +
                $"with administrator rights{(string.IsNullOrEmpty(result.ShortcutPath) ? "" : " from their desktop shortcut")}.\n\n" +
                $"Task: {result.TaskPath}{result.Note}",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            SetStatus("Failed: " + ex.Message, true);
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { Cursor = Cursors.Default; }
    }

    private void RefreshDelegations()
    {
        _delegations.BeginUpdate();
        _delegations.Items.Clear();
        try
        {
            foreach (var d in DelegationManager.List())
            {
                _delegations.Items.Add(new ListViewItem(new[]
                {
                    d.TaskName, d.FriendlyName, d.App, d.RunAs, d.TargetUser, d.LastRun
                }));
            }
        }
        catch (Exception ex) { SetStatus("List error: " + ex.Message, true); }
        finally { _delegations.EndUpdate(); }
    }

    private void OnRemove(object? sender, EventArgs e)
    {
        if (_delegations.SelectedItems.Count == 0) { SetStatus("Select a delegation to remove.", true); return; }
        string task = _delegations.SelectedItems[0].Text;
        var r = MessageBox.Show(this, $"Remove delegation '{task}' and its desktop shortcut(s)?",
            Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (r != DialogResult.Yes) return;
        try
        {
            DelegationManager.Remove(task);
            RefreshDelegations();
            SetStatus($"Removed '{task}'.");
        }
        catch (Exception ex) { SetStatus("Remove failed: " + ex.Message, true); }
    }

    // The Executable box may receive a path copied from the registry, a shortcut's
    // "Target", or an UninstallString: those wrap a path containing spaces in quotes
    // ("C:\...\app.exe"), may append an icon index (",0"), and may use %ProgramFiles%
    // style variables. File.Exists rejects all of those, so normalize before validating.
    private static string NormalizeExePath(string raw)
    {
        string s = raw.Trim();
        if (s.Length == 0) return s;

        if (s.StartsWith("\"", StringComparison.Ordinal))
        {
            int end = s.IndexOf('"', 1);
            if (end > 1) s = s.Substring(1, end - 1);   // text inside the first quote pair
        }
        else
        {
            int comma = s.LastIndexOf(',');             // strip a trailing icon index: "...app.exe,0"
            if (comma > 0 && int.TryParse(s.Substring(comma + 1).Trim(), out _))
                s = s.Substring(0, comma);
        }

        s = s.Trim();
        try { s = Environment.ExpandEnvironmentVariables(s); } catch { /* leave as-is */ }
        return s.Trim();
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.ForeColor = error ? Color.DarkRed : Color.DimGray;
    }
}
