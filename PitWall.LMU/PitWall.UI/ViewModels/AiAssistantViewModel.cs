using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PitWall.UI.Models;
using PitWall.UI.Services;

namespace PitWall.UI.ViewModels;

/// <summary>
/// ViewModel for the AI Race Engineer assistant panel with chat history,
/// quick queries, and race context display.
/// </summary>
public partial class AiAssistantViewModel : ViewModelBase
{
	private readonly IAgentQueryClient _agentQueryClient;

	public AiAssistantViewModel()
		: this(new NullAgentQueryClient())
	{
	}

	public AiAssistantViewModel(IAgentQueryClient agentQueryClient)
	{
		_agentQueryClient = agentQueryClient;
	}

	[ObservableProperty]
	private string inputText = string.Empty;

	[ObservableProperty]
	private bool isProcessing;

	[ObservableProperty]
	private bool showContext;

	[ObservableProperty]
	private string raceContext = string.Empty;

	[ObservableProperty]
	private string statusMessage = "Ready";

	public ObservableCollection<AiMessageViewModel> Messages { get; } = new();

	public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

	partial void OnStatusMessageChanged(string value)
	{
		OnPropertyChanged(nameof(HasStatus));
	}

	[RelayCommand]
	private async Task SendQueryAsync()
	{
		if (string.IsNullOrWhiteSpace(InputText) || IsProcessing)
		{
			return;
		}

		var userMessage = new AiMessageViewModel
		{
			Role = "User",
			Text = InputText.Trim(),
			Timestamp = DateTime.Now
		};

		Messages.Add(userMessage);
		var query = InputText;
		InputText = string.Empty;
		IsProcessing = true;
		StatusMessage = "Sending query...";

		try
		{
			var response = await _agentQueryClient.SendQueryAsync(query, CancellationToken.None);
			var responseText = string.IsNullOrWhiteSpace(response.Answer)
				? "Agent response unavailable. Check Settings."
				: response.Answer;
			
			Messages.Add(new AiMessageViewModel
			{
				Role = "Assistant",
				Text = responseText,
				Source = response.Source,
				Confidence = response.Confidence,
				Timestamp = DateTime.Now,
				Context = response.Context
			});
			StatusMessage = response.Success
				? BuildStatusMessage(response)
				: "Agent response failed. Check Settings.";
		}
		catch (Exception ex)
		{
			Messages.Add(new AiMessageViewModel
			{
				Role = "System",
				Text = $"Error: {ex.Message}",
				Timestamp = DateTime.Now
			});
			StatusMessage = $"Error: {ex.Message}";
		}
		finally
		{
			IsProcessing = false;
		}
	}

	private static string BuildStatusMessage(AgentResponseDto response)
	{
		var source = string.IsNullOrWhiteSpace(response.Source) ? "Unknown" : response.Source;
		return response.Confidence > 0
			? $"Response received ({source}, {response.Confidence:P0})"
			: $"Response received ({source})";
	}

	[RelayCommand]
	private async Task SendQuickQueryAsync(string query)
	{
		InputText = query;
		await SendQueryAsync();
	}

	[RelayCommand]
	private void ToggleContextDisplay()
	{
		ShowContext = !ShowContext;
	}

	[RelayCommand]
	private void ClearHistory()
	{
		Messages.Clear();
	}

	public void UpdateRaceContext(string context)
	{
		RaceContext = context;
	}
}

public partial class AiMessageViewModel : ObservableObject
{
	[ObservableProperty]
	private string role = string.Empty;

	[ObservableProperty]
	private string text = string.Empty;

	[ObservableProperty]
	private string? source;

	[ObservableProperty]
	private double confidence;

	[ObservableProperty]
	private DateTime timestamp;

	[ObservableProperty]
	private string? context;

	[ObservableProperty]
	private bool isContextExpanded;

	public string DisplayTimestamp => Timestamp.ToString("HH:mm:ss");

	public string DisplaySource => Source ?? "Unknown";

	public string ConfidenceDisplay => Confidence > 0 ? $"{Confidence:P0}" : string.Empty;

	public bool HasConfidence => Confidence > 0;

	public bool IsUserMessage => Role == "User";

	public bool IsAssistantMessage => Role == "Assistant";

	public bool HasContext => !string.IsNullOrWhiteSpace(Context);
}

// Null implementation for design-time support
internal sealed class NullAgentQueryClient : IAgentQueryClient
{
	public Task<AgentResponseDto> SendQueryAsync(string query, CancellationToken cancellationToken)
	{
		return Task.FromResult(new AgentResponseDto
		{
			Answer = "Design-time response",
			Source = "Null",
			Success = true
		});
	}
}
