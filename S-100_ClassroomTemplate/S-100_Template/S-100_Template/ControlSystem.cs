using System;
using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support
using Crestron.SimplSharpPro.GeneralIO;                 // For General I/O support
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharpPro.Keypads;
using Crestron.SimplSharpPro.UI;
using Crestron.SimplSharpPro.DM.Endpoints.Receivers;
using System.Collections.Generic;                       // For Crestron I/O support
using System.ComponentModel;
using Crestron.SimplSharpPro.Fusion;                    //Fusion control    
using Crestron.SimplSharp.CrestronXmlLinq;              // Linq
using Crestron.SimplSharpPro.DM;                        // DM support

namespace S_100_Template
{
    public class ControlSystem : CrestronControlSystem
    {
        //******************** Constants ********************\\
        private const string CONFIG_FILE_DIRECTORY = "\\User\\";
        private const string CONFIG_FILE_EXTENSION = "*.dat";
        private const string ProgramVersion = "v0.15";
        private const string ProgramDate = "Nov 16th, 2017";

        private const uint KEYPAD_ID = 0x25;// desk testing is 04, S-159 is 25
        private const uint FUSION_ID = 0x0A;
        //private const uint OCCSENSOR_ID = 0x97;
        //private const uint FUSION_ID = 0x03;
        private const uint XPANEL_ID = 0x03;
        private Relay[] myRelays;
        //private const uint VIDEO_SWITCH_ID = 0x0a;


        //******************** Local Variables ********************\\
        public FusionRoom RV;
        public GlsOirCCn OccSensor;
        public C2nCbdP Keypad;
        public XpanelForSmartGraphics Xpanel;
        public DmRmc200C DmScaler;
        public IBasicProjector Projector;
        ConfigLoader<S_100ConfigData> RoomConfig;
        private eSystemStatus SystemStatus;
        private ushort _volume;
        private byte keyNum;
        //private ushort _newVol;
        private ushort scaledVol;
        //private DMInputEventHandler DmScalarInput; 
        //private CrestronControlSystem RMC3;

        /// <summary>
        /// Delegate for a volume change 
        /// </summary>
        /// <param name="volume">volume device raising this event.</param>
        public delegate void VolumeChangeHandler(object source, ushort volume);

        public event VolumeChangeHandler VolumeScaleEvent;

        /// <summary>
        /// Map of possible start up errors, to the corresponding keypad LED to alert the user
        /// Example: if there is no *.dat file in the \User\ directory at startup, the bottom LED (#6) will blink
        /// </summary>
        private Dictionary<eSystemStatus, uint> LedErrorCodeFbs = new Dictionary<eSystemStatus, uint>()
		{
			{ eSystemStatus.VideoSwitchOffline, 1 },
			{ eSystemStatus.InvalidVideoSwitchAddress, 2 },
			{ eSystemStatus.LoadError, 3 },
			{ eSystemStatus.ConfigError, 4 },
			{ eSystemStatus.MultipleConfigs, 5 },
			{ eSystemStatus.NoConfig, 6 }
		};

        /// <summary>
        /// Timer used for keypad blinking feedback when projector warming or cooling.
        /// </summary>
        //private CTimer KeypadBlinkTimer;
        private const long KEYPAD_RAMP_SPEED = 500;

        //******************** Enums ********************\\
        /// <summary>
        /// Possible system status codes
        /// </summary>
        private enum eSystemStatus : byte
        {
            /// <summary>
            /// No config file was found.
            /// </summary>
            NoConfig,

            /// <summary>
            /// Error loading the config file.
            /// </summary>
            ConfigError,

            /// <summary>
            /// Error loading the config file.
            /// </summary>
            LoadError,

            /// <summary>
            /// Multiple config files were found during start up.
            /// </summary>
            MultipleConfigs,

            /// <summary>
            /// Config file has an invalid ipaddress for the external video switch.
            /// </summary>
            InvalidVideoSwitchAddress,

            /// <summary>
            /// Config loaded ok, system running as expected.
            /// </summary>
            Running,

            /// <summary>
            /// Program is running, but the switch is currently offline.
            /// </summary>
            VideoSwitchOffline
        }

        private enum eKeypadButtons : byte
        {
            ProjOnButtonAndFb1 = 1,
            ProjOnButtonAndFb2 = 2,
            ProjOffButtonAndFb3 = 3,
            ProjOffButtonAndFb4 = 4,
            VolUpButtonAndFb5 = 5,
            VolDownButtonAndFb6 = 6
        }

        //private Dictionary<eKeypadButtons, int> S100Keypad = new Dictionary<eKeypadButtons, int>()
        //{
        //    { eKeypadButtons.ProjOnButtonAndFb, 1},
        //    { eKeypadButtons.ProjOnButtonAndFb, 2},
        //    { eKeypadButtons.ProjOffButtonAndFb, 3},
        //    { eKeypadButtons.ProjOffButtonAndFb, 4},
        //    { eKeypadButtons.VolUpButtonAndFb, 5},
        //    { eKeypadButtons.VolDownButtonAndFb, 6}
        //};

        private enum eRvBools : byte
        {            
        }

        /// <summary>
        /// Fusion attribute numbers.
        /// </summary>
        public enum eRvUshorts : byte
        {
            FusionVolume = 2,
            FusionLampHour1 = 51,
            FusionLampHour2 = 52
        }

        /// <summary>
        /// Fusion attribute numbers.
        /// </summary>
        private enum eRvStrings : byte
        {
        }

        /// <summary>
        /// Join number from xpanel project
        /// </summary>
        private static class XpanelBools
        {
            public const uint XOnandButtonFb = 2;
            public const uint XVolUp = 5;
            public const uint XVolDn = 6; 
            public const uint XOffandButtonFb = 4;
        }

        /// <summary>
        /// Join number from xpanel project
        /// </summary>
        private static class XpanelUshorts
        {
            public const uint XVolFb = 1;
        }

        /// <summary>
        /// Join number from xpanel project
        /// </summary>
        private static class XpanelStrings
        {
        }

        /// <summary>
        /// Enumeration of the possible room states.
        /// </summary>
        private eRoomStates RoomState;

