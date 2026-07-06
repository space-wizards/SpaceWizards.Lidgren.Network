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
using System.Net;
using System.Net.Sockets;

namespace Lidgren.Network
{
	/// <summary>
	/// Partly immutable after NetPeer has been initialized
	/// </summary>
	public sealed class NetPeerConfiguration
	{
		// Default MTU value used to be pretty high. I've slowly had to reduce it over the years due to continuing
		// reports of "yet another person with an even shittier network" having issues.
		// The default values are now the spec-minimum values.
		// IPv4: 576 (spec minimum) - 60 (max IPv4 header) - 8 (UDP header) = 508
		// IPv6: 1280 (spec minimum) - 40 (IPv6 header) - 8 (UDP header) = 1232

		/// <summary>
		/// Default MTU value for IPv4 connections, in bytes.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Note that lidgren headers (5 bytes) are not included here; since it's part of the "mtu payload".
		/// </para>
		/// </remarks>
		public const int kDefaultMTU = 508;

		/// <summary>
		/// Default MTU value for IPv6 connections, in bytes.
		/// </summary>
		/// <remarks>
		/// Note that lidgren headers (5 bytes) are not included here; since it's part of the "mtu payload"
		/// </remarks>
		public const int kDefaultMTUV6 = 1232;

		private const string c_isLockedMessage = "You may not modify the NetPeerConfiguration after it has been used to initialize a NetPeer";

		private bool m_isLocked;
		private readonly string m_appIdentifier;
		private string m_networkThreadName;
		private IPAddress m_localAddress;
		private IPAddress m_broadcastAddress;
        private bool m_dualStack;

		internal bool m_acceptIncomingConnections;
		internal int m_maximumConnections;
		internal int m_defaultOutgoingMessageCapacity;
		internal float m_pingInterval;
		internal bool m_useMessageRecycling;
		internal int m_recycledCacheMaxCount;
		internal float m_connectionTimeout;
		internal bool m_enableUPnP;
		internal bool m_autoFlushSendQueue;
		private NetUnreliableSizeBehaviour m_unreliableSizeBehaviour;
		internal bool m_suppressUnreliableUnorderedAcks;
		private bool m_sendConnectionRejectionReasons;

		internal NetIncomingMessageType m_disabledTypes;
		internal int m_port;
		internal int m_receiveBufferSize;
		internal int m_sendBufferSize;
		internal float m_resendHandshakeInterval;
		internal int m_maximumHandshakeAttempts;

		internal bool m_logRateLimiterEnabled;
		internal NetLogRateLimitTarget m_logRateLimitTargets;
		internal int m_logRateLimitBurst;
		internal float m_logRateLimitWindow;

		// bad network simulation
		internal float m_loss;
		internal float m_duplicates;
		internal float m_minimumOneWayLatency;
		internal float m_randomOneWayLatency;

		// MTU
		internal int m_maximumTransmissionUnit;
		internal int m_maximumTransmissionUnitV6;
		internal bool m_autoExpandMTU;
		internal float m_expandMTUFrequency;
		internal int m_expandMTUFailAttempts;
		internal int m_maximumFragmentReassemblyBytesPerConnection;
		internal float m_fragmentGroupTimeout;

		/// <summary>
		/// NetPeerConfiguration constructor
		/// </summary>
		public NetPeerConfiguration(string appIdentifier)
		{
			if (string.IsNullOrEmpty(appIdentifier))
				throw new NetException("App identifier must be at least one character long");
			m_appIdentifier = appIdentifier;

			//
			// default values
			//
			m_disabledTypes = NetIncomingMessageType.ConnectionApproval | NetIncomingMessageType.UnconnectedData | NetIncomingMessageType.VerboseDebugMessage | NetIncomingMessageType.ConnectionLatencyUpdated | NetIncomingMessageType.NatIntroductionSuccess;
			m_networkThreadName = "Lidgren network thread";
			m_localAddress = IPAddress.Any;
			m_broadcastAddress = IPAddress.Broadcast;
			var ip = NetUtility.GetBroadcastAddress();
			if (ip != null)
			{
				m_broadcastAddress = ip;
			}
			m_port = 0;
			m_receiveBufferSize = 131071;
			m_sendBufferSize = 131071;
			m_acceptIncomingConnections = false;
			m_maximumConnections = 32;
			m_defaultOutgoingMessageCapacity = 16;
			m_pingInterval = 4.0f;
			m_connectionTimeout = 25.0f;
			m_useMessageRecycling = true;
			m_recycledCacheMaxCount = 64;
			m_resendHandshakeInterval = 3.0f;
			m_maximumHandshakeAttempts = 5;
			m_autoFlushSendQueue = true;
			m_suppressUnreliableUnorderedAcks = false;
			m_sendConnectionRejectionReasons = false;

			m_logRateLimiterEnabled = true;
			m_logRateLimitTargets = NetLogRateLimitTarget.All;
			m_logRateLimitBurst = 5;
			m_logRateLimitWindow = 10.0f;

			m_maximumTransmissionUnit = kDefaultMTU;
			m_maximumTransmissionUnitV6 = kDefaultMTUV6;
			m_autoExpandMTU = false;
			m_expandMTUFrequency = 2.0f;
			m_expandMTUFailAttempts = 5;
			m_maximumFragmentReassemblyBytesPerConnection = 32 * 1024 * 1024;
			m_fragmentGroupTimeout = 30.0f;
			m_unreliableSizeBehaviour = NetUnreliableSizeBehaviour.IgnoreMTU;

			m_loss = 0.0f;
			m_minimumOneWayLatency = 0.0f;
			m_randomOneWayLatency = 0.0f;
			m_duplicates = 0.0f;

			m_isLocked = false;
		}

