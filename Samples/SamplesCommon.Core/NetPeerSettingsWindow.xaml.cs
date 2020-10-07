using System;
using System.Text;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Lidgren.Network;
using ReactiveUI;

namespace SamplesCommon.Core
{
    public class NetPeerSettingsWindow : Window
    {
        private readonly NetPeer _peer;
        private readonly DispatcherTimer _timer;

        private readonly CheckBox _debugCheckBox;
        private readonly CheckBox _verboseCheckBox;
        private readonly TextBox _minLagBox;
        private readonly Button _closeButton;
        private readonly TextBox _pingBox;
        private readonly Button _saveButton;
        private readonly TextBlock _statistics;
        private readonly TextBlock _duplicatesDisplay;
        private readonly TextBox _duplicatesBox;
        private readonly TextBlock _lossDisplay;
        private readonly TextBox _lossBox;
        private readonly TextBlock _delayDisplay;
        private readonly TextBox _maxLagBox;

        public NetPeerSettingsWindow()
        {
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            _timer.Stop();
        }

        private void UpdateStatistics(object sender, EventArgs e)
        {
            var bdr = new StringBuilder();
            bdr.AppendLine(_peer.Statistics.ToString());

            if (_peer.ConnectionsCount > 0)
            {
                var conn = _peer.Connections[0];
                bdr.AppendLine("Connection 0:");
                bdr.Append(conn.Statistics.ToString());
            }

            _statistics.Text = bdr.ToString();
        }

        public NetPeerSettingsWindow(NetPeer peer)
        {
            InitializeComponent();

            _peer = peer;
            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.ApplicationIdle,
                (sender, e) => UpdateStatistics());
            _timer.Start();

            _debugCheckBox = this.FindControl<CheckBox>("DebugCheckBox");
            _verboseCheckBox = this.FindControl<CheckBox>("VerboseCheckBox");
            _minLagBox = this.FindControl<TextBox>("MinLag");
            _maxLagBox = this.FindControl<TextBox>("MaxLag");
            _delayDisplay = this.FindControl<TextBlock>("DelayDisplay");
            _lossBox = this.FindControl<TextBox>("LossBox");
            _lossDisplay = this.FindControl<TextBlock>("LossPercent");
            _duplicatesBox = this.FindControl<TextBox>("DuplicatesBox");
            _duplicatesDisplay = this.FindControl<TextBlock>("DuplicatesPercent");
            _statistics = this.FindControl<TextBlock>("Statistics");
            _closeButton = this.FindControl<Button>("CloseButton");
            _saveButton = this.FindControl<Button>("SaveButton");
            _pingBox = this.FindControl<TextBox>("PingBox");

            _saveButton.Command = ReactiveCommand.Create(SaveButtonPressed);
            _closeButton.Command = ReactiveCommand.Create(CloseButtonPressed);

            UpdateLabelsAndBoxes();
        }

        private void UpdateStatistics()
        {
            var bdr = new StringBuilder();
            bdr.AppendLine(_peer.Statistics.ToString());

            if (_peer.ConnectionsCount > 0)
            {
                var conn = _peer.Connections[0];
                bdr.AppendLine("Connection 0:");
                bdr.Append(conn.Statistics);
            }

            _statistics.Text = bdr.ToString();
        }

        private void CloseButtonPressed()
        {
            Close();
        }

        private void SaveButtonPressed()
        {
            Save();
            UpdateLabelsAndBoxes();
            UpdateStatistics();
        }

        private void Save()
        {
            _peer.Configuration.SetMessageTypeEnabled(NetIncomingMessageType.DebugMessage,
                _debugCheckBox.IsChecked.Value);
            _peer.Configuration.SetMessageTypeEnabled(NetIncomingMessageType.VerboseDebugMessage,
                _verboseCheckBox.IsChecked.Value);
#if DEBUG
            if (float.TryParse(_lossBox.Text, out var f))
                _peer.Configuration.SimulatedLoss = f / 100f;
            if (float.TryParse(_duplicatesBox.Text, out f))
                _peer.Configuration.SimulatedDuplicatesChance = f / 100f;
            if (float.TryParse(_minLagBox.Text, out f))
                _peer.Configuration.SimulatedMinimumLatency = f / 1000f;
            if (float.TryParse(_pingBox.Text, out f))
                _peer.Configuration.PingInterval = f / 1000f;
            if (float.TryParse(_maxLagBox.Text, out var max))
            {
                max /= 1000f;
                var r = max - _peer.Configuration.SimulatedMinimumLatency;
                if (r > 0)
                {
                    _peer.Configuration.SimulatedRandomLatency = r;
                    var nm = _peer.Configuration.SimulatedMinimumLatency +
                             _peer.Configuration.SimulatedRandomLatency;
                    _maxLagBox.Text = ((int) (max * 1000)).ToString();
                }
            }
#endif
        }

        private void UpdateLabelsAndBoxes()
        {
            var pc = _peer.Configuration;

#if DEBUG
            var loss = (pc.SimulatedLoss * 100.0f).ToString();
            _lossDisplay.Text = $"{loss} %";
            _lossBox.Text = loss;

            var dupes = (pc.SimulatedDuplicatesChance * 100.0f).ToString();
            _duplicatesDisplay.Text = $"{dupes} %";
            _duplicatesBox.Text = dupes;

            var minLat = (pc.SimulatedMinimumLatency * 1000.0f).ToString();
            var maxLat = ((pc.SimulatedMinimumLatency + pc.SimulatedRandomLatency) * 1000.0f).ToString();
#else
			var minLat = "";
			var maxLat = "";
#endif
            _delayDisplay.Text = $"{minLat} to {maxLat} ms";
            _minLagBox.Text = minLat;
            _minLagBox.Text = maxLat;

            _debugCheckBox.IsChecked = _peer.Configuration.IsMessageTypeEnabled(NetIncomingMessageType.DebugMessage);
            _verboseCheckBox.IsChecked =
                _peer.Configuration.IsMessageTypeEnabled(NetIncomingMessageType.VerboseDebugMessage);
            _pingBox.Text = (_peer.Configuration.PingInterval * 1000).ToString();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}