        /// <summary>
        /// Method to load the config file from disk, or create it upon first boot up.
        /// </summary>
        private eSystemStatus LoadConfigFile()
        {
            string[] files;

            try
            {
                files = Directory.GetFiles(CONFIG_FILE_DIRECTORY, CONFIG_FILE_EXTENSION);
            }
            catch (Exception e)
            {
                ErrorLog.Error("Unable to read files located at '{0}'. Cause: {1}", CONFIG_FILE_DIRECTORY, e.Message);
                return eSystemStatus.LoadError;
            }

            if (files.Length == 1)
            {
                CrestronConsole.PrintLine("Single *.dat file found \"{0}\"", files[0]);

                RoomConfig = new ConfigLoader<S_100ConfigData>(files[0]);

                if (RoomConfig.Details == null)
                {
                    return eSystemStatus.ConfigError;
                }

                bool needsSave = false;

                // config file validation
                if (string.IsNullOrEmpty(RoomConfig.Details.RoomName)) { RoomConfig.Details.RoomName = "Unnamed Room"; needsSave = true; }
                if (string.IsNullOrEmpty(RoomConfig.Details.RoomGuid)) { RoomConfig.Details.RoomGuid = Guid.NewGuid().ToString(); needsSave = true; }
                if (RoomConfig.Details.OccTimeout < 5) { RoomConfig.Details.OccTimeout = 5; needsSave = true; }
                if (RoomConfig.Details.OccTimeout > 1800) { RoomConfig.Details.OccTimeout = 1800; needsSave = true; }
                if (!Enum.IsDefined(typeof(eProjectorTypes), RoomConfig.Details.DisplayType)) { RoomConfig.Details.DisplayType = eProjectorTypes.NoControl; needsSave = true; }

                if (needsSave) RoomConfig.Save();

                CrestronConsole.PrintLine("Loading config for \"{0}\"", RoomConfig.Details.RoomName);

                return eSystemStatus.Running;
            }
            else if (files.Length == 0)
            {
                CrestronConsole.PrintLine("No config file found during startup in '{0}'", CONFIG_FILE_DIRECTORY);
                ErrorLog.Notice("No config file found during startup in '{0}'", CONFIG_FILE_DIRECTORY);

                return eSystemStatus.NoConfig;
            }
            else
            {
                CrestronConsole.PrintLine("More than one config file found in '{0}'. Cannot determine which to use.", CONFIG_FILE_DIRECTORY);
                ErrorLog.Error("More than one config file found in '{0}'. Cannot determine which to use.", CONFIG_FILE_DIRECTORY);

                return eSystemStatus.MultipleConfigs;
            }
        }

        /// <summary>
        /// Callback method to call volume change method on the display.
        /// </summary>
        private CTimer VolumeRampTimer;

        /// <summary>
        /// Value for how fast the volume change method should be called (333 = 3x a second, 100 = 10x a second).
        /// </summary>
        private const long VOLUME_RAMP_SPEED = 100;

        /// <summary>
        /// Callback method to invoke the volume change event based on which direction (up or down) is being presses.
        /// </summary>
        /// <param name="obj">Not used.</param>
        private void VolumeRamp(object obj)
        {
            VolumeRampAction.Invoke();
            if (VolumeScaleEvent != null)
            {
                VolumeScaleEvent(this, _volume);
            }
            CrestronConsole.PrintLine("Volume is {0}, scaled is {1}", _volume, scaledVol);
            VolumeRampTimer.Reset(VOLUME_RAMP_SPEED);
        }

        /// <summary>
        /// Action to encapsulate the appropriate volume change direction.
        /// </summary>
        Action VolumeRampAction;

        public ushort DmVolume
        {
            get { return _volume; }
        }

        public ushort MaxVolume
        {
            get { return 100; }
        }

        public ushort MinVolume
        {
            get { return 0; }
        }

        public void VolUp()
        {
            if (_volume < MaxVolume)
            {
                ++_volume;
            }
        }

        public void VolDown()
        {
            if (_volume > MinVolume)
            {
                --_volume;
            }
        }