		internal void Lock()
		{
			m_isLocked = true;
		}

		/// <summary>
		/// Gets the identifier of this application; the library can only connect to matching app identifier peers
		/// </summary>
		public string AppIdentifier
		{
			get { return m_appIdentifier; }
		}

		/// <summary>
		/// Enables receiving of the specified type of message
		/// </summary>
		public void EnableMessageType(NetIncomingMessageType type)
		{
			m_disabledTypes &= (~type);
		}

		/// <summary>
		/// Disables receiving of the specified type of message
		/// </summary>
		public void DisableMessageType(NetIncomingMessageType type)
		{
			m_disabledTypes |= type;
		}

		/// <summary>
		/// Enables or disables receiving of the specified type of message
		/// </summary>
		public void SetMessageTypeEnabled(NetIncomingMessageType type, bool enabled)
		{
			if (enabled)
				m_disabledTypes &= (~type);
			else
				m_disabledTypes |= type;
		}

		/// <summary>
		/// Gets if receiving of the specified type of message is enabled
		/// </summary>
		public bool IsMessageTypeEnabled(NetIncomingMessageType type)
		{
			return !((m_disabledTypes & type) == type);
		}

		/// <summary>
		/// Gets or sets the behaviour of unreliable sends above MTU
		/// </summary>
		public NetUnreliableSizeBehaviour UnreliableSizeBehaviour
		{
			get { return m_unreliableSizeBehaviour; }
			set { m_unreliableSizeBehaviour = value; }
		}

		/// <summary>
		/// Gets or sets the name of the library network thread. Cannot be changed once NetPeer is initialized.
		/// </summary>
		public string NetworkThreadName
		{
			get { return m_networkThreadName; }
			set
			{
				if (m_isLocked)
					throw new NetException("NetworkThreadName may not be set after the NetPeer which uses the configuration has been started");
				m_networkThreadName = value;
			}
		}

		/// <summary>
		/// Gets or sets the maximum amount of connections this peer can hold. Cannot be changed once NetPeer is initialized.
		/// </summary>
		public int MaximumConnections
		{
			get { return m_maximumConnections; }
			set
			{
				if (m_isLocked)
					throw new NetException(c_isLockedMessage);
				m_maximumConnections = value;
			}
		}

		/// <summary>
		/// The maximum number of connections allowed per IP address.
		/// </summary>
		public int MaximumIpConnections = 8;

		/// <summary>
		/// The maximum number of times a single IP address is allowed to try connect, within the window of <see cref="RapidConnectionWindow"/>.
		/// </summary>
		public int MaximumRapidConnections = 3;

		/// <summary>
		/// Should we send a disconnect reason when the limit is hit.
		/// Avoids the server trying to spam messages out.
		/// </summary>
		public bool SendConnectionRejectionReasons
		{
			get { return m_sendConnectionRejectionReasons; }
			set { m_sendConnectionRejectionReasons = value; }
		}

        /// <summary>
        /// How many seconds until connection count decays for <see cref="MaximumRapidConnections"/>.
        /// </summary>
		public double RapidConnectionWindow = 30.0;

		/// <summary>
		/// How many connections are "forgotten" every <see cref="RapidConnectionWindow"/> seconds.
		/// </summary>
		public int RapidConnectionDecay = 1;

