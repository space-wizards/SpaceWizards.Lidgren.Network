/* Copyright (c) 2010 Michael Lidgren

Permission is hereby granted, free of charge, to any person obtaining a copy of this software
and associated documentation files (the "Software"), to deal in the Software without
restriction, including without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom
the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE
USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

#if !__NOIPENDPOINT__
using NetEndPoint = System.Net.IPEndPoint;
#endif

namespace Lidgren.Network;

public partial class NetPeer
{
	internal event Action<NetIncomingMessageType, string>? LogEvent;
	private readonly Dictionary<LogRateLimitKey, LogRateLimitState> m_logRateLimiters = new Dictionary<LogRateLimitKey, LogRateLimitState>();
	private double m_lastLogRateLimiterPrune;

	private readonly List<KeyValuePair<LogRateLimitKey, LogRateLimitState>> _tempLimiterPairs = new();

	private readonly struct LogRateLimitKey : IEquatable<LogRateLimitKey>
	{
		public readonly NetLogRateLimitTarget Target;
		public readonly string EndPoint;

		public LogRateLimitKey(NetLogRateLimitTarget target, string endPoint)
		{
			Target = target;
			EndPoint = endPoint;
		}

		public bool Equals(LogRateLimitKey other)
		{
			return Target == other.Target && EndPoint.Equals(other.EndPoint, StringComparison.Ordinal);
		}

		public override bool Equals(object? obj)
		{
			return obj is LogRateLimitKey other && Equals(other);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Target, EndPoint);
		}
	}

	private sealed class LogRateLimitState
	{
		public double WindowStarted;
		public int Count;
		public int Suppressed;
	}

	[Conditional("DEBUG")]
	internal void LogVerbose(string message)
	{
#if __ANDROID__
		Android.Util.Log.WriteLine(Android.Util.LogPriority.Verbose, "", message);
#endif
		SendLogBase(NetIncomingMessageType.VerboseDebugMessage, message);
	}

	[Conditional("DEBUG")]
	internal void LogDebug(string message)
	{
#if __ANDROID__
		Android.Util.Log.WriteLine(Android.Util.LogPriority.Debug, "", message);
#endif
		SendLogBase(NetIncomingMessageType.DebugMessage, message);
	}

	internal void LogWarning(string message)
	{
#if __ANDROID__
		Android.Util.Log.WriteLine(Android.Util.LogPriority.Warn, "", message);
#endif
		SendLogBase(NetIncomingMessageType.WarningMessage, message);
	}

	internal void LogError(string message)
	{
#if __ANDROID__
		Android.Util.Log.WriteLine(Android.Util.LogPriority.Error, "", message);
#endif
		SendLogBase(NetIncomingMessageType.ErrorMessage, message);
	}

	internal void LogRateLimitedWarning(NetLogRateLimitTarget target, NetEndPoint? senderEndPoint, string message)
	{
		if (TryRateLimitLog(target, senderEndPoint, ref message))
			LogWarning(message);
	}

	internal void LogRateLimitedError(NetLogRateLimitTarget target, NetEndPoint? senderEndPoint, string message)
	{
		if (TryRateLimitLog(target, senderEndPoint, ref message))
			LogError(message);
	}

	private bool TryRateLimitLog(NetLogRateLimitTarget target, NetEndPoint? senderEndPoint, ref string message)
	{
		// If it's disabled / not in the logged groups send it.
		if (!m_configuration.m_logRateLimiterEnabled
			|| (m_configuration.m_logRateLimitTargets & target) == 0
			|| senderEndPoint == null)
		{
			return true;
		}

		// Rate-limiter is stored per net-peer so remove any very old ones when the log comes in.
		double now = NetTime.Now;
		double window = m_configuration.m_logRateLimitWindow;
		PruneLogRateLimiters(now, window);

		// Make a new rate-limit window if relevant.
		var key = new LogRateLimitKey(target, senderEndPoint.ToString());
		if (!m_logRateLimiters.TryGetValue(key, out var state))
		{
			state = new LogRateLimitState { WindowStarted = now };
			m_logRateLimiters[key] = state;
		}

		// We've gone out of the window time so dump how many we suppressed.
		if (now - state.WindowStarted >= window)
		{
			int suppressed = state.Suppressed;
			state.WindowStarted = now;
			state.Count = 1;
			state.Suppressed = 0;

			if (suppressed > 0)
				message += $" (suppressed {suppressed} similar logs in the previous {window:0.##}s)";

			return true;
		}

		// Haven't hit the short-term limit yet.
		if (state.Count < m_configuration.m_logRateLimitBurst)
		{
			state.Count++;
			return true;
		}

		// Actually limited.
		state.Suppressed++;
		return false;
	}

	private void PruneLogRateLimiters(double now, double window)
	{
		if (now - m_lastLogRateLimiterPrune < window)
			return;

		m_lastLogRateLimiterPrune = now;
		double oldestWindow = now - (window * 2.0);

		_tempLimiterPairs.Clear();
		_tempLimiterPairs.AddRange(m_logRateLimiters);

		foreach (var pair in _tempLimiterPairs)
		{
			if (pair.Value.WindowStarted < oldestWindow)
				m_logRateLimiters.Remove(pair.Key);
		}
	}

	private void SendLogBase(NetIncomingMessageType type, string text)
	{
		LogEvent?.Invoke(type, text);

		if (m_configuration.IsMessageTypeEnabled(type))
			ReleaseMessage(CreateIncomingMessage(type, text));
	}

	private bool CheckLogEnabled(NetIncomingMessageType type)
	{
		return m_configuration.IsMessageTypeEnabled(type) || LogEvent != null;
	}

#if NET6_0_OR_GREATER && !__ANDROID__
	// On supported TFMs, use an interpolated string handler,
	// so we can avoid running string formatting if a log level is disabled.
	internal void LogWarning([InterpolatedStringHandlerArgument("")] NetWarningLogInterpolatedStringHandler text)
	{
		SendLogBase(NetIncomingMessageType.WarningMessage, text.Implementation);
	}

	internal void LogError([InterpolatedStringHandlerArgument("")] NetErrorLogInterpolatedStringHandler text)
	{
		SendLogBase(NetIncomingMessageType.ErrorMessage, text.Implementation);
	}

	private void SendLogBase(NetIncomingMessageType type, DefaultInterpolatedStringHandler text)
	{
		if (!CheckLogEnabled(type))
			return;

		SendLogBase(type, text.ToStringAndClear());
	}

	[InterpolatedStringHandler]
	internal ref struct NetErrorLogInterpolatedStringHandler
	{
		public DefaultInterpolatedStringHandler Implementation;

		public NetErrorLogInterpolatedStringHandler(
			int literalLength,
			int formattedCount,
			NetPeer peer,
			out bool handlerIsValid)
		{
			handlerIsValid = peer.CheckLogEnabled(NetIncomingMessageType.ErrorMessage);

			if (handlerIsValid)
				Implementation = new DefaultInterpolatedStringHandler(literalLength, formattedCount);
		}

		public void AppendLiteral(string value)
			=> Implementation.AppendLiteral(value);

		public void AppendFormatted<T>(T t, int alignment = 0, string? format = null)
			=> Implementation.AppendFormatted(t, alignment, format);

		public string ToStringAndClear() => Implementation.ToStringAndClear();
	}

	[InterpolatedStringHandler]
	internal ref struct NetWarningLogInterpolatedStringHandler
	{
		public DefaultInterpolatedStringHandler Implementation;

		public NetWarningLogInterpolatedStringHandler(
			int literalLength,
			int formattedCount,
			NetPeer peer,
			out bool handlerIsValid)
		{
			handlerIsValid = peer.CheckLogEnabled(NetIncomingMessageType.WarningMessage);

			if (handlerIsValid)
				Implementation = new DefaultInterpolatedStringHandler(literalLength, formattedCount);
		}

		public void AppendLiteral(string value)
			=> Implementation.AppendLiteral(value);

		public void AppendFormatted<T>(T t, int alignment = 0, string? format = null)
			=> Implementation.AppendFormatted(t, alignment, format);

		public string ToStringAndClear() => Implementation.ToStringAndClear();
	}
#endif
}

/// <summary>
/// Log categories that can be rate limited when they are caused by malformed network input.
/// </summary>
[Flags]
public enum NetLogRateLimitTarget
{
	/// <summary>
	/// Do not rate limit any malformed network input logs.
	/// </summary>
	None = 0,

	/// <summary>
	/// Rate limit malformed packet header and message type logs.
	/// </summary>
	MalformedPacket = 1 << 0,

	/// <summary>
	/// Rate limit malformed fragmentation header logs.
	/// </summary>
	MalformedFragment = 1 << 1,

	/// <summary>
	/// Rate limit packet parsing exception logs.
	/// </summary>
	PacketParsingError = 1 << 2,

	/// <summary>
	/// Rate limit unhandled library message logs.
	/// </summary>
	UnhandledLibraryMessage = 1 << 3,

	/// <summary>
	/// Rate limit all malformed network input log categories.
	/// </summary>
	All = MalformedPacket | MalformedFragment | PacketParsingError | UnhandledLibraryMessage,
}