        /// <summary>
        /// ControlSystem Constructor. Starting point for the SIMPL#Pro program.
        /// Use the constructor to:
        /// * Initialize the maximum number of threads (max = 400)
        /// * Register devices
        /// * Register event handlers
        /// * Add Console Commands
        /// 
        /// Please be aware that the constructor needs to exit quickly; if it doesn't
        /// exit in time, the SIMPL#Pro program will exit.
        /// 
        /// You cannot send / receive data in the constructor
        /// </summary>
        public ControlSystem()
            : base()
        {
            try
            {
                Thread.MaxNumberOfUserThreads = 20;
                SystemStatus = LoadConfigFile();

                //Subscribe to the controller events (System, Program, and Ethernet)
                CrestronEnvironment.SystemEventHandler += new SystemEventHandler(ControlSystem_ControllerSystemEventHandler);
                CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(ControlSystem_ControllerProgramEventHandler);
                CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(ControlSystem_ControllerEthernetEventHandler);
                
                #region VersiPort
                if (this.SupportsVersiport)
                {
                    for (uint i = 1; i <= 2; i++)
                    {
                        if (this.VersiPorts[i].Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                            ErrorLog.Error("Error Registering Versiport 1: {0}", this.VersiPorts[i].DeviceRegistrationFailureReason);
                        else
                            this.VersiPorts[i].SetVersiportConfiguration(eVersiportConfiguration.DigitalOutput);
                    }
                }
                #endregion
                #region Relay
                if (this.SupportsRelay)
                {
                    // Create a new array sized for the relays on the controller + 1 as relays are 1 based not 0
                    myRelays = new Relay[this.RelayPorts.Count + 1];
                    // Register each relay in the control system
                    for (uint i = 1; i <= this.RelayPorts.Count; i++)
                    {
                        myRelays[i] = RelayPorts[i];
                        if (myRelays[i].Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                            ErrorLog.Error("Error Registering Relay {0}: {1}", myRelays[i].ID, myRelays[i].DeviceRegistrationFailureReason);
                    }
                }
                #endregion
            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in the constructor: {0}", e.Message);
            }
        }

        		/// <summary>
		/// Method to handle keypad events after a boot up error.
		/// </summary>
		/// <param name="device">Reference to the device raising this event.</param>
		/// <param name="args">Information about the event being raised.</param>
		void Keypad_ButtonStateChangeAfterStartUpError(GenericBase device, ButtonEventArgs args)
		{
			if (args.Button == Keypad.Button[(uint)eKeypadButtons.ProjOffButtonAndFb3] && args.NewButtonState == eButtonState.DoubleTapped)
            //if (args.Button == Keypad.Button[(uint)S100Keypad[eKeypadButtons.ProjOffButtonAndFb]] && args.NewButtonState == eButtonState.DoubleTapped)
			{
				string res = string.Empty;
				CrestronConsole.SendControlSystemCommand("progreset -p:1", ref res);
			}
		}


        /// <summary>
        /// InitializeSystem - this method gets called after the constructor 
        /// has finished. 
        /// 
        /// Use InitializeSystem to:
        /// * Start threads
        /// * Configure ports, such as serial and verisports
        /// * Start and initialize socket connections
        /// Send initial device configurations
        /// 
        /// Please be aware that InitializeSystem needs to exit quickly also; 
        /// if it doesn't exit in time, the SIMPL#Pro program will exit.
        /// </summary>
        public override void InitializeSystem()
        {
            switch (SystemStatus)
            {
                case eSystemStatus.NoConfig:
                case eSystemStatus.ConfigError:
                case eSystemStatus.LoadError:
                case eSystemStatus.MultipleConfigs:
                case eSystemStatus.InvalidVideoSwitchAddress:
                    Keypad = new C2nCbdP(KEYPAD_ID, this);
                    Keypad.ParameterBargraphTimeout = SimplSharpDeviceHelper.SecondsToUshort(2f);
                    Keypad.ParameterDblTapSpeed = SimplSharpDeviceHelper.SecondsToUshort(0.5f);
                    Keypad.ParameterHoldTime = SimplSharpDeviceHelper.SecondsToUshort(0.5f);
                    Keypad.ButtonStateChange += new ButtonEventHandler(Keypad_ButtonStateChangeAfterStartUpError);
                    //ComPort displayCom = this.ComPorts[1];
                    //bool registerCom = true;
                    if (Keypad.Register() == eDeviceRegistrationUnRegistrationResponse.Success)
                        CrestronConsole.PrintLine("Keypad regisitered OKAY");
                    else
                        CrestronConsole.PrintLine("Could not register Keypad. Cause: {0}", Keypad.RegistrationFailureReason);

                    Keypad.Feedbacks[LedErrorCodeFbs[SystemStatus]].BlinkPattern = eButtonBlinkPattern.MediumBlip;
                    Keypad.Feedbacks[LedErrorCodeFbs[SystemStatus]].State = true;
                    break;
                case eSystemStatus.Running:
                    //Xpanel = new XpanelForSmartGraphics(XPANEL_ID, this);
                    //Xpanel.Description = "Tech support xpanel";
                    //Xpanel.SigChange += new SigEventHandler(Xpanel_SigChange);
                    //Xpanel.OnlineStatusChange += new OnlineStatusChangeEventHandler(Xpanel_OnlineStatusChange);
                    //if (Xpanel.Register() == eDeviceRegistrationUnRegistrationResponse.Success)
                    //    CrestronConsole.PrintLine("Xpanel registered OKAY");
                    //else
                    //    CrestronConsole.PrintLine("Could not register the xpanel. Cause: {0}", Xpanel.RegistrationFailureReason);
                    Keypad = new C2nCbdP(KEYPAD_ID, this);
                    Keypad.Description = "Cameo keypad";
                    ComPort displayCom = this.ComPorts[1];
                    bool registerCom = true;
                    Keypad.ParameterBargraphTimeout = SimplSharpDeviceHelper.SecondsToUshort(1.5f);
                    Keypad.ParameterDblTapSpeed = SimplSharpDeviceHelper.SecondsToUshort(0.3f);
                    Keypad.ParameterHoldTime = SimplSharpDeviceHelper.SecondsToUshort(2);
                    Keypad.ParameterWaitForDoubleTap = false;
                    Keypad.OnlineStatusChange += new OnlineStatusChangeEventHandler(CresnetDevice_OnlineStatsChange);
                    Keypad.ButtonStateChange += new ButtonEventHandler(Keypad_ButtonStateChange);
                    //KeypadBlinkTimer = new CTimer(KeypadBlinkFB, null, Timeout.Infinite, Timeout.Infinite);
                    if (Keypad.Register() == eDeviceRegistrationUnRegistrationResponse.Success)
                        CrestronConsole.PrintLine("Keypad registered OKAY and status is running");
                    else
                    CrestronConsole.PrintLine("Could not register the keypad. Cause: {0}", Keypad.RegistrationFailureReason);
                    RV = new FusionRoom(FUSION_ID, this, RoomConfig.Details.RoomName, RoomConfig.Details.RoomGuid);
                    RV.FusionStateChange += new FusionStateEventHandler(RV_FusionStateChange);
                    RV.ExtenderRoomViewSchedulingDataReservedSigs.Use();
                    RV.ExtenderRoomViewSchedulingDataReservedSigs.DeviceExtenderSigChange += new DeviceExtenderJoinChangeEventHandler(ExtenderRoomViewSchedulingDataReservedSigs_DeviceExtenderSigChange);
                    RV.OnlineStatusChange += new OnlineStatusChangeEventHandler(RV_OnlineStatusChange);
                    ScheduleQueryTimer = new CTimer(GetRoomCalendar, null, Timeout.Infinite, Timeout.Infinite);
                    AutoOnMeetingTimer = new CTimer(AutoOnCallback, null, Timeout.Infinite, Timeout.Infinite);
                    VolumeRampTimer = new CTimer(VolumeRamp, null, Timeout.Infinite, Timeout.Infinite);
                    _volume = 35;
                    //Crestron.SimplSharpPro.DM.Cards.Card.Dmps3HdmiInputWithoutAnalogAudio CfmInput;
                    //Crestron.SimplSharp.EthernetAdapterType.EthernetLANAdapter rmc3EthOutput;
                    DmScaler = new DmRmc200C(14,this);
                    switch (RoomConfig.Details.DisplayType)
                    {
                        case eProjectorTypes.PanasonicPT_DW5500:
                            Projector = new PT_DW5500(displayCom, registerCom);
                            //CrestronConsole.PrintLine("Panasonic PT-DW5500 Projector is detected");
                            break;

                        default:
                            CrestronConsole.PrintLine("The config file contains an invalid display type. No Projector controls will be available.");
                            Projector = null;
                            break;
                    }

                    AddFusionSigs(); // Add Fusion Signals and generate RVI file

                    CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(CrestronEnvironment_EthernetEventHandler);

                    // subscribe to all display events
                    try
                    {
                        Projector.PowerEvent += new PowerChangeHandler(Projector_PowerEvent);
                    }
                    catch (NotImplementedException e)
                    {
                        CrestronConsole.PrintLine(e.Message);
                    }

                    catch (Exception e)
                    {
                        CrestronConsole.PrintLine("Unexpected exception while subscribing to Projector events: {0}", e.Message);
                    }

                    try
                    {
                        Projector.VideoMuteEvent += (x, y) => { CrestronConsole.PrintLine("Projector is {0}", y ? "Video is muted" : "Video is unmuted"); };
                    }
                    catch (NotImplementedException e)
                    {
                        CrestronConsole.PrintLine(e.Message);
                    }
                    catch (Exception e)
                    {
                        CrestronConsole.PrintLine("Unexpected exception while subscribing to display events: {0}", e.Message);
                    }

                    try
                    {
                        Projector.InputEvent += (x, y) => { CrestronConsole.PrintLine("Projector input is {0}", ((eVideoInputs)y).ToString()); };
                    }
                    catch (NotImplementedException e)
                    {
                        CrestronConsole.PrintLine(e.Message);
                    }
                    catch (Exception e)
                    {
                        CrestronConsole.PrintLine("Unexpected exception while subscribing to display events: {0}", e.Message);
                    }

                    try
                    {
                        //Projector.LampEvent += (x, y) => { CrestronConsole.PrintLine("Projector input is {0}", ((eVideoInputs)y).ToString()); };
                        Projector.LampEvent += new LampHourChangeHandler(Projector_LampEvent);
                    }
                    catch (NotImplementedException e)
                    {
                        CrestronConsole.PrintLine(e.Message);
                    }

                    try
                    {
                        VolumeScaleEvent += new VolumeChangeHandler(Scaled_VolumeEvent);
                    }
                    catch (NotImplementedException e)
                    {
                        CrestronConsole.PrintLine(e.Message);
                    }
                    try
                    {
                        Projector.DoneWarmingEvent += new DoneWarmingHandler(Projector_DoneWarmingEvent);
                    }
                    catch (NotImplementedException e)
                    {
                        CrestronConsole.PrintLine(e.Message);
                    }
                    catch (Exception e)
                    {
                        CrestronConsole.PrintLine("Unexpected exception while subscribing to display events: {0}", e.Message);
                    }
                    break;
            }
        }

        /// <summary>
        /// Method to handle events from Fusion.
        /// </summary>
        /// <param name="device">Reference to the FusionRoom object raising this event.</param>
        /// <param name="args">Information about the event being raised.</param>
        void RV_FusionStateChange(FusionBase device, FusionStateEventArgs args)
        {
            // determine which event has been raised.
            switch (args.EventId)
            {
                //case FusionEventIds.UserConfiguredBoolSigChangeEventId:
                //    // determin which bool sig has changed
                //    if (args.UserConfiguredSigDetail == RV.UserDefinedBooleanSigDetails[(uint)eRvBools.CfmRebootCommand])
                //    {
                //        // the signal will go true, then false. only respond to one transition of the signal
                //        //if (RV.UserDefinedBooleanSigDetails[(uint)eRvBools.CfmRebootCommand].OutputSig.BoolValue)
                //        //{
                //        //    CfmRebootCommandReceived = true;
                //        //    CfmPower.Reboot();
                //        //}
                //    }
                //    else if (args.UserConfiguredSigDetail == RV.UserDefinedBooleanSigDetails[(uint)eRvBools.LedFlash])
                //    {
                //        // the signal will go true, then false. only respond to one transition of the signal
                //        //if (RV.UserDefinedBooleanSigDetails[(uint)eRvBools.LedFlash].OutputSig.BoolValue)
                //            //ToggleLedFlash();
                //    }
                //    else if (args.UserConfiguredSigDetail == RV.UserDefinedBooleanSigDetails[(uint)eRvBools.CfmPowerStatus])
                //    {
                //        // the signal will go true, then false. only respond to one transition of the signal
                //        if (RV.UserDefinedBooleanSigDetails[(uint)eRvBools.CfmPowerStatus].OutputSig.BoolValue)
                //        {
                //            //if (CfmPower.IsPowered)
                //                //CfmPower.Off();
                //            //else
                //                //CfmPower.On();
                //        }
                //    }
                //    break;

                //case FusionEventIds.UserConfiguredUShortSigChangeEventId:
                //    // determine which ushort sig has changed
                //    if (args.UserConfiguredSigDetail == RV.UserDefinedUShortSigDetails[(uint)eRvUshorts.OccSensorTimeout])
                //    {
                //        OccSensor.RemoteTimeout.UShortValue = RV.UserDefinedUShortSigDetails[(uint)eRvUshorts.OccSensorTimeout].OutputSig.UShortValue;
                //        RoomConfig.Details.OccTimeout = RV.UserDefinedUShortSigDetails[(uint)eRvUshorts.OccSensorTimeout].OutputSig.UShortValue;
                //        RoomConfig.Save();
                //    }
                //    break;

                case FusionEventIds.SystemPowerOffReceivedEventId:
                case FusionEventIds.DisplayPowerOffReceivedEventId:
                    // respond to both events the same way
                    SetRoomState(eRoomStates.Off);
                    break;

                case FusionEventIds.SystemPowerOnReceivedEventId:
                case FusionEventIds.DisplayPowerOnReceivedEventId:
                    // respond to both events the same way
                    SetRoomState(eRoomStates.On);
                    break;

                default:
                    break;
            }
        }

        //TODO make this return the list, rather than update the class member
        //TODO use event scheduler
        /// <summary>
        /// Method to parse the xml schedule response data return from Fusion.
        /// </summary>
        /// <param name="paramData">Raw string data in xml format.</param>
        private void ProcessScheduleData(string paramData)
        {
            // clear previous list of meetings
            Meetings.Clear();

            try
            {
                // create xdocument from fusion response data
                XDocument xdoc = XDocument.Parse(paramData);
                XNamespace xns = XNamespace.None;
                var xevents = xdoc.Descendants(xns + "Event");

                // get the details for each event and add it to the meetings list
                foreach (var e in xevents)
                {
                    RvCalendarEvent tempEvent = new RvCalendarEvent();
                    tempEvent.MeetingId = e.Element("MeetingID").Value;
                    tempEvent.RvMeetingId = e.Element("RVMeetingID").Value;
                    tempEvent.Recurring = bool.Parse(e.Element("Recurring").Value);
                    tempEvent.StartTime = DateTime.Parse(e.Element("dtStart").Value);
                    tempEvent.EndTime = DateTime.Parse(e.Element("dtEnd").Value);
                    tempEvent.Organizer = e.Element("Organizer").Value;
                    tempEvent.IsEvent = ParseBoolFromString(e.Element("IsEvent").Value);
                    tempEvent.Subject = e.Element("Subject").Value;
                    tempEvent.Location = e.Element("Location").Value;

                    Meetings.Add(tempEvent);
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("ProcessScheduleData exception: {0}\n\r{1}", e.Message, e.StackTrace);
            }
        }

        /// <summary>
        /// List of meetings coming up in the next 60 minutes.
        /// </summary>
        private List<RvCalendarEvent> Meetings = new List<RvCalendarEvent>(25);

        /// <summary>
        /// Reference to the next meeting that will trigger an auto on event.
        /// </summary>
        private RvCalendarEvent NextMeeting;

        /// <summary>
        /// Timer used to turn the system on for the next meeting.
        /// </summary>
        private CTimer AutoOnMeetingTimer;

        /// <summary>
        /// Class representing a RV meeting object.
        /// </summary>
        private class RvCalendarEvent
        {
            public string MeetingId;
            public string RvMeetingId;
            public bool Recurring;
            public DateTime StartTime;
            public DateTime EndTime;
            public string Organizer;
            public bool IsEvent;
            public string Subject;
            public string Location;

            /// <summary>
            /// Gets the length of the meeting (end - start).
            /// </summary>
            public TimeSpan Durration
            {
                get { return EndTime - StartTime; }
            }

            /// <summary>
            /// Gets the number of milliseconds to the start of this event.
            /// </summary>
            public long TimeToStart
            {
                get { return (long)((StartTime - DateTime.Now).TotalMilliseconds); }
            }

            public override string ToString()
            {
                return string.Format("{0} (organized by {1}) {2} {3}", Subject, Organizer, StartTime.ToString(), Durration.ToString());
            }
        }

        /// <summary>
        /// Method to parse a bool value from a string.
        /// </summary>
        /// <param name="value">String containing "0" or "1" (0=FALSE, 1=TRUE).</param>
        /// <returns>Parsed bool value.</returns>
        private bool ParseBoolFromString(string value)
        {
            if (value == "1")
                return true;
            else if (value == "0")
                return false;
            else
                throw new FormatException("Invalid input");
        }

        /// <summary>
        /// Method to add the custom fusion sigs and generate the RVI file.
        /// </summary>
        private void AddFusionSigs()
        {
            // bools (digitals)
            //RV.AddSig(eSigType.Bool, (uint)eRvBools.CfmVideoSync, "CFM Video Sync", eSigIoMask.InputSigOnly);


            // ushorts (analogs)
            RV.AddSig(eSigType.UShort, (uint)eRvUshorts.FusionVolume, "Fusion Volume", eSigIoMask.InputOutputSig);
            RV.AddSig(eSigType.UShort, (uint)eRvUshorts.FusionLampHour1, "Lamp Hour One", eSigIoMask.InputSigOnly);
            RV.AddSig(eSigType.UShort, (uint)eRvUshorts.FusionLampHour2, "Lamp Hour Two", eSigIoMask.InputSigOnly);

            // strings (serials)
            //RV.AddSig(eSigType.String, (uint)eRvStrings.CfmVideoResolution, "CFM Video Resolution", eSigIoMask.InputSigOnly);

            // must be called if using Fusion AutoDiscovery
            //FusionRVI.GenerateFileForAllFusionDevices();
        }

        /// <summary>
        /// Callback method for the Fusion RV connection online/offline event.
        /// </summary>
        /// <param name="currentDevice">Reference to the FusionRoom connection raising this event.</param>
        /// <param name="args">Information about the event being raised.</param>
        void RV_OnlineStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
        {
            // alert via console
            CrestronConsole.PrintLine("Fusion is {0}", args.DeviceOnLine ? "online" : "offline");

            if (args.DeviceOnLine)	// if the connection is resetablished, start the periodic calendar query
                ScheduleQueryTimer.Reset(10000, SCHEDULE_QUERY_INTERVAL);

            else					// if the connection is lost, stop the periodic query
                ScheduleQueryTimer.Stop();
        }

        private CTimer ScheduleQueryTimer;
        private const long SCHEDULE_QUERY_INTERVAL = 300000;	// 5 minutes
        private const long AUTO_ON_TIME_BEFORE_MEETING_START = 300000; // 5 mins

        /// <summary>
        /// Callback method to handle FusionRoom calendar response events.
        /// </summary>
        /// <param name="currentDeviceExtender">Reference to the FusionRoom object raising this event.</param>
        /// <param name="args">Information about the event being raised.</param>
        void ExtenderRoomViewSchedulingDataReservedSigs_DeviceExtenderSigChange(DeviceExtender currentDeviceExtender, SigEventArgs args)
        {
            // determine if the event is regarding the schedule response signal
            if (args.Sig == RV.ExtenderRoomViewSchedulingDataReservedSigs.ScheduleResponse)
            {
                // print response data to the console
                //CrestronConsole.PrintLine("received:\n\r" + args.Sig.StringValue);

                // pass the response data to the xml parsing method
                ProcessScheduleData(args.Sig.StringValue);

                // determine how many meetings are upcoming
                if (Meetings.Count == 0)
                {
                    // no meetings in the next hour
                    CrestronConsole.PrintLine("No upcoming meetings found");
                    NextMeeting = null;
                }
                else if (Meetings.Count == 1)	// a single upcoming meeting was found
                {
                    // meetings in progress will have a negative TimeToStart value
                    if (Meetings[0].TimeToStart > 0)
                    {
                        CrestronConsole.PrintLine("1 upcoming meeting found: {0}", Meetings[0].ToString());

                        long turnOnTime = Meetings[0].TimeToStart < AUTO_ON_TIME_BEFORE_MEETING_START ? 0 : (Meetings[0].TimeToStart - AUTO_ON_TIME_BEFORE_MEETING_START);

                        CrestronConsole.PrintLine("\"{0}\" starts in {1}", Meetings[0].Subject, turnOnTime);
                        // start the auto on countdown
                        AutoOnMeetingTimer.Reset(turnOnTime);

                        // save reference to the meeting
                        NextMeeting = Meetings[0];
                    }
                    else
                    {
                        CrestronConsole.PrintLine("No upcoming meetings found");
                    }
                }
                else // more than one meeting found
                {
                    CrestronConsole.PrintLine("{0} upcoming meetings found.", Meetings.Count);

                    foreach (RvCalendarEvent rvce in Meetings)
                        CrestronConsole.PrintLine("    " + rvce.ToString());

                    // sort list in order of start time (soonest first)
                    Meetings.Sort((x, y) => DateTime.Compare(x.StartTime, y.StartTime));

                    // iterate through list to find the first meeting that is not already started
                    for (int i = 0; i < Meetings.Count; i++)
                    {
                        if (Meetings[i].TimeToStart > 0)
                        {
                            long turnOnTime = Meetings[i].TimeToStart < AUTO_ON_TIME_BEFORE_MEETING_START ? 0 : (Meetings[0].TimeToStart - AUTO_ON_TIME_BEFORE_MEETING_START);

                            CrestronConsole.PrintLine("\"{0}\" starts in {1}", Meetings[i].Subject, turnOnTime);

                            // start the auto on countdown
                            AutoOnMeetingTimer.Reset(turnOnTime);

                            // save reference to the meeting
                            NextMeeting = Meetings[i];
                            break;
                        }
                    }
                }
            }
        }

        private void GetRoomCalendar(object obj)
        {
            CrestronConsole.PrintLine("Sending 1-hr schedule query...");

            string rightNow = string.Format("{0}-{1}-{2}T{3:00}:{4:00}:{5:00}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second);

            RV.ExtenderRoomViewSchedulingDataReservedSigs.ScheduleQuery.StringValue = string.Format("<RequestSchedule><RequestID>1</RequestID><RoomID>{0}</RoomID><Start>{1}</Start><HourSpan>1</HourSpan></RequestSchedule>", RV.ParameterInstanceID, rightNow);
        }



        /// <summary>
        /// Method to handle Sig change events from the xpanel.
        /// </summary>
        /// <param name="currentDevice">Reference to the device raising this event.</param>
        /// <param name="args">Inforamtion about the event being raised</param>
        void Xpanel_SigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            switch (args.Sig.Type)
            {
                case eSigType.Bool:
                    if (args.Sig.BoolValue)	// press
                    {
                        // do nothing on press
                    }
                    else					// release
                    {
                        switch (args.Sig.Number)
                        {
                            case XpanelBools.XOnandButtonFb:
                                SetRoomState(eRoomStates.On);
                                break;
                            case XpanelBools.XOffandButtonFb:
                                SetRoomState(eRoomStates.Off);
                                break;
                            default:
                                break;
                        }
                    }
                    break;

                case eSigType.String:
                    switch (args.Sig.Number)
                    {
                        //case XpanelStrings.RoomNameOutput:
                        //    RoomConfig.Details.RoomName = args.Sig.StringValue;
                        //    break;

                        default:
                            break;
                    }
                    break;

                case eSigType.UShort:
                    switch (args.Sig.Number)
                    {
                        //case XpanelUshorts.SensorTimeoutSliderAndFb:
                        //    Xpanel.StringInput[XpanelStrings.SensorTimeFb].StringValue = FormatSecondsToMinutes(args.Sig.UShortValue);
                        //    break;

                        default:
                            break;
                    }
                    break;

                case eSigType.NA:
                default:
                    break;
            }
        }

        //void KeypadBlinkFB(object obj)
        //{
        //    if (keyNum == 1)
        //    {
        //        Keypad.Feedbacks[(uint)eKeypadButtons.ProjOnButtonAndFb1].State = Keypad.Feedbacks[(uint)eKeypadButtons.ProjOnButtonAndFb1].State == true ? false : true;
        //        Keypad.Feedbacks[(uint)eKeypadButtons.ProjOnButtonAndFb2].State = Keypad.Feedbacks[(uint)eKeypadButtons.ProjOnButtonAndFb2].State == true ? false : true;
        //    }
        //    else if (keyNum == 2)
        //    {
        //        Keypad.Feedbacks[(uint)eKeypadButtons.ProjOffButtonAndFb3].State = Keypad.Feedbacks[(uint)eKeypadButtons.ProjOffButtonAndFb3].State == true ? false : true;
        //        Keypad.Feedbacks[(uint)eKeypadButtons.ProjOffButtonAndFb4].State = Keypad.Feedbacks[(uint)eKeypadButtons.ProjOffButtonAndFb4].State == true ? false : true;
        //        //Keypad.Feedbacks[(uint)S100Keypad[eKeypadButtons.ProjOffButtonAndFb]].State = Keypad.Feedbacks[(uint)S100Keypad[eKeypadButtons.ProjOffButtonAndFb]].State == true ? false : true;
        //    }
        //    KeypadBlinkTimer.Reset(KEYPAD_RAMP_SPEED);
        //}

        void Projector_DoneWarmingEvent(IBasicProjector sender, bool IsWarming)
        {    
            CrestronConsole.PrintLine("The projector is not longer warming");
            Projector.Input = eVideoInputs.Dvi1;
        }

        /// <summary>
        /// Callback method to turn the system on.
        /// </summary>
        /// <param name="obj">Not used.</param>
        private void AutoOnCallback(object obj)
        {
            CrestronConsole.PrintLine("Turning the system on for the meeting \"{0}\"", NextMeeting.Subject);

            // if the room is off, turn it on.
            if (RoomState == eRoomStates.Off)
                SetRoomState(eRoomStates.On);
        }

        void Keypad_ButtonStateChange(GenericBase device, ButtonEventArgs args)
        {
            switch (args.Button.Number)
            {
                case (uint)eKeypadButtons.ProjOnButtonAndFb2:
                    // determine the state of the button
                    switch (args.NewButtonState) 
                    {
                        case eButtonState.Pressed:
                            //this.VersiPorts[1].DigitalOut = false;
                            this.myRelays[1].Close();
                            SetRoomState(eRoomStates.On);
                            break;
                        case eButtonState.Released:
                            //this.VersiPorts[1].DigitalOut = false;
                            this.myRelays[1].Open();
                            break;
                    }
                    break;

                case (uint)eKeypadButtons.ProjOffButtonAndFb4:
                //case (uint)S100Keypad[eKeypadButtons.ProjOffButtonAndFb] = 1:
                    // determine the state of the button
                    switch (args.NewButtonState)
                    {
                        case eButtonState.Pressed:
                            this.myRelays[2].Close();
                            SetRoomState(eRoomStates.Off);
                            break;
                        case eButtonState.Released:
                            this.myRelays[2].Open();
                            break;
                    }
                    break;
                case (uint)eKeypadButtons.VolDownButtonAndFb6:
                    // check to see if the vol is already ramping the other direction
                    //if (Keypad.Button[(uint)eKeypadButtons.VolDownButtonAndFb].State == eButtonState.Pressed)
                        //return;

                    // only make volume changes if the room is on
                    //if (RoomState != eRoomStates.On)
                        //return;

                    // determine the new state of the button
                    switch (args.NewButtonState)
                    {
                        case eButtonState.Pressed:
                            // acessssing the volume change action
                            VolumeRampAction = VolDown;
                            // start the ramp timer now
                            VolumeRampTimer.Reset(0);
                            Keypad.Feedbacks[(uint)eKeypadButtons.VolDownButtonAndFb6].State = true;
                            break;

                        case eButtonState.Released:
                            // stop the ramp timer
                            VolumeRampTimer.Stop();
                            Keypad.Feedbacks[(uint)eKeypadButtons.VolDownButtonAndFb6].State = false;
                            break;

                        default:
                            break;
                    }

                    break;

                case (uint)eKeypadButtons.VolUpButtonAndFb5:
                    // check to see if the vol is already ramping the other direction
                    //if (Keypad.Button[(uint)eKeypadButtons.VolUpButtonAndFb].State == eButtonState.Pressed)
                        //return;

                    // only make volume changes if the room is on
                    //if (RoomState != eRoomStates.On)
                        //return;

                    switch (args.NewButtonState)
                    {
                        case eButtonState.Pressed:
                            // assing the volume change action
                            VolumeRampAction = VolUp;
                            Keypad.Feedbacks[(uint)eKeypadButtons.VolUpButtonAndFb5].State = true;

                            // start the ramp timer now
                            VolumeRampTimer.Reset(0);
                            break;

                        case eButtonState.Released:
                            // stop the ramp timer
                            VolumeRampTimer.Stop();
                            Keypad.Feedbacks[(uint)eKeypadButtons.VolUpButtonAndFb5].State = false;
                            break;

                        default:
                            break;
                    }
                    break;


                default:
                    break;
            }
        }

        private string ProcessorMac;
        private string ProcessorIp;
        private string ProcessorHostname;

        /// <summary>
        /// Method to read the local network info and send to fusion.
        /// </summary>
        /// <param name="isStartUp">TRUE = get IP, MAC and HOSTNAME, FALSE = get IP only.</param>
        private void GetProcessorInfo(bool isStartUp)
        {
            string info = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, 0);

            if (string.IsNullOrEmpty(info))
            {
                CrestronConsole.PrintLine(">>> Could not read IP address");
                ProcessorIp = "?";
                //RV.UserDefinedStringSigDetails[(uint)eRvStrings.ProcessorIp].InputSig.StringValue = "?";
            }
            else
            {
                CrestronConsole.PrintLine("ip address {0}", info);
                ProcessorIp = info;
                //RV.UserDefinedStringSigDetails[(uint)eRvStrings.ProcessorIp].InputSig.StringValue = info;
            }

            // only get the following parameters if this method is being called by the start up sequence
            if (!isStartUp)
                return;

            info = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_MAC_ADDRESS, 0);

            if (string.IsNullOrEmpty(info))
            {
                CrestronConsole.PrintLine(">>> Could not read MAC address");
                ProcessorMac = "?";
                //RV.UserDefinedStringSigDetails[(uint)eRvStrings.ProcessorMac].InputSig.StringValue = "?";
            }
            else
            {
                CrestronConsole.PrintLine("mac address {0}", info);
                ProcessorMac = info;
                //RV.UserDefinedStringSigDetails[(uint)eRvStrings.ProcessorMac].InputSig.StringValue = info;
            }

            info = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_HOSTNAME, 0);

            if (string.IsNullOrEmpty(info))
            {
                CrestronConsole.PrintLine(">>> Could not read host name");
                ProcessorHostname = "?";
                //RV.UserDefinedStringSigDetails[(uint)eRvStrings.ProcessorHostname].InputSig.StringValue = "?";
            }
            else
            {
                CrestronConsole.PrintLine("host name {0}", info);
                ProcessorHostname = info;
                //RV.UserDefinedStringSigDetails[(uint)eRvStrings.ProcessorHostname].InputSig.StringValue = info;
            }
        }