		/// <summary>
		/// Gets or sets the maximum amount of bytes to send in a single packet for IPv4 connections, excluding IPv4 and UDP headers.
		/// Cannot be changed once NetPeer is initialized.
		/// </summary>
		/// <seealso cref="MaximumTransmissionUnitV6"/>
		public int MaximumTransmissionUnit
		{
			get { return m_maximumTransmissionUnit; }
			set
			{
				if (m_isLocked)
					throw new NetException(c_isLockedMessage);
				if (value < 1 || value >= ((ushort.MaxValue + 1) / 8))
					throw new NetException("MaximumTransmissionUnit must be between 1 and " + (((ushort.MaxValue + 1) / 8) - 1) + " bytes");
				m_maximumTransmissionUnit = value;
			}
		}

		/// <summary>
		/// Gets or sets the maximum amount of bytes to send in a single packet for IPv6 connections, excluding IPv6 and UDP headers.
		/// Cannot be changed once NetPeer is initialized.
		/// </summary>
		/// <seealso cref="MaximumTransmissionUnit"/>
		public int MaximumTransmissionUnitV6
		{
			get { return m_maximumTransmissionUnitV6; }
			set
			{
				if (m_isLocked)
					throw new NetException(c_isLockedMessage);
				if (value < 1 || value >= ((ushort.MaxValue + 1) / 8))
					throw new NetException("MaximumTransmissionUnitV6 must be between 1 and " + (((ushort.MaxValue + 1) / 8) - 1) + " bytes");
				m_maximumTransmissionUnitV6 = value;
			}
		}

		/// <summary>
		/// Gets or sets the default capacity in bytes when NetPeer.CreateMessage() is called without argument
		/// </summary>
		public int DefaultOutgoingMessageCapacity
		{
			get { return m_defaultOutgoingMessageCapacity; }
			set { m_defaultOutgoingMessageCapacity = value; }
		}

		/// <summary>
		/// Gets or sets the time between latency calculating pings
		/// </summary>
		public float PingInterval
		{
			get { return m_pingInterval; }
			set { m_pingInterval = value; }
		}

		/// <summary>
		/// Gets or sets if the library should recycling messages to avoid excessive garbage collection. Cannot be changed once NetPeer is initialized.
		/// </summary>
		public bool UseMessageRecycling
		{
			get { return m_useMessageRecycling; }
			set
			{
				if (m_isLocked)
					throw new NetException(c_isLockedMessage);
				m_useMessageRecycling = value;
			}
		}

		/// <summary>
		/// Gets or sets the maximum number of incoming/outgoing messages to keep in the recycle cache.
		/// </summary>
		public int RecycledCacheMaxCount
		{
			get { return m_recycledCacheMaxCount; }
			set
			{
				if (m_isLocked)
					throw new NetException(c_isLockedMessage);
				m_recycledCacheMaxCount = value;
			}
		}

		/// <summary>
		/// Gets or sets the number of seconds timeout will be postponed on a successful ping/pong
		/// </summary>
		public float ConnectionTimeout
		{
			get { return m_connectionTimeout; }
			set
			{
				if (value < m_pingInterval)
					throw new NetException("Connection timeout cannot be lower than ping interval!");
				m_connectionTimeout = value;
			}
		}

		/// <summary>
		/// Enables UPnP support; enabling port forwarding and getting external ip
		/// </summary>
		public bool EnableUPnP
		{
			get { return m_enableUPnP; }
			set
			{
				if (m_isLocked)
					throw new NetException(c_isLockedMessage);
				m_enableUPnP = value;
			}
		}

		/// <summary>
		/// Enables or disables automatic flushing of the send queue. If disabled, you must manully call NetPeer.FlushSendQueue() to flush sent messages to network.
		/// </summary>
		public bool AutoFlushSendQueue
		{
			get { return m_autoFlushSendQueue; }
			set { m_autoFlushSendQueue = value; }
		}

		/// <summary>
		/// If true, will not send acks for unreliable unordered messages. This will save bandwidth, but disable flow control and duplicate detection for this type of messages.
		/// </summary>
		public bool SuppressUnreliableUnorderedAcks
		{
			get { return m_suppressUnreliableUnorderedAcks; }
			set
			{
				if (m_isLocked)
					throw new NetException(c_isLockedMessage);
				m_suppressUnreliableUnorderedAcks = value;
			}
		}

		/// <summary>
		/// Gets or sets the local ip address to bind to. Defaults to IPAddress.Any. Cannot be changed once NetPeer is initialized.
		/// </summary>
		public IPAddress LocalAddress
		{
			get { return m_localAddress; }
			set
			{
				if (m_isLocked)
					throw new NetException(c_isLockedMessage);
				m_localAddress = value;
			}
		}

