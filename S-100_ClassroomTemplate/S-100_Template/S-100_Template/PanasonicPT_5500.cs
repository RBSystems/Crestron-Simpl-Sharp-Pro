using System;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharpPro.DeviceSupport;
using System.Text;
using System.Collections.Generic;
using Crestron.SimplSharpPro.DM;
using Crestron.SimplSharp.CrestronIO;
using System.ComponentModel;


namespace S_100_Template
{
    public class PT_DW5500 : IBasicProjector
    {

        #region Constructor

        public PT_DW5500(ComPort paraComPort, bool paramRegisterComPort)
        {
            CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(CrestronEnvironment_ProgramStatusEventHandler);
            _com = paraComPort;

            if (paramRegisterComPort)
            {
                if (_com.Register() == eDeviceRegistrationUnRegistrationResponse.Success)
                    CrestronConsole.PrintLine("comport registered ok");
                else
                    throw new NotRegisteredException(_com.DeviceRegistrationFailureString); 
            }

            _com.SerialDataReceived += new ComPortDataReceivedEvent(_com_SerialDataReceived);
            _com.SetComPortSpec(ComPort.eComBaudRates.ComspecBaudRate9600,
                                ComPort.eComDataBits.ComspecDataBits8,
                                ComPort.eComParityType.ComspecParityNone,
                                ComPort.eComStopBits.ComspecStopBits1,
                                ComPort.eComProtocolType.ComspecProtocolRS232,
                                ComPort.eComHardwareHandshakeType.ComspecHardwareHandshakeNone,
                                ComPort.eComSoftwareHandshakeType.ComspecSoftwareHandshakeNone,
                                false);

            RxHandler = new Thread(ProcessRxData, null, Thread.eThreadStartOptions.Running);
            PollTimer = new CTimer(PollCallback, null, Timeout.Infinite, Timeout.Infinite);
            RxWaitTimer = new CTimer(RxWaitCallback, null, Timeout.Infinite, Timeout.Infinite);

            LastCommand = eCommands.Idle;
            _ready = true;
            _frequency = 5000;
            PollTimer.Reset(500);
            pollcounter = 1;
            _lampHour1 = 0;
            _lampHour2 = 0;
            lastLampQueried = 1;
        }
        #endregion

        #region Private members

        private enum eCommands
        {
            Idle,
            PowerOn,
            PowerOff,
            PowerQuery,
            //InputQuery,
            LampQuery,
            LampHour1Query,
            LampHour2Query,
            InputDVI,
            InputRGB1,
            VideoMuteOn,
            VideoMuteOff
        }

        private readonly Dictionary<eCommands, string> CommandStrings = new Dictionary<eCommands, string>()
		{ 
            { eCommands.PowerOn, stx + "ADZZ;PON" + etx },
            { eCommands.PowerOff, stx + "ADZZ;POF" + etx },
			{ eCommands.PowerQuery, stx + "ADZZ;QPW" + etx },
			//{ eCommands.InputQuery, stx + "ADZZ;Q$S" + etx },
			{ eCommands.LampQuery, stx + "ADZZ;Q$S" + etx },
			{ eCommands.InputDVI, stx + "ADZZ;IIS:DVI" + etx },
            { eCommands.InputRGB1, stx + "ADZZ;IIS:RG1" + etx },
			{ eCommands.VideoMuteOn, stx + "ADZZ;OSH:1" + etx },
			{ eCommands.VideoMuteOff, stx + "ADZZ;OSH:0" + etx },
			{ eCommands.LampHour1Query, stx + "ADZZ;Q$L:1" + etx },
			{ eCommands.LampHour2Query, stx + "ADZZ;Q$L:2" + etx }
		};

        private CrestronQueue<eCommands> TxQueue = new CrestronQueue<eCommands>();
        private CrestronQueue<string> RxQueue = new CrestronQueue<string>();

        private Thread RxHandler;

        private ComPort _com;

        private bool _ready;

        #endregion

