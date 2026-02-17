using PitWall.UI.Models;
using PitWall.UI.ViewModels;
using Xunit;

namespace PitWall.UI.Tests;

/// <summary>
/// Additional StrategyViewModel tests for uncovered edge cases:
/// OverrideStrategy, ToggleFuelSaveMode, SelectAlternativeStrategy,
/// and CalculatePitWindow validation.
/// </summary>
public class StrategyViewModelAdditionalTests
{
    [Fact]
    public void OverrideStrategyCommand_ExecutesWithoutError()
    {
        var vm = new StrategyViewModel();
        vm.OverrideStrategyCommand.Execute(null);
        // Method body is a TODO placeholder; verifying no exception
        Assert.NotNull(vm);
    }

    [Fact]
    public void ToggleFuelSaveModeCommand_TogglesFuelSaveMode()
    {
        var vm = new StrategyViewModel();
        Assert.False(vm.FuelSaveModeActive);

        vm.ToggleFuelSaveModeCommand.Execute(null);
        Assert.True(vm.FuelSaveModeActive);

        vm.ToggleFuelSaveModeCommand.Execute(null);
        Assert.False(vm.FuelSaveModeActive);
    }

    [Fact]
    public void SelectAlternativeStrategyCommand_ExecutesWithoutError()
    {
        var vm = new StrategyViewModel();
        var alt = new StrategyAlternative { Description = "Test" };
        vm.SelectAlternativeStrategyCommand.Execute(alt);
        Assert.NotNull(vm);
    }

    [Fact]
    public void CalculatePitWindow_ZeroFuelPerLap_ReturnsEarly()
    {
        var vm = new StrategyViewModel();
        vm.UpdateStintStatus(50, 20, 5, 5);

        vm.CalculatePitWindow(0, 2.0, 100);

        Assert.Equal(0, vm.OptimalPitLapStart);
    }

    [Fact]
    public void CalculatePitWindow_NegativeTireWear_ReturnsEarly()
    {
        var vm = new StrategyViewModel();
        vm.UpdateStintStatus(50, 20, 5, 5);

        vm.CalculatePitWindow(3.0, -1.0, 100);

        Assert.Equal(0, vm.OptimalPitLapStart);
    }

    [Fact]
    public void CalculatePitWindow_ZeroTankCapacity_ReturnsEarly()
    {
        var vm = new StrategyViewModel();
        vm.UpdateStintStatus(50, 20, 5, 5);

        vm.CalculatePitWindow(3.0, 2.0, 0);

        Assert.Equal(0, vm.OptimalPitLapStart);
    }
}
