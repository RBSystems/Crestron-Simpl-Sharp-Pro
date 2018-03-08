using System;

namespace S_100_Template
{
    public interface IPollable
    {
        void Poll();

        bool PollDevice { get; set; }
    }

    public delegate void OnPollingChange(IPollable sender, PollingEventArgs args);

    public class PollingEventArgs : EventArgs
    {
        public PollingEventArgs(bool paramIsEnabled, ushort paramFrequency)
        {
            IsEnabled = paramIsEnabled;
            Frequency = paramFrequency;
        }

        public bool IsEnabled { get; private set; }
        public ushort Frequency { get; private set; }
    }
}