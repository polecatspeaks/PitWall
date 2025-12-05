using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameReaderCommon;
using PitWall.Core;
using PitWall.Models;
using PitWall.Storage;
using PitWall.UI;
using SimHub.Plugins;

namespace PitWall
{
    /// <summary>
    /// PitWall - AI-powered race engineer plugin for SimHub
    /// </summary>
    [PluginDescription("AI race engineer providing real-time strategy recommendations")]
    [PluginAuthor("PitWall Team")]
    [PluginName("Pit Wall Race Engineer")]
    public class PitWallPlugin : IPlugin, IDataPlugin, IWPFSettingsV2
    {
        public PluginManager? PluginManager { get; set; }

        private ITelemetryProvider? _telemetryProvider;
        private StrategyEngine? _strategyEngine;
        private FuelStrategy? _fuelStrategy;
        private AudioMessageQueue? _audioQueue;
        private AudioPlayer? _audioPlayer;
        private TyreDegradation? _tyreDegradation;
        private ProfileAnalyzer? _profileAnalyzer;
        private IProfileDatabase? _profileDatabase;
        private PitWallSettings? _settings;
        private SettingsControl? _settingsControl;
        private List<LapData> _sessionLaps = new List<LapData>();
        private int _lastLapNumber = 0;

        /// <summary>
        /// Plugin display name
        /// </summary>
        public string Name => "Pit Wall Race Engineer";

        /// <summary>
        /// IWPFSettingsV2: Left menu title for settings panel
        /// </summary>
        public string LeftMenuTitle => "Pit Wall";

        /// <summary>
        /// IWPFSettingsV2: Icon for settings panel menu (null = default)
        /// </summary>
        public ImageSource PictureIcon => null;

        /// <summary>
        /// IWPFSettingsV2: Settings panel control
        /// </summary>
        public Control GetSettingsControl() => new UserControl();

        /// <summary>
        /// IWPFSettings: GetWPFSettingsControl (legacy method, required by base interface)
        /// </summary>
        public Control GetWPFSettingsControl(PluginManager pluginManager) => GetSettingsControl();

        /// <summary>
        /// IWPFSettingsV2: Help/description text
        /// </summary>
        public string GetInfoText() => 
            "Pit Wall Race Engineer - AI-powered race strategy assistant\n" +
            "\n" +
            "Features:\n" +
            "• Real-time fuel and tyre strategy recommendations\n" +
            "• AI learning from historical replay data\n" +
            "• Personalized driver profiles per track/car\n" +
            "• Audio alerts for critical pit strategy moments\n" +
            "\n" +
            "Getting Started:\n" +
            "1. Click 'Browse' to select your iRacing replay folder\n" +
            "2. Click 'Import Historical Data' to seed profiles\n" +
            "3. Data will update automatically during races\n" +
            "\n" +
            "iRacing Replays Location: Documents\\iRacing\\replays";

        /// <summary>
        /// IWPFSettingsV2: Plugin header text for settings panel
        /// </summary>
        public string GetHeaderText() => "Pit Wall Race Engineer";

        /// <summary>
        /// Called when the plugin is initialized
        /// </summary>
        public void Init(PluginManager pluginManager)
        {
            PluginManager = pluginManager;

            // Phase 5A.1: Load settings (only in SimHub environment, not tests)
            try
            {
                _settings = this.ReadCommonSettings("PitWallSettings", () => new PitWallSettings());
            }
            catch
            {
                // ReadCommonSettings requires Newtonsoft.Json which is only available in SimHub
                _settings = new PitWallSettings();
            }

            // Phase 1-5: initialize strategy stack with profile learning
            _fuelStrategy = new FuelStrategy();
            _tyreDegradation = new TyreDegradation();
            _profileAnalyzer = new ProfileAnalyzer();
            _profileDatabase = new SQLiteProfileDatabase();
            
            IPluginPropertyProvider propertyProvider = pluginManager as IPluginPropertyProvider
                ?? new PluginManagerPropertyProvider(pluginManager);
            _telemetryProvider = new SimHubTelemetryProvider(propertyProvider);
            _strategyEngine = new StrategyEngine(_fuelStrategy, _tyreDegradation, new TrafficAnalyzer(), _profileDatabase);
            _audioQueue = new AudioMessageQueue();
            _audioPlayer = new AudioPlayer(_audioQueue);

            // Load profile asynchronously (non-blocking)
            // Driver/track/car info will be available after first DataUpdate
            
            // Initialize settings UI
            if (_profileDatabase != null && _settings != null)
            {
                _settingsControl = new SettingsControl(_settings, (SQLiteProfileDatabase)_profileDatabase);
            }
        }

