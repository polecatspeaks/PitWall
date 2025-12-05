using PitWall.Core;
using SimHub.Plugins;

namespace PitWall
{
    /// <summary>
    /// Adapter over SimHub PluginManager implementing test-friendly property provider.
    /// </summary>
    public class PluginManagerPropertyProvider : IPluginPropertyProvider
    {
        private readonly PluginManager _pluginManager;

        public PluginManagerPropertyProvider(PluginManager pluginManager)
        {
            _pluginManager = pluginManager;
        }

        public object? GetPropertyValue(string propertyName)
        {
            return _pluginManager.GetPropertyValue(propertyName);
        }

        public string? GameName => _pluginManager.GameName;
    }
}