        /// <summary>
        /// Gets or sets a value indicating whether the library should use IPv6 dual stack mode.
        /// If you enable this you should make sure that the <see cref="LocalAddress"/> is an IPv6 address.
        /// Cannot be changed once NetPeer is initialized.
        /// </summary>
        public bool DualStack
        {
            get { return m_dualStack;  }
            set
            {
                if (m_isLocked)
                    throw new NetException(c_isLockedMessage);
                m_dualStack = value;
            }
        }

        /// <summary>
        /// Gets or sets the local broadcast address to use when broadcasting
        /// </summary>
        public IPAddress BroadcastAddress
		{
			get { return m_broadcastAddress; }
			set
			{
				if (m_isLocked)
					throw new NetException(c_isLockedMessage);
				m_broadcastAddress = value;
			}
		}

		/// <summary>
		/// Gets or sets the local port to bind to. Defaults to 0. Cannot be changed once NetPeer is initialized.
		/// </summary>
		public int Port
		{
			get { return m_port; }
			set
			{
				if (m_isLocked)
					throw new NetException(c_isLockedMessage);
				m_port = value;
			}
		}

		/// <summary>
		/// Gets or sets the size in bytes of the receiving buffer. Defaults to 131071 bytes. Cannot be changed once NetPeer is initialized.
		/// </summary>
		public int ReceiveBufferSize
		{
			get { return m_receiveBufferSize; }
			set
			{
				if (m_isLocked)
					throw new NetException(c_isLockedMessage);
				m_receiveBufferSize = value;
			}
		}

		/// <summary>
		/// Gets or sets the size in bytes of the sending buffer. Defaults to 131071 bytes. Cannot be changed once NetPeer is initialized.
		/// </summary>
		public int SendBufferSize
		{
			get { return m_sendBufferSize; }
			set
			{
				if (m_isLocked)
					throw new NetException(c_isLockedMessage);
				m_sendBufferSize = value;
			}
		}

		/// <summary>
		/// Gets or sets if the NetPeer should accept incoming connections. This is automatically set to true in NetServer and false in NetClient.
		/// </summary>
		public bool AcceptIncomingConnections
		{
			get { return m_acceptIncomingConnections; }
			set { m_acceptIncomingConnections = value; }
		}

		/// <summary>
		/// Gets or sets the number of seconds between handshake attempts
		/// </summary>
		public float ResendHandshakeInterval
		{
			get { return m_resendHandshakeInterval; }
			set { m_resendHandshakeInterval = value; }
		}

		/// <summary>
		/// Gets or sets the maximum number of handshake attempts before failing to connect
		/// </summary>
		public int MaximumHandshakeAttempts
		{
			get { return m_maximumHandshakeAttempts; }
			set
			{
				if (value < 1)
					throw new NetException("MaximumHandshakeAttempts must be at least 1");
				m_maximumHandshakeAttempts = value;
			}
		}

		/// <summary>
		/// Gets or sets if repeated malformed network input logs should be rate limited.
		/// </summary>
		public bool LogRateLimiterEnabled
		{
			get { return m_logRateLimiterEnabled; }
			set { m_logRateLimiterEnabled = value; }
		}

		/// <summary>
		/// Gets or sets which malformed network input log categories are rate limited.
		/// </summary>
		public NetLogRateLimitTarget LogRateLimitTargets
		{
			get { return m_logRateLimitTargets; }
			set { m_logRateLimitTargets = value; }
		}

		/// <summary>
		/// Gets or sets how many matching logs are emitted per endpoint and category before suppression starts.
		/// </summary>
		public int LogRateLimitBurst
		{
			get { return m_logRateLimitBurst; }
			set
			{
				if (value < 1)
					throw new NetException("LogRateLimitBurst must be at least 1");
				m_logRateLimitBurst = value;
			}
		}

		/// <summary>
		/// Gets or sets the rate limit window in seconds.
		/// </summary>
		public float LogRateLimitWindow
		{
			get { return m_logRateLimitWindow; }
			set
			{
				if (value <= 0.0f)
					throw new NetException("LogRateLimitWindow must be greater than zero");
				m_logRateLimitWindow = value;
			}
		}


		/// <summary>
		/// Gets or sets if the NetPeer should send large messages to try to expand the maximum transmission unit size
		/// </summary>
		public bool AutoExpandMTU
		{
			get { return m_autoExpandMTU; }
			set
			{
				if (m_isLocked)
					throw new NetException(c_isLockedMessage);
				m_autoExpandMTU = value;
			}
		}