        /// <summary>
        /// Delay time after the display is turned on to send the hdmi1 input command.
        /// </summary>
        //private const long VIDEO_INPUT_COMMAND_DELAY = 15000;

        /// <summary>
        /// Method to set the room state and set devices to their corresponding state.
        /// </summary>
        /// <param name="newState">State to put the room into</param>
        private void SetRoomState(eRoomStates newState)
        {
            // determine which state is being attempted
            switch (newState)
            {
                case eRoomStates.Off:
                    // turn the display off
                    Projector.PowerOff();
                    //Thread.Sleep(4000);
                    //Projector.PollPowerAndLamp();

                    // update the room state
                    RoomState = newState;

                    // update keypad feedback LEDs
                    //Keypad.Feedbacks[(uint)eKeypadButtons.ProjOffButtonAndFb].State = true;
                    //Keypad.Feedbacks[(uint)eKeypadButtons.ProjOnButtonAndFb].State = false;
                    break;

                case eRoomStates.On:
                    // turn the display on
                    Projector.PowerOn();
                    //Thread.Sleep(4000);
                    //CrestronConsole.PrintLine("now polling lamp 4 seconds after turning on");
                    //Projector.PollPowerAndLamp();

                    // update the room state
                    RoomState = newState;

                    // update keypad feedback LEDs
                    //Keypad.Feedbacks[(uint)eKeypadButtons.ProjOnButtonAndFb].State = true;
                    //Keypad.Feedbacks[(uint)eKeypadButtons.ProjOffButtonAndFb].State = false;
                    break;

                case eRoomStates.Starting:
                case eRoomStates.ShuttingDown:
                default:
                    throw new ArgumentException("Cannot set the RoomState to {0}", newState.ToString());
            }
        }

