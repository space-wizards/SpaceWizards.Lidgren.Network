using System;
using System.Net;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using Lidgren.Network;

using MSCommon;

namespace MSClient
{
	static class Program
	{
		private static Form1 m_mainForm;
		private static NetClient m_client;
		private static IPEndPoint m_masterServer;
		private static Dictionary<long, IPEndPoint[]> m_hostList;
		public static extern int SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);
		public const int WM_VSCROLL = 277; // Vertical scroll
		public const int SB_BOTTOM = 7; // Scroll to bottom 
		
		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			m_mainForm = new Form1();

			m_hostList = new Dictionary<long, IPEndPoint[]>();

			NetPeerConfiguration config = new NetPeerConfiguration("game");
			config.EnableMessageType(NetIncomingMessageType.UnconnectedData);
			config.EnableMessageType(NetIncomingMessageType.NatIntroductionSuccess);
			m_client = new NetClient(config);
			m_client.Start();

			Application.Idle += new EventHandler(AppIdle);
			Application.Run(m_mainForm);
		}

		static void AppIdle(object sender, EventArgs e)
		{
			while (AppStillIdle)
			{
				NetIncomingMessage inc;
				while ((inc = m_client.ReadMessage()) != null)
				{
					switch (inc.MessageType)
					{
						case NetIncomingMessageType.VerboseDebugMessage:
						case NetIncomingMessageType.DebugMessage:
						case NetIncomingMessageType.WarningMessage:
						case NetIncomingMessageType.ErrorMessage:
							AppendText(m_mainForm.richTextBox1, inc.ReadString());
							break;
						case NetIncomingMessageType.UnconnectedData:
							if (inc.SenderEndPoint.Equals(m_masterServer))
							{
								// it's from the master server - must be a host
								var id = inc.ReadInt64();
								var hostInternal = inc.ReadIPEndPoint();
								var hostExternal = inc.ReadIPEndPoint();
		
								m_hostList[id] = new IPEndPoint[] { hostInternal, hostExternal };

								// update combo box
								m_mainForm.comboBox1.Items.Clear();
								foreach (var kvp in m_hostList)
									m_mainForm.comboBox1.Items.Add(kvp.Key.ToString() + " (" + kvp.Value[1] + ")");
							}
							break;
						case NetIncomingMessageType.NatIntroductionSuccess:
							string token = inc.ReadString();
							MessageBox.Show("Nat introduction success to " + inc.SenderEndPoint + " token is: " + token);
							break;
					}
				}
			}
		}

		public static void GetServerList(string masterServerAddress)
		{
			//
			// Send request for server list to master server
			//
			m_masterServer = new IPEndPoint(NetUtility.Resolve(masterServerAddress), CommonConstants.MasterServerPort);

			NetOutgoingMessage listRequest = m_client.CreateMessage();
			listRequest.Write((byte)MasterServerMessageType.RequestHostList);
			m_client.SendUnconnectedMessage(listRequest, m_masterServer);
		}

		public static void RequestNATIntroduction(long hostid)
		{
			if (hostid == 0)
			{
				MessageBox.Show("Select a host in the list first");
				return;
			}

			if (m_masterServer == null)
				throw new Exception("Must connect to master server first!");

			NetOutgoingMessage om = m_client.CreateMessage();
			om.Write((byte)MasterServerMessageType.RequestIntroduction);

			// write my internal ipendpoint
			IPAddress mask;
			om.Write(new IPEndPoint(NetUtility.GetMyAddress(out mask), m_client.Port));

			// write requested host id
			om.Write(hostid);

			// write token
			om.Write("mytoken");

			m_client.SendUnconnectedMessage(om, m_masterServer);
		}
		public static void AppendText(RichTextBox box, string line)
		{
			try
			{
				box.AppendText(line + Environment.NewLine);
				ScrollRichTextBox(box);
			}
			catch
			{
			}
		}

		public static void ScrollRichTextBox(RichTextBox box)
		{
			if (box == null || box.IsDisposed || box.Disposing)
				return;
			SendMessage(box.Handle, WM_VSCROLL, (IntPtr)SB_BOTTOM, IntPtr.Zero);
		}
		
		[StructLayout(LayoutKind.Sequential)]
		public partial struct PeekMsg
		{
			public IntPtr hWnd;
			public Message msg;
			public IntPtr wParam;
			public IntPtr lParam;
			public uint time;
			public System.Drawing.Point p;
		}

		[System.Security.SuppressUnmanagedCodeSecurity] // We won't use this maliciously
		[DllImport("User32.dll", CharSet = CharSet.Auto)]
		public static extern bool PeekMessage(out PeekMsg msg, IntPtr hWnd, uint messageFilterMin, uint messageFilterMax, uint flags);

		public static bool AppStillIdle
		{
			get
			{
				PeekMsg msg;
				return !PeekMessage(out msg, IntPtr.Zero, 0, 0, 0);
			}
		}
	}
}
