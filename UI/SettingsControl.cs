using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PitWall.Core;
using PitWall.Replay;
using PitWall.Storage;

namespace PitWall.UI
{
    /// <summary>
    /// Settings control for PitWall plugin
    /// Allows users to import historical replay data
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        private readonly PitWallSettings _settings;
        private readonly SQLiteProfileDatabase _database;
        private ReplayProcessor? _replayProcessor;
        private bool _isProcessing;

        private TextBox _replayFolderTextBox;
        private Button _browseFolderButton;
        private Button _importButton;
        private Label _statusLabel;
        private Label _profileCountLabel;
        private Label _lastImportLabel;
        private ProgressBar _progressBar;
        private ListBox _profileListBox;
        private Button _refreshProfilesButton;

        public SettingsControl(PitWallSettings settings, SQLiteProfileDatabase database)
        {
            _settings = settings;
            _database = database;

            InitializeComponent();
            LoadSettings();
            RefreshProfileList();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Main panel
            this.BackColor = Color.FromArgb(240, 240, 240);
            this.AutoScroll = true;
            this.Padding = new Padding(20);

            int yPos = 10;

            // Title
            var titleLabel = new Label
            {
                Text = "PitWall - Replay Import Settings",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Location = new Point(10, yPos),
                Size = new Size(600, 30),
                ForeColor = Color.FromArgb(0, 102, 204)
            };
            this.Controls.Add(titleLabel);
            yPos += 40;

            // Replay folder section
            var folderLabel = new Label
            {
                Text = "iRacing Replay Folder:",
                Font = new Font("Segoe UI", 10),
                Location = new Point(10, yPos),
                Size = new Size(200, 25)
            };
            this.Controls.Add(folderLabel);
            yPos += 30;

            _replayFolderTextBox = new TextBox
            {
                Location = new Point(10, yPos),
                Size = new Size(500, 25),
                Font = new Font("Segoe UI", 9)
            };
            this.Controls.Add(_replayFolderTextBox);

            _browseFolderButton = new Button
            {
                Text = "Browse...",
                Location = new Point(520, yPos - 2),
                Size = new Size(90, 28),
                Font = new Font("Segoe UI", 9)
            };
            _browseFolderButton.Click += BrowseFolderButton_Click;
            this.Controls.Add(_browseFolderButton);
            yPos += 40;

            // Import button
            _importButton = new Button
            {
                Text = "Import Historical Data",
                Location = new Point(10, yPos),
                Size = new Size(200, 35),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _importButton.FlatAppearance.BorderSize = 0;
            _importButton.Click += ImportButton_Click;
            this.Controls.Add(_importButton);
            yPos += 45;

            // Progress bar
            _progressBar = new ProgressBar
            {
                Location = new Point(10, yPos),
                Size = new Size(600, 25),
                Visible = false,
                Style = ProgressBarStyle.Continuous
            };
            this.Controls.Add(_progressBar);
            yPos += 35;

            // Status label
            _statusLabel = new Label
            {
                Text = "Ready",
                Font = new Font("Segoe UI", 9),
                Location = new Point(10, yPos),
                Size = new Size(600, 25),
                ForeColor = Color.Gray
            };
            this.Controls.Add(_statusLabel);
            yPos += 35;

            // Stats section
            var statsLabel = new Label
            {
                Text = "Import Statistics:",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Location = new Point(10, yPos),
                Size = new Size(200, 25)
            };
            this.Controls.Add(statsLabel);
            yPos += 30;

            _profileCountLabel = new Label
            {
                Text = "Profiles: 0",
                Font = new Font("Segoe UI", 9),
                Location = new Point(20, yPos),
                Size = new Size(300, 20)
            };
            this.Controls.Add(_profileCountLabel);
            yPos += 25;

            _lastImportLabel = new Label
            {
                Text = "Last import: Never",
                Font = new Font("Segoe UI", 9),
                Location = new Point(20, yPos),
                Size = new Size(300, 20)
            };
            this.Controls.Add(_lastImportLabel);
            yPos += 35;

            // Profile list section
            var profileLabel = new Label
            {
                Text = "Profiles:",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Location = new Point(10, yPos),
                Size = new Size(200, 25)
            };
            this.Controls.Add(profileLabel);

            _refreshProfilesButton = new Button
            {
                Text = "Refresh",
                Location = new Point(520, yPos - 2),
                Size = new Size(90, 28),
                Font = new Font("Segoe UI", 9)
            };
            _refreshProfilesButton.Click += (s, e) => RefreshProfileList();
            this.Controls.Add(_refreshProfilesButton);
            yPos += 30;

            _profileListBox = new ListBox
            {
                Location = new Point(10, yPos),
                Size = new Size(600, 200),
                Font = new Font("Consolas", 8)
            };
            this.Controls.Add(_profileListBox);
            yPos += 210;

            // Info label
            var infoLabel = new Label
            {
                Text = "Tip: Import historical replays to get personalized predictions from day 1.\n" +
                       "Recent sessions are automatically weighted higher than old sessions.",
                Font = new Font("Segoe UI", 8, FontStyle.Italic),
                Location = new Point(10, yPos),
                Size = new Size(600, 40),
                ForeColor = Color.Gray
            };
            this.Controls.Add(infoLabel);

            this.ResumeLayout();
        }

        private void LoadSettings()
        {
            _replayFolderTextBox.Text = _settings.ReplayFolderPath;

            if (_settings.LastImportDate.HasValue)
            {
                var daysAgo = (DateTime.Now - _settings.LastImportDate.Value).TotalDays;
                _lastImportLabel.Text = $"Last import: {daysAgo:F0} days ago ({_settings.LastImportDate.Value:yyyy-MM-dd})";
            }

            _profileCountLabel.Text = $"Profiles: {_settings.ProfilesImported} imported, {_settings.ReplaysProcessed} replays processed";
        }

        private void BrowseFolderButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select iRacing replay folder";
                dialog.SelectedPath = _replayFolderTextBox.Text;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _replayFolderTextBox.Text = dialog.SelectedPath;
                    _settings.ReplayFolderPath = dialog.SelectedPath;
                }
            }
        }

        private async void ImportButton_Click(object sender, EventArgs e)
        {
            if (_isProcessing)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_replayFolderTextBox.Text))
            {
                MessageBox.Show("Please select a replay folder first.", "No Folder Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!System.IO.Directory.Exists(_replayFolderTextBox.Text))
            {
                MessageBox.Show("Selected folder does not exist.", "Invalid Folder", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _isProcessing = true;
            _importButton.Enabled = false;
            _browseFolderButton.Enabled = false;
            _progressBar.Visible = true;
            _progressBar.Value = 0;
            _statusLabel.Text = "Scanning replay folder...";
            _statusLabel.ForeColor = Color.Blue;
            Logger.Info($"Import started. Folder={_replayFolderTextBox.Text}");

            try
            {
                _replayProcessor = new ReplayProcessor(_database);
                
                _replayProcessor.ProgressChanged += (s, args) =>
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        var percent = (args.CurrentIndex * 100) / args.TotalFiles;
                        _progressBar.Value = Math.Min(percent, 100);
                        _statusLabel.Text = $"Processing [{args.CurrentIndex}/{args.TotalFiles}]: {args.CurrentFile}";
                    });
                };

                _replayProcessor.ProcessingComplete += (s, args) =>
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        _progressBar.Visible = false;
                        _statusLabel.Text = args.Message;
                        _statusLabel.ForeColor = args.Success ? Color.Green : Color.Red;

                        _settings.LastImportDate = DateTime.Now;
                        _settings.ProfilesImported = args.ProfilesCreated;
                        _settings.ReplaysProcessed = args.ReplaysProcessed;

                        Logger.Info($"Import complete. Profiles={args.ProfilesCreated}, ReplaysProcessed={args.ReplaysProcessed}, ReplaysSkipped={args.ReplaysSkipped}");

                        LoadSettings();
                        RefreshProfileList();

                        _importButton.Enabled = true;
                        _browseFolderButton.Enabled = true;
                        _isProcessing = false;

                        MessageBox.Show(
                            $"Import complete!\n\n" +
                            $"Profiles created: {args.ProfilesCreated}\n" +
                            $"Replays processed: {args.ReplaysProcessed}\n" +
                            $"Replays skipped: {args.ReplaysSkipped}",
                            "Import Complete",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );
                    });
                };

                var driverName = Environment.UserName;
                await Task.Run(() => _replayProcessor.ProcessReplayLibraryAsync(_replayFolderTextBox.Text, driverName));
            }
            catch (Exception ex)
            {
                _progressBar.Visible = false;
                _statusLabel.Text = $"Error: {ex.Message}";
                _statusLabel.ForeColor = Color.Red;
                _importButton.Enabled = true;
                _browseFolderButton.Enabled = true;
                _isProcessing = false;

                Logger.Error($"Import failed: {ex}");

                MessageBox.Show($"Import failed: {ex.Message}", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshProfileList()
        {
            _profileListBox.Items.Clear();

            try
            {
                // Get all profiles from database
                var profiles = _database.GetRecentSessions(100).Result;
                
                if (profiles.Count == 0)
                {
                    _profileListBox.Items.Add("No profiles found. Import replays or race to build profiles.");
                    return;
                }

                // Group by track/car and show summary
                var grouped = profiles
                    .GroupBy(p => $"{p.TrackName} + {p.CarName}")
                    .OrderByDescending(g => g.Max(p => p.SessionDate))
                    .ToList();

                _profileListBox.Items.Add($"Found {grouped.Count} track/car combinations:");
                _profileListBox.Items.Add("");

                foreach (var group in grouped)
                {
                    var latest = group.OrderByDescending(p => p.SessionDate).First();
                    var sessionCount = group.Count();
                    var daysAgo = (DateTime.Now - latest.SessionDate).TotalDays;

                    var line = $"{latest.TrackName.PadRight(25)} + {latest.CarName.PadRight(25)} | " +
                               $"{sessionCount,2} sessions | Last: {daysAgo,3:F0} days ago";

                    if (daysAgo > 180)
                    {
                        line += " ⚠️ STALE";
                    }

                    _profileListBox.Items.Add(line);
                }
            }
            catch (Exception ex)
            {
                _profileListBox.Items.Add($"Error loading profiles: {ex.Message}");
            }
        }
    }
}
