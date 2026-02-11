using System;
using System.Diagnostics;
using System.IO;

namespace PitWall.UI.Services;

public interface IStackRestartService
{
	StackRestartResult Restart();
}

public sealed record StackRestartResult(bool Success, string Message);

public sealed class StackRestartService : IStackRestartService
{
	private const int DefaultApiPort = 5236;
	private const int DefaultAgentPort = 5139;
	private const string ScriptRelativePath = "Tools\\RestartPitWallStack.ps1";
	private const string SolutionFileName = "PitWall.LMU.sln";

	public StackRestartResult Restart()
	{
		try
		{
			var baseDirectory = AppContext.BaseDirectory;
			var projectRoot = FindSolutionRoot(baseDirectory);
			if (string.IsNullOrWhiteSpace(projectRoot))
			{
				return new StackRestartResult(false, "Failed to locate PitWall.LMU.sln.");
			}

			var scriptPath = Path.Combine(baseDirectory, ScriptRelativePath);
			if (!File.Exists(scriptPath))
			{
				return new StackRestartResult(false, $"Restart script not found: {scriptPath}");
			}

			var apiBase = ResolveBaseUri(Environment.GetEnvironmentVariable("PITWALL_API_BASE"), DefaultApiPort);
			var agentBase = ResolveBaseUri(Environment.GetEnvironmentVariable("PITWALL_AGENT_BASE"), DefaultAgentPort);

			var startInfo = new ProcessStartInfo
			{
				FileName = "powershell",
				UseShellExecute = false,
				CreateNoWindow = true
			};
			startInfo.ArgumentList.Add("-NoProfile");
			startInfo.ArgumentList.Add("-ExecutionPolicy");
			startInfo.ArgumentList.Add("Bypass");
			startInfo.ArgumentList.Add("-File");
			startInfo.ArgumentList.Add(scriptPath);
			startInfo.ArgumentList.Add("-ProjectRoot");
			startInfo.ArgumentList.Add(projectRoot);
			startInfo.ArgumentList.Add("-ApiBase");
			startInfo.ArgumentList.Add(apiBase);
			startInfo.ArgumentList.Add("-AgentBase");
			startInfo.ArgumentList.Add(agentBase);

			Process.Start(startInfo);
			return new StackRestartResult(true, "Restarting PitWall stack.");
		}
		catch (Exception ex)
		{
			return new StackRestartResult(false, ex.Message);
		}
	}

	private static string? FindSolutionRoot(string baseDirectory)
	{
		var directory = new DirectoryInfo(baseDirectory);
		while (directory != null)
		{
			var candidate = Path.Combine(directory.FullName, SolutionFileName);
			if (File.Exists(candidate))
			{
				return directory.FullName;
			}
			directory = directory.Parent;
		}

		return null;
	}

	private static string ResolveBaseUri(string? envValue, int defaultPort)
	{
		if (!string.IsNullOrWhiteSpace(envValue) && Uri.TryCreate(envValue, UriKind.Absolute, out var uri))
		{
			return uri.GetLeftPart(UriPartial.Authority);
		}

		return $"http://localhost:{defaultPort}";
	}
}

internal sealed class NullStackRestartService : IStackRestartService
{
	public StackRestartResult Restart()
	{
		return new StackRestartResult(false, "Stack restart is unavailable.");
	}
}
