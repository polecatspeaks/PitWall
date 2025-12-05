using GameReaderCommon;
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
            
            // TODO: Initialize strategy engine, audio system, etc.
            // Log: Plugin initialized v0.1.0
        }

        /// <summary>
        /// Called when the plugin is being unloaded
        /// </summary>
        public void End(PluginManager pluginManager)
        {
            // TODO: Cleanup resources
            // Log: Plugin shutting down
        }

        /// <summary>
        /// Called at high frequency (~100Hz) with updated game data
        /// </summary>
        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            // TODO: Process telemetry and generate recommendations
            // This is called very frequently - must be fast (<10ms)
        }
    }
}
