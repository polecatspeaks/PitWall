using System;
using System.Collections.Generic;
using System.Linq;
using PitWall.UI.Models;

namespace PitWall.UI.Services;

/// <summary>
/// Circular buffer for storing telemetry data with lap-based indexing.
/// Maintains a rolling window of the most recent telemetry samples.
/// </summary>
public class TelemetryBuffer
{
	private const double DefaultLapDurationSeconds = 90.0;
	private const int DefaultLapSampleCount = 8000;
	private readonly TelemetrySampleDto[] _buffer;
	private readonly int _capacity;
	private int _writeIndex;
	private int _count;
	private readonly object _lock = new();

	public TelemetryBuffer(int capacity = 10000)
	{
		_capacity = capacity;
		_buffer = new TelemetrySampleDto[capacity];
		_writeIndex = 0;
		_count = 0;
	}

	public int Count
	{
		get
		{
			lock (_lock)
			{
				return _count;
			}
		}
	}

	public void Add(TelemetrySampleDto sample)
	{
		lock (_lock)
		{
			_buffer[_writeIndex] = sample;
			_writeIndex = (_writeIndex + 1) % _capacity;
			if (_count < _capacity)
			{
				_count++;
			}
		}
	}

	public TelemetrySampleDto[] GetAll()
	{
		lock (_lock)
		{
			if (_count == 0)
			{
				return Array.Empty<TelemetrySampleDto>();
			}

			var result = new TelemetrySampleDto[_count];
			if (_count < _capacity)
			{
				// Buffer not full yet, data is contiguous from start
				Array.Copy(_buffer, 0, result, 0, _count);
			}
			else
			{
				// Buffer is full, need to wrap around
				var firstPart = _capacity - _writeIndex;
				Array.Copy(_buffer, _writeIndex, result, 0, firstPart);
				Array.Copy(_buffer, 0, result, firstPart, _writeIndex);
			}
			return result;
		}
	}

	public TelemetrySampleDto[] GetLapData(int lapNumber)
	{
		lock (_lock)
		{
			return GetAll()
				.Where(s => s.LapNumber == lapNumber)
				.ToArray();
		}
	}

	public int[] GetAvailableLaps()
	{
		lock (_lock)
		{
			return GetAll()
				.Select(s => s.LapNumber)
				.Where(lap => lap > 0)
				.Distinct()
				.OrderBy(lap => lap)
				.ToArray();
		}
	}

	public double? GetLapFraction(int lapNumber, TelemetrySampleDto sample)
	{
		if (lapNumber < 0)
		{
			return null;
		}

		lock (_lock)
		{
			var allSamples = GetAll();
			if (allSamples.Length < 2)
			{
				return null;
			}

			var sampleIndex = Array.FindIndex(allSamples, s => ReferenceEquals(s, sample));
			if (sampleIndex < 0 && sample.Timestamp.HasValue)
			{
				var timestamp = sample.Timestamp.Value;
				sampleIndex = Array.FindIndex(
					allSamples,
					s => s.LapNumber == lapNumber && s.Timestamp.HasValue && s.Timestamp.Value == timestamp);
			}

			if (sampleIndex < 0)
			{
				sampleIndex = allSamples.Length - 1;
			}

			var lapStartIndex = FindLapStartIndex(allSamples, sampleIndex, lapNumber);
			var progressIndex = Math.Max(0, sampleIndex - lapStartIndex);
			var previousLapLength = FindPreviousLapLength(allSamples, lapStartIndex, lapNumber);
			if (previousLapLength >= 2)
			{
				return Math.Clamp((double)progressIndex / (previousLapLength - 1), 0.0, 1.0);
			}

			if (sample.Timestamp.HasValue && allSamples[lapStartIndex].Timestamp.HasValue)
			{
				var lapDurationSeconds = FindPreviousLapDurationSeconds(allSamples, lapStartIndex, lapNumber);
				if (lapDurationSeconds <= 0)
				{
					lapDurationSeconds = DefaultLapDurationSeconds;
				}

				var elapsedSeconds = (sample.Timestamp.Value - allSamples[lapStartIndex].Timestamp.Value).TotalSeconds;
				if (elapsedSeconds < 0)
				{
					elapsedSeconds = 0;
				}

				var fraction = elapsedSeconds / lapDurationSeconds;
				return fraction - Math.Floor(fraction);
			}

			var estimatedLapSamples = DefaultLapSampleCount;
			var wrappedIndex = progressIndex % estimatedLapSamples;
			return (double)wrappedIndex / Math.Max(1, estimatedLapSamples - 1);
		}
	}

