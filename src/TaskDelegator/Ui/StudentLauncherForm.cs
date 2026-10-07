using System.Diagnostics;
using System.Drawing;
using TaskDelegator.Models;
using TaskDelegator.Services;

namespace TaskDelegator.Ui;

/// <summary>
/// Run-only launcher for standard users (e.g. students in a training lab).
/// Lists the apps delegated to the current user and lets them launch one.
/// There is intentionally no uninstall / remove / configuration action here.
/// </summary>
public sealed class StudentLauncherForm : Form
{
    private static readonly Color Accent = Color.FromArgb(15, 108, 189);

    private readonly ListView _apps = new()
    {
        Dock = DockStyle.Fill,
        View = View.Tile,
        MultiSelect = false,
        HideSelection = false
    };
    private readonly ImageList _icons = new() { ImageSize = new Size(32, 32), ColorDepth = ColorDepth.Depth32Bit };
    private readonly Button _run = new() { Text = "Run", Width = 120, Height = 32 };
    private readonly Button _refresh = new() { Text = "Refresh", Width = 100, Height = 32 };
    private readonly LinkLabel _admin = new() { Text = "Administrator…", AutoSize = true };
    private readonly Label _status = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 8, 0) };

    public StudentLauncherForm()
    {
        Text = "Allowed Programs";
        Width = 560;
        Height = 480;
        MinimumSize = new Size(440, 360);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);

        _apps.LargeImageList = _icons;
        _apps.TileSize = new Size(260, 40);

        Controls.Add(BuildBody());
        Controls.Add(BuildButtons());
        Controls.Add(BuildStatus());
        Controls.Add(BuildHeader());

        Load += (_, _) => Reload();
        _apps.DoubleClick += (_, _) => RunSelected();
        _run.Click += (_, _) => RunSelected();
        _refresh.Click += (_, _) => Reload();
        _admin.LinkClicked += (_, _) => OpenAdmin();
    }

    private Control BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Accent };
        header.Controls.Add(new Label
        {
            Text = "Allowed Programs",
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(14, 8)
        });
        header.Controls.Add(new Label
        {
            Text = $"Signed in as {Environment.UserName}. Double-click a program to run it with administrator rights.",
            ForeColor = Color.FromArgb(220, 236, 255),
            AutoSize = true,
            Location = new Point(16, 34)
        });
        return header;
    }

    private Control BuildBody()
    {
        var p = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        p.Controls.Add(_apps);
        return p;
    }

    private Control BuildButtons()
    {
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(10, 8, 10, 8) };

        _run.BackColor = Accent;
        _run.ForeColor = Color.White;
        _run.FlatStyle = FlatStyle.Flat;
        _run.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        _run.Margin = new Padding(0, 0, 8, 0);

        var left = new FlowLayoutPanel { Dock = DockStyle.Left, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true };
        left.Controls.Add(_run);
        left.Controls.Add(_refresh);

        var right = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, AutoSize = true };
        _admin.Margin = new Padding(0, 10, 4, 0);
        right.Controls.Add(_admin);

        bar.Controls.Add(left);
        bar.Controls.Add(right);
        return bar;
    }

    private Control BuildStatus()
    {
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 24, BackColor = SystemColors.ControlLight };
        bar.Controls.Add(_status);
        return bar;
    }

    private void Reload()
    {
        _apps.BeginUpdate();
        _apps.Items.Clear();
        _icons.Images.Clear();
        try
        {
            var entries = StudentMenu.GetForCurrentUser();
            foreach (var e in entries)
            {
                var item = new ListViewItem(e.FriendlyName) { Tag = e };
                TryAddIcon(e, item);
                _apps.Items.Add(item);
            }
            SetStatus(entries.Count == 0
                ? "No programs have been assigned to you yet."
                : $"{entries.Count} program(s) available.");
        }
        catch (Exception ex) { SetStatus("Could not load programs: " + ex.Message, true); }
        finally { _apps.EndUpdate(); }
    }

    private void TryAddIcon(MenuEntry e, ListViewItem item)
    {
        try
        {
            if (!string.IsNullOrEmpty(e.IconPath) && File.Exists(e.IconPath))
            {
                using var ico = Icon.ExtractAssociatedIcon(e.IconPath);
                if (ico is not null)
                {
                    _icons.Images.Add(e.TaskName, ico);
                    item.ImageKey = e.TaskName;
                }
            }
        }
        catch { /* no icon is fine */ }
    }

    private void RunSelected()
    {
        if (_apps.SelectedItems.Count == 0 || _apps.SelectedItems[0].Tag is not MenuEntry e)
        {
            SetStatus("Select a program first.", true);
            return;
        }
        try
        {
            SetStatus($"Starting '{e.FriendlyName}'…");
            StudentMenu.Run(e);
            SetStatus($"Started '{e.FriendlyName}'. It may take a moment to appear.");
        }
        catch (Exception ex) { SetStatus($"Could not start '{e.FriendlyName}': {ex.Message}", true); }
    }

    private void OpenAdmin()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath ?? Application.ExecutablePath,
                Arguments = "--admin",
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(psi);
        }
        catch (Exception)
        {
            SetStatus("Administrator mode was cancelled (administrator rights required).", true);
        }
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.ForeColor = error ? Color.DarkRed : Color.DimGray;
    }
}
