using GameReaderCommon;
using PitWall.Core;
using SimHub.Plugins;

namespace PitWall
{
    /// <summary>
    /// PitWall - AI-powered race engineer plugin for SimHub
    /// </summary>
    [PluginDescription("AI race engineer providing real-time strategy recommendations")]
    [PluginAuthor("PitWall Team")]
    [PluginName("Pit Wall Race Engineer")]
    public class PitWallPlugin : IPlugin, IDataPlugin
    {
        public PluginManager? PluginManager { get; set; }

        private ITelemetryProvider? _telemetryProvider;
        private StrategyEngine? _strategyEngine;
        private FuelStrategy? _fuelStrategy;
        private AudioMessageQueue? _audioQueue;
        private AudioPlayer? _audioPlayer;

        /// <summary>
        /// Plugin display name
        /// </summary>
        public string Name => "Pit Wall Race Engineer";

        /// <summary>
        /// Called when the plugin is initialized
        /// </summary>
        public void Init(PluginManager pluginManager)
        {
            PluginManager = pluginManager;

            // Phase 1: initialize fuel-focused strategy stack
            _fuelStrategy = new FuelStrategy();
            IPluginPropertyProvider propertyProvider = pluginManager as IPluginPropertyProvider
                ?? new PluginManagerPropertyProvider(pluginManager);
            _telemetryProvider = new SimHubTelemetryProvider(propertyProvider);
            _strategyEngine = new StrategyEngine(_fuelStrategy);
            _audioQueue = new AudioMessageQueue();
            _audioPlayer = new AudioPlayer(_audioQueue);
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
            // TODO: Cleanup resources
            // Log: Plugin shutting down
            _audioQueue?.Clear();
        }
    }
}
