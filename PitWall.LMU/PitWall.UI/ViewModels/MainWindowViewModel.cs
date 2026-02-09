using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using PitWall.UI.Models;
using PitWall.UI.Services;

namespace PitWall.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
	private readonly IRecommendationClient _recommendationClient;
	private readonly ITelemetryStreamClient _telemetryStreamClient;
	private CancellationTokenSource? _cts;

	public MainWindowViewModel()
		: this(new NullRecommendationClient(), new NullTelemetryStreamClient())
	{
	}

	public MainWindowViewModel(IRecommendationClient recommendationClient, ITelemetryStreamClient telemetryStreamClient)
	{
		_recommendationClient = recommendationClient;
		_telemetryStreamClient = telemetryStreamClient;
	}

	[ObservableProperty]
	private string fuelLiters = "-- L";

	[ObservableProperty]
	private string fuelLaps = "-- LAPS";

	[ObservableProperty]
	private string speedDisplay = "-- KPH";

	[ObservableProperty]
	private string tiresLine1 = "FL --  --  |  FR --  --";

	[ObservableProperty]
	private string tiresLine2 = "RL --  --  |  RR --  --";

	[ObservableProperty]
	private string strategyMessage = "Awaiting strategy...";

	[ObservableProperty]
	private string strategyConfidence = "CONF --";

	public async Task StartAsync(string sessionId, CancellationToken cancellationToken)
	{
		_cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

		_ = Task.Run(() =>
			_telemetryStreamClient.ConnectAsync(
				int.TryParse(sessionId, out var parsed) ? parsed : 1,
				UpdateTelemetry,
				_cts.Token),
			_cts.Token);

		_ = Task.Run(() => PollRecommendationsAsync(sessionId, _cts.Token), _cts.Token);
	}

	public void Stop()
	{
		_cts?.Cancel();
		_cts?.Dispose();
		_cts = null;
	}

	public void UpdateTelemetry(TelemetrySampleDto telemetry)
	{
		FuelLiters = $"{telemetry.FuelLiters:0.0} L";
		SpeedDisplay = $"{telemetry.SpeedKph:0.0} KPH";

		if (telemetry.TyreTempsC.Length >= 4)
		{
			TiresLine1 = $"FL {telemetry.TyreTempsC[0]:0}C  --  |  FR {telemetry.TyreTempsC[1]:0}C  --";
			TiresLine2 = $"RL {telemetry.TyreTempsC[2]:0}C  --  |  RR {telemetry.TyreTempsC[3]:0}C  --";
		}
		else
		{
			TiresLine1 = "FL --  --  |  FR --  --";
			TiresLine2 = "RL --  --  |  RR --  --";
		}
	}

	public void UpdateRecommendation(RecommendationDto recommendation)
	{
		StrategyMessage = string.IsNullOrWhiteSpace(recommendation.Recommendation)
			? "Awaiting strategy..."
			: recommendation.Recommendation;

		StrategyConfidence = $"CONF {recommendation.Confidence:0.00}";
	}

	private async Task PollRecommendationsAsync(string sessionId, CancellationToken cancellationToken)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
		while (await timer.WaitForNextTickAsync(cancellationToken))
		{
			var recommendation = await _recommendationClient.GetRecommendationAsync(sessionId, cancellationToken);
			UpdateRecommendation(recommendation);
		}
	}

	private sealed class NullRecommendationClient : IRecommendationClient
	{
		public Task<RecommendationDto> GetRecommendationAsync(string sessionId, CancellationToken cancellationToken)
		{
			return Task.FromResult(new RecommendationDto());
		}
	}

	private sealed class NullTelemetryStreamClient : ITelemetryStreamClient
	{
		public Task ConnectAsync(int sessionId, Action<TelemetrySampleDto> onMessage, CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}
}
