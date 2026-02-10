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
				.Distinct()
				.OrderBy(lap => lap)
				.ToArray();
		}
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