	private static int FindLapStartIndex(TelemetrySampleDto[] samples, int sampleIndex, int lapNumber)
	{
		var index = Math.Clamp(sampleIndex, 0, samples.Length - 1);
		while (index > 0 && samples[index - 1].LapNumber == lapNumber)
		{
			index--;
		}
		return index;
	}

	private static int FindPreviousLapLength(TelemetrySampleDto[] samples, int lapStartIndex, int lapNumber)
	{
		if (lapStartIndex <= 0)
		{
			return 0;
		}

		var previousLapNumber = samples[lapStartIndex - 1].LapNumber;
		if (previousLapNumber == lapNumber)
		{
			return 0;
		}

		var previousLapEnd = lapStartIndex - 1;
		var previousLapStart = previousLapEnd;
		while (previousLapStart > 0 && samples[previousLapStart - 1].LapNumber == previousLapNumber)
		{
			previousLapStart--;
		}

		return previousLapEnd - previousLapStart + 1;
	}

	private static double FindPreviousLapDurationSeconds(TelemetrySampleDto[] samples, int lapStartIndex, int lapNumber)
	{
		if (lapStartIndex <= 0)
		{
			return 0;
		}

		var previousLapNumber = samples[lapStartIndex - 1].LapNumber;
		if (previousLapNumber == lapNumber)
		{
			return 0;
		}

		var previousLapEnd = lapStartIndex - 1;
		var previousLapStart = previousLapEnd;
		while (previousLapStart > 0 && samples[previousLapStart - 1].LapNumber == previousLapNumber)
		{
			previousLapStart--;
		}

		var startTime = samples[previousLapStart].Timestamp;
		var endTime = samples[previousLapEnd].Timestamp;
		if (!startTime.HasValue || !endTime.HasValue)
		{
			return 0;
		}

		var duration = (endTime.Value - startTime.Value).TotalSeconds;
		return duration > 0 ? duration : 0;
	}

	public TelemetrySampleDto? GetLatest()
	{
		lock (_lock)
		{
			if (_count == 0)
			{
				return null;
			}

			var lastIndex = (_writeIndex - 1 + _capacity) % _capacity;
			return _buffer[lastIndex];
		}
	}

	public TelemetrySampleDto[] GetRange(int startIndex, int count)
	{
		lock (_lock)
		{
			if (startIndex < 0 || startIndex >= _count)
			{
				return Array.Empty<TelemetrySampleDto>();
			}

			var actualCount = Math.Min(count, _count - startIndex);
			var result = new TelemetrySampleDto[actualCount];
			
			var allData = GetAll();
			Array.Copy(allData, startIndex, result, 0, actualCount);
			
			return result;
		}
	}

	public void Clear()
	{
		lock (_lock)
		{
			Array.Clear(_buffer, 0, _capacity);
			_writeIndex = 0;
			_count = 0;
		}
	}

	public Dictionary<string, double> GetValueAtIndex(int index)
	{
		lock (_lock)
		{
			var allData = GetAll();
			if (index < 0 || index >= allData.Length)
			{
				return new Dictionary<string, double>();
			}

			var sample = allData[index];
			return new Dictionary<string, double>
			{
				["Speed"] = sample.SpeedKph,
				["Fuel"] = sample.FuelLiters,
				["Throttle"] = sample.ThrottlePosition * 100,
				["Brake"] = sample.BrakePosition * 100,
				["Steering"] = sample.SteeringAngle,
				["TyreFL"] = sample.TyreTempsC.Length > 0 ? sample.TyreTempsC[0] : 0,
				["TyreFR"] = sample.TyreTempsC.Length > 1 ? sample.TyreTempsC[1] : 0,
				["TyreRL"] = sample.TyreTempsC.Length > 2 ? sample.TyreTempsC[2] : 0,
				["TyreRR"] = sample.TyreTempsC.Length > 3 ? sample.TyreTempsC[3] : 0
			};
		}
	}
}
