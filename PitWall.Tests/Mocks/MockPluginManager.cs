using System.Collections.Generic;
using GameReaderCommon;
using SimHub.Plugins;

namespace PitWall.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of PluginManager for testing
    /// </summary>
    public class MockPluginManager : PluginManager
    {
        private readonly Dictionary<string, object?> _properties = new();

        public void SetPropertyValue(string propertyName, object? value)
        {
            _properties[propertyName] = value;
        }

        public new object? GetPropertyValue(string propertyName)
        {
            return _properties.TryGetValue(propertyName, out var value) ? value : null;
        }

        public new string? GameName { get; set; }
    }
}