        //private void SetVideoInputCallback(object obj)
        //{
        //    try
        //    {
        //        Projector.Input = eVideoInputs.Dvi1;

        //    }
        //    catch (NotImplementedException)
        //    {
        //        CrestronConsole.PrintLine("This display ({0}) does not allow input switching.", RoomConfig.Details.DisplayType.ToString());
        //    }
        //}


        private void GetCresnetDeviceInfo()
        {
            // perform cresnet query
            CrestronCresnetHelper.Query();

            // itterate through the returned list and update fusion
            foreach (CrestronCresnetHelper.DiscoveredDeviceElement device in CrestronCresnetHelper.DiscoveredElementsList)
            {
                if (device.CresnetId == KEYPAD_ID)
                {
                    //RV.UserDefinedStringSigDetails[(uint)eRvStrings.KeypadFwVersion].InputSig.StringValue = device.Version;
                    CrestronConsole.PrintLine("Keypad fw version: {0}", device.Version);
                }
                //else if (device.CresnetId == OCCSENSOR_ID)
                //{
                //    RV.UserDefinedStringSigDetails[(uint)eRvStrings.OccSensorFwVersion].InputSig.StringValue = device.Version;
                //    CrestronConsole.PrintLine("Occupancy fw version: {0}", device.Version);
                //}
            }
        }


