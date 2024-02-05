﻿/* Copyright (c) 2010 Michael Lidgren

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
using System.Diagnostics;

namespace Lidgren.Network
{
	public partial class NetPeer
	{
		internal event Action<NetIncomingMessageType, string>? LogEvent;

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
		
		[Conditional("DEBUG")]
		internal void LogWarning(string message)
		{
#if __ANDROID__
			Android.Util.Log.WriteLine(Android.Util.LogPriority.Warn, "", message);
#endif
			SendLogBase(NetIncomingMessageType.WarningMessage, message);
		}
		
		[Conditional("DEBUG")]
		internal void LogError(string message)
		{
#if __ANDROID__
			Android.Util.Log.WriteLine(Android.Util.LogPriority.Error, "", message);
#endif
			SendLogBase(NetIncomingMessageType.ErrorMessage, message);
		}

		private void SendLogBase(NetIncomingMessageType type, string text)
		{
			LogEvent?.Invoke(type, text);

			if (m_configuration.IsMessageTypeEnabled(type))
				ReleaseMessage(CreateIncomingMessage(type, text));
		}
	}
}