		/// <summary>
		/// Gets or sets how often to send large messages to expand MTU if AutoExpandMTU is enabled
		/// </summary>
		public float ExpandMTUFrequency
		{
			get { return m_expandMTUFrequency; }
			set
			{
				if (!float.IsFinite(value) || value <= 0)
					throw new NetException("ExpandMTUFrequency must be greater than zero");
				m_expandMTUFrequency = value;
			}
		}

		/// <summary>
		/// Gets or sets the number of failed expand mtu attempts to perform before setting final MTU
		/// </summary>
		public int ExpandMTUFailAttempts
		{
			get { return m_expandMTUFailAttempts; }
			set
			{
				if (value <= 0)
					throw new NetException("ExpandMTUFailAttempts must be greater than zero");
				m_expandMTUFailAttempts = value;
			}
		}

		/// <summary>
		/// Gets or sets the maximum bytes used by incomplete fragment groups for one connection.
		/// </summary>
		public int MaximumFragmentReassemblyBytesPerConnection
		{
			get { return m_maximumFragmentReassemblyBytesPerConnection; }
			set
			{
				if (value < 1)
					throw new NetException("MaximumFragmentReassemblyBytesPerConnection must be at least 1");
				m_maximumFragmentReassemblyBytesPerConnection = value;
			}
		}

		/// <summary>
		/// Gets or sets how long an incomplete fragment group is kept alive, in seconds.
		/// </summary>
		public float FragmentGroupTimeout
		{
			get { return m_fragmentGroupTimeout; }
			set
			{
				if (value <= 0.0f)
					throw new NetException("FragmentGroupTimeout must be greater than zero");
				m_fragmentGroupTimeout = value;
			}
		}

		/// <summary>
		/// Gets or sets the simulated amount of sent packets lost from 0.0f to 1.0f
		/// </summary>
		public float SimulatedLoss
		{
			get { return m_loss; }
			set { m_loss = value; }
		}

		/// <summary>
		/// Gets or sets the minimum simulated amount of one way latency for sent packets in seconds
		/// </summary>
		public float SimulatedMinimumLatency
		{
			get { return m_minimumOneWayLatency; }
			set { m_minimumOneWayLatency = value; }
		}

		/// <summary>
		/// Gets or sets the simulated added random amount of one way latency for sent packets in seconds
		/// </summary>
		public float SimulatedRandomLatency
		{
			get { return m_randomOneWayLatency; }
			set { m_randomOneWayLatency = value; }
		}

		/// <summary>
		/// Gets the average simulated one way latency in seconds
		/// </summary>
		public float SimulatedAverageLatency
		{
			get { return m_minimumOneWayLatency + (m_randomOneWayLatency * 0.5f); }
		}

		/// <summary>
		/// Gets or sets the simulated amount of duplicated packets from 0.0f to 1.0f
		/// </summary>
		public float SimulatedDuplicatesChance
		{
			get { return m_duplicates; }
			set { m_duplicates = value; }
		}

		/// <summary>
		/// Creates a memberwise shallow clone of this configuration
		/// </summary>
		public NetPeerConfiguration Clone()
		{
			NetPeerConfiguration retval = (NetPeerConfiguration)this.MemberwiseClone();
			retval.m_isLocked = false;
			return retval;
		}

		internal int MTUForAddress(IPAddress address)
		{
			if (address.AddressFamily == AddressFamily.InterNetworkV6)
				return address.IsIPv4MappedToIPv6 ? MaximumTransmissionUnit : MaximumTransmissionUnitV6;

			return MaximumTransmissionUnit;
		}

		internal int MTUForEndPoint(IPEndPoint endPoint) => MTUForAddress(endPoint.Address);
	}

	/// <summary>
	/// Behaviour of unreliable sends above MTU
	/// </summary>
	public enum NetUnreliableSizeBehaviour
	{
		/// <summary>
		/// Sending an unreliable message will ignore MTU and send everything in a single packet; this is the new default
		/// </summary>
		IgnoreMTU = 0,

		/// <summary>
		/// Old behaviour; use normal fragmentation for unreliable messages - if a fragment is dropped, memory for received fragments are never reclaimed!
		/// </summary>
		NormalFragmentation = 1,

		/// <summary>
		/// Alternate behaviour; just drops unreliable messages above MTU
		/// </summary>
		DropAboveMTU = 2,
	}
}