        /// <summary>
		/// Method to handle display volume change events.
		/// </summary>
		/// <param name="sender">Reference to the display raising this event.</param>
		/// <param name="IsOn">Volume level of the display.</param>
        void Scaled_VolumeEvent(object source, ushort Volume)
		{
            scaledVol = (ushort)CrestronEnvironment.ScaleWithLimits(DmVolume,  MaxVolume, MinVolume, 65535, 0);
            RV.UserDefinedUShortSigDetails[(uint)eRvUshorts.FusionVolume].InputSig.UShortValue = scaledVol;
            DmScaler.AudioOutput.Volume.UShortValue = scaledVol;
			Keypad.BargraphValue.UShortValue = scaledVol;
		}

        void Projector_LampEvent(IBasicProjector sender, ushort lamph1, ushort lamph2)
        {
            CrestronConsole.PrintLine("Projector Lamp hours: Lamp 1 = {0}, Lamp 2 = {1}", lamph1, lamph2);
            RV.UserDefinedUShortSigDetails[(uint)eRvUshorts.FusionLampHour1].InputSig.UShortValue = lamph1;
            RV.UserDefinedUShortSigDetails[(uint)eRvUshorts.FusionLampHour2].InputSig.UShortValue = lamph2;
            CrestronConsole.PrintLine("Projector Lamp hours end of method");
        }