        /// <summary>
        /// Called at high frequency (~100Hz) with updated game data
        /// </summary>
        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            if (_telemetryProvider == null || _strategyEngine == null)
            {
                return; // Not initialized
            }

            var telemetry = _telemetryProvider.GetCurrentTelemetry();

            // Load profile once when driver/track/car info is available
            if (_lastLapNumber == 0 && !string.IsNullOrEmpty(telemetry.TrackName) && !string.IsNullOrEmpty(telemetry.CarName))
            {
                // Assume driver name is system username for now (could be from SimHub property later)
                string driverName = Environment.UserName;
                _strategyEngine.LoadProfile(driverName, telemetry.TrackName, telemetry.CarName).Wait();
            }

            // Capture lap data for session analysis
            if (telemetry.CurrentLap > _lastLapNumber && _lastLapNumber > 0 && telemetry.IsLapValid)
            {
                double fuelUsed = _fuelStrategy?.GetAverageFuelPerLap() ?? 0;
                double tyreAvg = (_tyreDegradation?.GetLatestWear(TyrePosition.FrontLeft) +
                                  _tyreDegradation?.GetLatestWear(TyrePosition.FrontRight) +
                                  _tyreDegradation?.GetLatestWear(TyrePosition.RearLeft) +
                                  _tyreDegradation?.GetLatestWear(TyrePosition.RearRight)) / 4.0 ?? 0;

                _sessionLaps.Add(new LapData
                {
                    LapNumber = _lastLapNumber,
                    LapTime = TimeSpan.FromSeconds(telemetry.LastLapTime),
                    FuelUsed = fuelUsed,
                    FuelRemaining = telemetry.FuelRemaining,
                    IsValid = telemetry.IsLapValid,
                    IsClear = true, // TODO: Detect traffic from opponents
                    TyreWearAverage = tyreAvg,
                    Timestamp = DateTime.Now
                });
            }
            _lastLapNumber = telemetry.CurrentLap;

            // Record lap progression data
            _strategyEngine.RecordLap(telemetry);

            var recommendation = _strategyEngine.GetRecommendation(telemetry);
            if (_audioQueue != null && recommendation != null && !string.IsNullOrEmpty(recommendation.Message))
            {
                _audioQueue.Enqueue(recommendation);
                _audioPlayer?.PlayNext();
            }

            // NOTE: Keep this loop <10ms; do not allocate excessively
        }

        /// <summary>
        /// Called when the plugin is being unloaded
        /// </summary>
        public void End(PluginManager pluginManager)
        {
            // Phase 5: Analyze and save session profile
            if (_profileAnalyzer != null && _profileDatabase != null && _sessionLaps.Count > 0 && _telemetryProvider != null)
            {
                var telemetry = _telemetryProvider.GetCurrentTelemetry();
                if (!string.IsNullOrEmpty(telemetry.TrackName) && !string.IsNullOrEmpty(telemetry.CarName))
                {
                    var sessionData = new SessionData
                    {
                        DriverName = Environment.UserName,
                        TrackName = telemetry.TrackName,
                        CarName = telemetry.CarName,
                        SessionType = "Race", // TODO: Detect session type
                        SessionDate = DateTime.Now,
                        Laps = _sessionLaps,
                        TotalFuelUsed = _sessionLaps.Sum(l => l.FuelUsed),
                        SessionDuration = _sessionLaps.Count > 0 
                            ? TimeSpan.FromTicks(_sessionLaps.Sum(l => l.LapTime.Ticks))
                            : TimeSpan.Zero
                    };

                    try
                    {
                        // Analyze session and create/merge profile
                        var newProfile = _profileAnalyzer.AnalyzeSession(sessionData);
                        var existingProfile = _profileDatabase.GetProfile(newProfile.DriverName, newProfile.TrackName, newProfile.CarName).Result;

                        DriverProfile profileToSave = existingProfile != null
                            ? _profileAnalyzer.MergeProfiles(existingProfile, newProfile)
                            : newProfile;

                        _profileDatabase.SaveProfile(profileToSave).Wait();
                        _profileDatabase.SaveSession(sessionData).Wait();
                    }
                    catch
                    {
                        // Silently fail profile saving to avoid crashing SimHub
                        // TODO: Log error
                    }
                }
            }

            _audioQueue?.Clear();
            _sessionLaps.Clear();
            _lastLapNumber = 0;

            // Phase 5A.1: Save settings (only in SimHub environment, not tests)
            try
            {
                if (_settings != null)
                {
                    this.SaveCommonSettings("PitWallSettings", _settings);
                }
            }
            catch
            {
                // SaveCommonSettings requires Newtonsoft.Json which is only available in SimHub
            }
        }
    }
}
