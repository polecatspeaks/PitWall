namespace PitWall.Core
{
    /// <summary>
    /// Abstraction for accessing SimHub PluginManager property bag (test-friendly).
    /// </summary>
    public interface IPluginPropertyProvider
    {
        object? GetPropertyValue(string propertyName);
        string? GameName { get; }
    }
}