        /// <summary>
        /// Method to handle display power change events.
        /// </summary>
        /// <param name="sender">Reference to the display raising this event.</param>
        /// <param name="IsOn">Power state of the display. TRUE = On, FALSE = Off.</param>
        void Projector_PowerEvent(IBasicProjector sender, ushort num)
        {
            switch (num)
            {

                case 0: //off and done cooling
                        //KeypadBlinkTimer.Stop();
                        Keypad.Feedbacks[(uint)eKeypadButtons.ProjOnButtonAndFb1].State = false;
                        Keypad.Feedbacks[(uint)eKeypadButtons.ProjOnButtonAndFb2].State = false;
                        //Keypad.Feedbacks[(uint)eKeypadButtons.ProjOffButtonAndFb].BlinkPattern = eButtonBlinkPattern.AlwaysOn;
                        Keypad.Feedbacks[(uint)eKeypadButtons.ProjOffButtonAndFb3].State = true;
                        Keypad.Feedbacks[(uint)eKeypadButtons.ProjOffButtonAndFb4].State = true;
                        RV.DisplayPowerOn.InputSig.BoolValue = false;
                        RV.SystemPowerOn.InputSig.BoolValue = false;
                        break;

                case 1: //warming - proj is on state                       
                        //Keypad.Feedbacks[(uint)eKeypadButtons.ProjOnButtonAndFb1].State = true;
                        //Keypad.Feedbacks[(uint)eKeypadButtons.ProjOnButtonAndFb2].State = true;
                        //keyNum = 1;
                        Keypad.Feedbacks[(uint)eKeypadButtons.ProjOnButtonAndFb1].BlinkPattern = eButtonBlinkPattern.MediumBlink;
                        Keypad.Feedbacks[(uint)eKeypadButtons.ProjOnButtonAndFb2].BlinkPattern = eButtonBlinkPattern.MediumBlink;
                        //Keypad.Feedbacks[LedErrorCodeFbs[SystemStatus]].BlinkPattern = eButtonBlinkPattern.MediumBlip;
                        //Keypad.Feedbacks[(uint)eKeypadButtons.ProjOnButtonAndFb].State = true;
                        Keypad.Feedbacks[(uint)eKeypadButtons.ProjOffButtonAndFb3].State = false;
                        Keypad.Feedbacks[(uint)eKeypadButtons.ProjOffButtonAndFb4].State = false;
                        RV.DisplayPowerOn.InputSig.BoolValue = true;
                        RV.SystemPowerOn.InputSig.BoolValue = true;
                        //KeypadBlinkTimer.Reset(KEYPAD_RAMP_SPEED);
                        break;
                   
                case 2: //on and done warming
                        //KeypadBlinkTimer.Stop();
                        Keypad.Feedbacks[(uint)eKeypadButtons.ProjOnButtonAndFb1].State = true;
                        Keypad.Feedbacks[(uint)eKeypadButtons.ProjOnButtonAndFb2].State = true;
                        //Keypad.Feedbacks[(uint)eKeypadButtons.ProjOnButtonAndFb1].BlinkPattern = eButtonBlinkPattern.AlwaysOn;
                        Keypad.Feedbacks[(uint)eKeypadButtons.ProjOffButtonAndFb3].State = false;
                        Keypad.Feedbacks[(uint)eKeypadButtons.ProjOffButtonAndFb4].State = false;
                        RV.DisplayPowerOn.InputSig.BoolValue = true;
                        RV.SystemPowerOn.InputSig.BoolValue = true;
                        break;
                    
                case 3: // cooling -- proj is off state
                        //Keypad.Feedbacks[(uint)eKeypadButtons.ProjOffButtonAndFb3].State = true;
                        //Keypad.Feedbacks[(uint)eKeypadButtons.ProjOffButtonAndFb4].State = true;
                        //keyNum = 2;
                        Keypad.Feedbacks[(uint)eKeypadButtons.ProjOffButtonAndFb3].BlinkPattern = eButtonBlinkPattern.MediumBlink;
                        Keypad.Feedbacks[(uint)eKeypadButtons.ProjOffButtonAndFb4].BlinkPattern = eButtonBlinkPattern.MediumBlink;
                        Keypad.Feedbacks[(uint)eKeypadButtons.ProjOnButtonAndFb1].State = false;
                        Keypad.Feedbacks[(uint)eKeypadButtons.ProjOnButtonAndFb2].State = false;
                        RV.DisplayPowerOn.InputSig.BoolValue = false;
                        RV.SystemPowerOn.InputSig.BoolValue = false;
                        //KeypadBlinkTimer.Reset(KEYPAD_RAMP_SPEED);
                        break;

            }

            //CrestronConsole.PrintLine("display {0}", IsOn ? "is on" : "is off");
        }