        #region System
        void CrestronEnvironment_ProgramStatusEventHandler(eProgramStatusEventType programEventType)
        {
            switch (programEventType)
            {
                case eProgramStatusEventType.Stopping:
                    RxQueue.Enqueue(null);
                    break;

                default:
                    break;
            }
        }

        #endregion

        #region Tx/Rx Handling

        private CTimer RxWaitTimer;

        private void RxWaitCallback(object o)
        {
            if (TxQueue.Count == 0)
            {
                LastCommand = eCommands.Idle;
                CrestronConsole.PrintLine("rx timeout, nothing in queue");
            }
            else
            {
                LastCommand = TxQueue.Dequeue();
                CrestronConsole.PrintLine("rx timeout, sending {0}", LastCommand.ToString());

                //if (LastCommand == eCommands.VolumeChange)
                //{
                //    _com.Send(GetVolumeString());
                //    RxWaitTimer.Reset(500);
                //}
                //else
                //{
                    _com.Send(CommandStrings[LastCommand]);
                    RxWaitTimer.Reset(500);
                //}
            }
        }

        private eCommands LastCommand;

        private void TxCommand(eCommands cmd)
        {
            if (!_ready)
                throw new NotRegisteredException("This object was not fully initialized");

            if (LastCommand == eCommands.Idle)
            {
                CrestronConsole.PrintLine("sending {0}", cmd.ToString());

                LastCommand = cmd;

                switch (cmd)
                {
                    case eCommands.PowerOn:
                    case eCommands.PowerOff:
                    case eCommands.PowerQuery:
                    //case eCommands.InputQuery:
                    case eCommands.LampQuery:
                    case eCommands.InputDVI:
                    case eCommands.InputRGB1:
                    case eCommands.VideoMuteOn:
                    case eCommands.VideoMuteOff:
                    case eCommands.LampHour1Query:
                    case eCommands.LampHour2Query:
                        _com.Send(CommandStrings[cmd]);
                        RxWaitTimer.Reset(500);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                CrestronConsole.PrintLine("adding {0} to txqueue", cmd.ToString());
                TxQueue.Enqueue(cmd);
            }
        }

        private void ProcessResponse(string msg)
        {
            //CrestronConsole.PrintLine("msg length is {0}, and lastlamp is {1}",msg.Length,lastLampQueried);
            if (msg.Contains("000") || msg.Contains("POF"))     //projector is off and not warming or cooling
            {
                _isOn = false;
                if (PowerEvent != null)
                    PowerEvent(this, _isOn, _isWarming, _isCooling);
            }
            else if (msg.Contains("001") || msg.Contains("PON"))     //projector is on and not warming or cooling
            {
                _isOn = true;
                if (PowerEvent != null)
                    PowerEvent(this, _isOn, _isWarming, _isCooling);
            }
            else if (msg.Contains("OSH:1"))                 // Video is Muted
            {
                _isVideoMuted = true;
                if (VideoMuteEvent != null)
                    VideoMuteEvent(this, _isVideoMuted);
            }
            else if (msg.Contains("OSH:0"))                  // Video is Unmuted
            {
                _isVideoMuted = false;
                if (VideoMuteEvent != null)
                    VideoMuteEvent(this, _isVideoMuted);
            }
            else if (msg.Contains("0") && (msg.Length == 2))      //off and not cooling       
            {
                //CrestronConsole.PrintLine("Proj off and not cooling");
                _isWarming = false;
                _isCooling = false;
                _isOn = false;
                if (PowerEvent != null)
                    PowerEvent(this, _isOn, _isWarming, _isCooling);
            }
            else if (msg.Contains("1") && (msg.Length == 2))      //warming 
            {
                //CrestronConsole.PrintLine("Proj warming");
                _isWarming = true;
                _isCooling = false;
                _isOn = true;
            }
            else if (msg.Contains("2") && (msg.Length == 2))      //on and not warming
            {
                //CrestronConsole.PrintLine("Proj on and not warming");
                _isWarming = false;
                _isCooling = false;
                _isOn = true;
                if (PowerEvent != null)
                    PowerEvent(this, _isOn, _isWarming, _isCooling);
            }
            else if (msg.Contains("3") && (msg.Length == 2))      //cooling
            {
                //CrestronConsole.PrintLine("Code has reached Proj cooling");
                _isWarming = false;
                _isCooling = true;
                _isOn = false;
                if (PowerEvent != null)
                    PowerEvent(this, _isOn, _isWarming, _isCooling);
            }
            else if ((msg.Length == 5) && (lastLampQueried == 1))    //get lamp hour 1       
            {
                //CrestronConsole.PrintLine("Code has reached parsing lamp 1");
                string tempString = null;
                tempString = msg.Substring(1, 4);
                _lampHour1 = ushort.Parse(tempString);
                if (LampEvent != null)
                    LampEvent(this, _lampHour1, _lampHour2);
            }
            else if ((msg.Length == 5) && (lastLampQueried == 2))   //lamp hour 2           
            {
                //CrestronConsole.PrintLine("Code has reached parsing lamp 2");
                string tempString = null;
                tempString = msg.Substring(1, 4);
                _lampHour2 = ushort.Parse(tempString);
                if (LampEvent != null)
                    LampEvent(this, _lampHour1, _lampHour2);
            }                    
          RxWaitTimer.Stop();
          //CrestronConsole.PrintLine("Code has reached the end of parsing");
        }

        private object ProcessRxData(object obj)
        {
            //CrestronConsole.PrintLine("RxHandler thread {ID# {0}) is running", Thread.CurrentThread.ManagedThreadId);
            StringBuilder RxData = new StringBuilder();
            int Pos = -1;

            String MatchString = String.Empty;

            // the Dequeue method will wait, making this an acceptable
            // while (true) implementation.
            while (true)
            {
                try
                {
                    // removes string from queue, blocks until an item is queued
                    string tmpString = RxQueue.Dequeue();

                    if (tmpString == null)
                        return null; // terminate the thread

                    RxData.Append(tmpString); //Append received data to the COM buffer
                    MatchString = RxData.ToString();
                    //CrestronConsole.PrintLine("matchstring is {0}", MatchString);
                    //find the delimiter
                    Pos = MatchString.IndexOf(Convert.ToChar("\x03"));
                    //Pos = MatchString.IndexOf(Convert.ToChar(etx));
                    if (Pos >= 0)
                    {
                        // delimiter found
                        // create temporary string with matched data.
                        CrestronConsole.PrintLine("we achieved delimeter {0}, length {1}",MatchString,MatchString.Length);
                        MatchString = MatchString.Substring(0, Pos);
                        RxData.Remove(0, Pos + 1); // remove data from COM buffer
                        // parse data here
                        //CrestronConsole.PrintLine("Matchstrng is {0}", MatchString);
                        ProcessResponse(MatchString);
                    }
                }
                catch (Exception ex)
                {
                    CrestronConsole.PrintLine("Exception in thread: {0}\n\r{1}", ex.Message, ex.StackTrace);
                }

                if (TxQueue.IsEmpty)
                {
                    LastCommand = eCommands.Idle;
                    //CrestronConsole.PrintLine("Last Command is second {0}, postion is {1}", LastCommand, Pos);
                }
                else
                {
                    _com.Send(CommandStrings[LastCommand]);
                    RxWaitTimer.Reset(500);                    
                }
            }
        }

        void _com_SerialDataReceived(ComPort ReceivingComPort, ComPortSerialDataEventArgs args)
        {
            RxQueue.Enqueue(args.SerialData);
            //CrestronConsole.PrintLine("Serial data recieved");
        }

        #endregion

        #region Controls and Parameters

        private eVideoInputs _input;
        private bool _isOn;
        private ushort _lampHour1, _lampHour2;
        private bool _isVideoMuted;
        private bool _isWarming;
        private bool _isCooling;
        private object _lock;
        private static string stx = "\x02";
        private static string etx = "\x03";
        private byte pollcounter, lastLampQueried;


        public void PowerControl(bool enable)
        {
            if (enable)
                TxCommand(eCommands.PowerOn);
            else
                TxCommand(eCommands.PowerOff);
        }


        #region IBasicProjector Members

        public void PowerOn()
        {
            TxCommand(eCommands.PowerOn);
        }

        public void PowerOff()
        {
            TxCommand(eCommands.PowerOff);
        }

        public void PollPowerAndLamp()
        {
            TxCommand(eCommands.LampQuery);
        }

        public void VideoMuteOn()
        {
            TxCommand(eCommands.VideoMuteOn);
        }

        public void VideoMuteOff()
        {
            TxCommand(eCommands.VideoMuteOff);
        }


        public eVideoInputs Input
        {
            get
            {
                return _input;
            }
            set
            {
                switch (value)
                {
                    case eVideoInputs.Dvi1:
                        _input = eVideoInputs.Dvi1;
                        TxCommand(eCommands.InputDVI);
                        break;

                    default:
                        throw new InvalidInputException("Invalid input selection", value);
                }

            }
        }

        public eVideoInputs[] UsableInputs
        {
            get { return new eVideoInputs[] { eVideoInputs.Dvi1, eVideoInputs.Vga1 }; }
        }

        public bool IsOn
        {
            get { return _isOn; }
        }

        public bool IsVideoMuted
        {
            get { return _isVideoMuted; }
        }

        public ushort LampHour1
        {
            get { return _lampHour1; }
        }

        public ushort LampHour2
        {
            get { return _lampHour2; }
        }

        public bool IsWarming
        {
            get { return _isWarming; }
            set
            {
                _isWarming = value;
                OnDoneWarming();
            }
        }

        protected virtual void OnDoneWarming
        {
            if(DoneWarmingEvent != null)
                DoneWarmingEvent(this,_isWarming);
        }

        public bool IsCooling
        {
            get { return _isCooling; }
        }


        public event PowerChangeHandler PowerEvent;

        public event VideoMuteChangeHandler VideoMuteEvent;

        public event LampHourChangeHandler LampEvent;

        public event InputChangeHandler InputEvent;

        public event DoneWarmingHandler DoneWarmingEvent;

        #endregion

        #endregion

        #region Polling

		private CTimer PollTimer;

		private void PollCallback(object obj)
		{
			if (!_ready)
				throw new NotRegisteredException("This object was not fully initialized");

			Poll();
            //CrestronConsole.PrintLine("Just polled");

			PollTimer.Reset(_frequency);
		}


		#region IPollable Members


		public void Poll()
		{

			if (!_ready)
				throw new NotRegisteredException("This object was not fully initialized");

            pollcounter++;

            switch(pollcounter)
                {
                    case (1):
                        {
                            TxCommand(eCommands.PowerQuery);
                            break;
                        }
                    case (2):
                        {
                            TxCommand(eCommands.LampQuery);
                            break;
                        }
                    //case (3):
                    //    if (!_isOn)
                    //    {
                    //        TxCommand(eCommands.LampHour1Query);
                    //        lastLampQueried = 1;
                    //    }
                    //    break;

                    //case (4):
                    //    if (!_isOn)
                    //    {
                    //        TxCommand(eCommands.LampHour2Query);
                    //        lastLampQueried = 2;
                    //    }
                        //break;

                    default:
                        {
                            pollcounter = 0;
                            break;
                        }

                }
		}

        private bool _poll;
        public bool PollDevice
        {
            get
            {
                return _poll;
            }
            set
            {
                _poll = value;

                if (value)
                    PollTimer.Reset(0);
                else
                    PollTimer.Stop();

                if (PollingChangeEvent != null)
                    PollingChangeEvent(this, new PollingEventArgs(_poll, _frequency));
            }
        }

        public event OnPollingChange PollingChangeEvent;

        private ushort _frequency;
        public ushort PollFrequency
        {
            get { return _frequency; }
            set { _frequency = value; }
        }

		#endregion

		#endregion


        #region INotifyPropertyChanged Members

        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
        {
            add { throw new NotImplementedException(); }
            remove { throw new NotImplementedException(); }
        }

        #endregion
    }
}