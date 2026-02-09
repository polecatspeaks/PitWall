using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PitWall.Core;
using PitWall.Models;
using PitWall.Storage;
using PitWall.Storage.Telemetry;
using PitWall.Telemetry;

namespace PitWall.UI
{
    /// <summary>
    /// Settings control for PitWall plugin
    /// Allows users to import historical IBT telemetry data
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        private readonly PitWallSettings _settings;
        private readonly SQLiteProfileDatabase _database;
        private readonly IbtImporter _ibtImporter;
        private readonly ISessionRepository _sessionRepository;
        private readonly ProfileGenerator _profileGenerator;
        private bool _isProcessing;

        private TextBox _replayFolderTextBox = null!;
        private Button _browseFolderButton = null!;
        private Button _importButton = null!;
        private Label _statusLabel = null!;
        private Label _profileCountLabel = null!;
        private Label _lastImportLabel = null!;
        private ProgressBar _progressBar = null!;
        private ListBox _profileListBox = null!;
        private Button _refreshProfilesButton = null!;
        private Button _viewDetailsButton = null!;
        private LinkLabel _viewDetailsLink = null!;
        private Label _profileDetailsBodyLabel = null!;

        private List<DriverProfile> _profiles = new List<DriverProfile>();

        public SettingsControl(PitWallSettings settings, SQLiteProfileDatabase database)
        {
            _settings = settings;
            _database = database;
            _ibtImporter = new IbtImporter();
            // Unified database path for telemetry and profiles
            var dbPath = DatabasePaths.GetDatabasePath();
            _sessionRepository = new SQLiteSessionRepository(dbPath);
            _profileGenerator = new ProfileGenerator(_sessionRepository, database);

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

            _viewDetailsLink = new LinkLabel
            {
                Text = "View details",
                Location = new Point(400, yPos),
                Size = new Size(90, 22),
                Font = new Font("Segoe UI", 9)
            };
            _viewDetailsLink.LinkClicked += (s, e) => ShowSelectedProfileDetails();
            this.Controls.Add(_viewDetailsLink);

            _refreshProfilesButton = new Button
            {
                Text = "Refresh",
                Location = new Point(500, yPos - 2),
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
            _profileListBox.SelectedIndexChanged += ProfileListBox_SelectedIndexChanged;
            _profileListBox.DoubleClick += ProfileListBox_DoubleClick;
            this.Controls.Add(_profileListBox);
            yPos += 205;

            // Profile details panel directly under list to stay in viewport
            var detailsPanel = new Panel
            {
                Location = new Point(10, yPos),
                Size = new Size(600, 90),
                BackColor = Color.FromArgb(245, 245, 245),
                BorderStyle = BorderStyle.FixedSingle
            };

            var detailsHeader = new Label
            {
                Text = "Profile Details",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Location = new Point(10, 8),
                Size = new Size(200, 20)
            };
            detailsPanel.Controls.Add(detailsHeader);

            _viewDetailsButton = new Button
            {
                Text = "View Details",
                Location = new Point(detailsPanel.Width - 110, 8),
                Size = new Size(100, 28),
                Font = new Font("Segoe UI", 9)
            };
            _viewDetailsButton.Click += (s, e) => ShowSelectedProfileDetails();
            detailsPanel.Controls.Add(_viewDetailsButton);

            _profileDetailsBodyLabel = new Label
            {
                Text = "Select a profile to view details",
                Font = new Font("Consolas", 9),
                Location = new Point(10, 38),
                Size = new Size(detailsPanel.Width - 20, 45),
                BackColor = Color.FromArgb(255, 255, 224)
            };
            detailsPanel.Controls.Add(_profileDetailsBodyLabel);

            this.Controls.Add(detailsPanel);
            yPos += detailsPanel.Height + 10;

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
                MessageBox.Show("Please select an IBT telemetry folder first.", "No Folder Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            _statusLabel.Text = "Scanning for IBT files...";
            _statusLabel.ForeColor = Color.Blue;
            Logger.Info($"IBT import started. Folder={_replayFolderTextBox.Text}");

            try
            {
                // Phase 5A: Use real IbtImporter
                var ibtFiles = Directory.GetFiles(_replayFolderTextBox.Text, "*.ibt", SearchOption.TopDirectoryOnly);

                if (ibtFiles.Length == 0)
                {
                    MessageBox.Show(
                        $"No .ibt files found in:\n{_replayFolderTextBox.Text}\n\n" +
                        "Make sure you've selected the correct iRacing telemetry folder.\n" +
                        "Default location: Documents\\iRacing\\telemetry",
                        "No IBT Files Found",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    _progressBar.Visible = false;
                    _statusLabel.Text = "No IBT files found";
                    _statusLabel.ForeColor = Color.Orange;
                    _importButton.Enabled = true;
                    _browseFolderButton.Enabled = true;
                    _isProcessing = false;
                    return;
                }

                _statusLabel.Text = $"Found {ibtFiles.Length} IBT files. Importing...";
                Logger.Info($"Found {ibtFiles.Length} IBT files to import");

                int imported = 0;
                int failed = 0;

                for (int i = 0; i < ibtFiles.Length; i++)
                {
                    var file = ibtFiles[i];
                    try
                    {
                        _statusLabel.Text = $"Importing {i + 1}/{ibtFiles.Length}: {Path.GetFileName(file)}";
                        _progressBar.Value = (int)((i + 1) * 100.0 / ibtFiles.Length);

                        // Import IBT file
                        var session = await _ibtImporter.ImportIBTFileAsync(file);

                        // Save to database
                        await _sessionRepository.SaveSessionAsync(session);

                        imported++;
                        Logger.Info($"Imported: {Path.GetFileName(file)} - {session.Laps.Count} laps, {session.RawSamples.Count} samples");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Logger.Error($"Failed to import {file}: {ex.Message}");
                    }
                }

                // Generate profiles from imported telemetry
                _statusLabel.Text = $"Generating profiles from {imported} sessions...";
                _statusLabel.ForeColor = Color.Blue;
                Logger.Info("Starting profile generation");

                int profilesGenerated = 0;
                try
                {
                    profilesGenerated = await _profileGenerator.GenerateProfilesFromImportedSessionsAsync();
                    Logger.Info($"Generated {profilesGenerated} profiles");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Profile generation failed: {ex.Message}");
                }

                _progressBar.Visible = false;
                _statusLabel.Text = $"Import complete: {imported} sessions, {profilesGenerated} profiles";
                _statusLabel.ForeColor = imported > 0 ? Color.Green : Color.Red;

                _settings.LastImportDate = DateTime.Now;
                LoadSettings();
                RefreshProfileList();

                _importButton.Enabled = true;
                _browseFolderButton.Enabled = true;
                _isProcessing = false;

                MessageBox.Show(
                    $"Import Complete!\n\n" +
                    $"Successfully imported: {imported} sessions\n" +
                                        $"Profiles generated: {profilesGenerated}\n" +
                    $"Failed: {failed}\n\n" +
                    $"Each session includes:\n" +
                    $"• Session metadata (driver, car, track)\n" +
                    $"• Lap-by-lap statistics\n" +
                    $"• 60Hz telemetry samples\n\n" +
                                        $"Profiles aggregated by driver/car/track combination.\n\n" +
                    $"Data saved to: pitwall_telemetry.db",
                    "Import Complete",
                    MessageBoxButtons.OK,
                    imported > 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning
                );
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
            _profiles.Clear();

            try
            {
                // Get all driver profiles from database
                var profiles = _database.GetProfiles(100).Result;
                _profiles = profiles.OrderByDescending(p => p.LastUpdated).ToList();

                if (_profiles.Count == 0)
                {
                    _profileListBox.Items.Add("No profiles found. Import replays or race to build profiles.");
                    UpdateProfileDetails(null);
                    return;
                }

                foreach (var profile in _profiles)
                {
                    _profileListBox.Items.Add(new ProfileListItem(profile));
                }

                // Default to first profile selection to show details
                _profileListBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                _profileListBox.Items.Add($"Error loading profiles: {ex.Message}");
                UpdateProfileDetails(null);
            }
        }

        private void ProfileListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selected = _profileListBox.SelectedItem as ProfileListItem;
            UpdateProfileDetails(selected?.Profile);
        }

        private void ProfileListBox_DoubleClick(object sender, EventArgs e)
        {
            ShowSelectedProfileDetails();
        }

        private void ShowSelectedProfileDetails()
        {
            var selected = _profileListBox.SelectedItem as ProfileListItem;
            if (selected?.Profile == null)
            {
                return;
            }

            MessageBox.Show(
                ProfileDisplayFormatter.FormatDetails(selected.Profile),
                "Profile Details",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void UpdateProfileDetails(DriverProfile? profile)
        {
            _profileDetailsBodyLabel.Text = profile == null
                ? "Select a profile to view details"
                : ProfileDisplayFormatter.FormatDetails(profile);
        }

        private sealed class ProfileListItem
        {
            public DriverProfile Profile { get; }

            public ProfileListItem(DriverProfile profile)
            {
                Profile = profile;
            }

            public override string ToString()
            {
                var daysAgo = (DateTime.Now - Profile.LastUpdated).TotalDays;
                string stale = Profile.IsStale ? " STALE" : string.Empty;
                return $"{Profile.DriverName.PadRight(20)} | {Profile.TrackName.PadRight(20)} | {Profile.CarName.PadRight(20)} | Fuel/lap: {Profile.AverageFuelPerLap:F2} | Laps: {Profile.SessionsCompleted,3} | Updated: {daysAgo,3:F0} days ago{stale}";
            }
        }
    }
}