        /// <summary>
        /// Event Handler for Ethernet events: Link Up and Link Down. 
        /// Use these events to close / re-open sockets, etc. 
        /// </summary>
        /// <param name="ethernetEventArgs">This parameter holds the values 
        /// such as whether it's a Link Up or Link Down event. It will also indicate 
        /// wich Ethernet adapter this event belongs to.
        /// </param>
        void ControlSystem_ControllerEthernetEventHandler(EthernetEventArgs ethernetEventArgs)
        {
            switch (ethernetEventArgs.EthernetEventType)
            {//Determine the event type Link Up or Link Down
                case (eEthernetEventType.LinkDown):
                    //Next need to determine which adapter the event is for. 
                    //LAN is the adapter is the port connected to external networks.
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {
                        //
                    }
                    break;
                case (eEthernetEventType.LinkUp):
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {

                    }
                    break;
            }
        }

        void ControlSystem_VersiportChange(Versiport port, VersiportEventArgs args) //IO event on keypad
        {
            if (port == Keypad.VersiPorts[1])
                CrestronConsole.PrintLine("Port 1: {0}", port.DigitalIn);
            if (port == Keypad.VersiPorts[2])
                CrestronConsole.PrintLine("Port 2: {0}", port.DigitalIn);
        }

        /// <summary>
        /// Event Handler for Programmatic events: Stop, Pause, Resume.
        /// Use this event to clean up when a program is stopping, pausing, and resuming.
        /// This event only applies to this SIMPL#Pro program, it doesn't receive events
        /// for other programs stopping
        /// </summary>
        /// <param name="programStatusEventType"></param>
        void ControlSystem_ControllerProgramEventHandler(eProgramStatusEventType programStatusEventType)
        {
            switch (programStatusEventType)
            {
                case (eProgramStatusEventType.Paused):
                    //The program has been paused.  Pause all user threads/timers as needed.
                    break;
                case (eProgramStatusEventType.Resumed):
                    //The program has been resumed. Resume all the user threads/timers as needed.
                    break;
                case (eProgramStatusEventType.Stopping):
                    //The program has been stopped.
                    //Close all threads. 
                    //Shutdown all Client/Servers in the system.
                    //General cleanup.
                    //Unsubscribe to all System Monitor events
                    break;
            }

        }


        /// <summary>
        /// Event Handler for system events, Disk Inserted/Ejected, and Reboot
        /// Use this event to clean up when someone types in reboot, or when your SD /USB
        /// removable media is ejected / re-inserted.
        /// </summary>
        /// <param name="systemEventType"></param>
        void ControlSystem_ControllerSystemEventHandler(eSystemEventType systemEventType)
        {
            switch (systemEventType)
            {
                case (eSystemEventType.DiskInserted):
                    //Removable media was detected on the system
                    break;
                case (eSystemEventType.DiskRemoved):
                    //Removable media was detached from the system
                    break;
                case (eSystemEventType.Rebooting):
                    //The system is rebooting. 
                    //Very limited time to preform clean up and save any settings to disk.
                    break;
            }

        }

        void CresnetDevice_OnlineStatsChange(GenericBase sender, OnlineOfflineEventArgs args)
		{
            CrestronConsole.PrintLine(args.DeviceOnLine ? "Keypad online" : ">>> Keypad offline!");
            GetCresnetDeviceInfo();
		}

        /// <summary>
        /// Method to handle ethernet state events.
        /// </summary>
        /// <param name="ethernetEventArgs">Information about the current state of the lan connection.</param>
        void CrestronEnvironment_EthernetEventHandler(EthernetEventArgs ethernetEventArgs)
        {
            CrestronConsole.PrintLine("Ethernet link on interface '{0}' was {1}", ethernetEventArgs.EthernetAdapter, ethernetEventArgs.EthernetEventType == eEthernetEventType.LinkUp ? "established" : "lost");

            switch (ethernetEventArgs.EthernetEventType)
            {
                case eEthernetEventType.LinkUp:
                    GetProcessorInfo(false);
                    break;


                case eEthernetEventType.LinkDown:
                    break;

                default:
                    break;
            }
        }
    }
}