using System;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharpPro.DeviceSupport;
using System.Text;
using System.Collections.Generic;
using Crestron.SimplSharpPro.DM;
using Crestron.SimplSharp.CrestronIO;

namespace S_100_Template
{
    public class PT_D5500 : IBasicProjector, IPollable
    {
        #region Constructor

        public PT_D5500(ComPort paraComport, bool paramRegisterComPort)
        {
            CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(CrestronEnvironment_ProgramStatusEventHandler);
            _com = paramComPort;

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
            _volume = 5;
            _ready = true;
            _frequency = 20000;
        }
        #endregion

        #region Private members

        private enum eCommands
        {
            Idle,
            PowerOn,
            PowerOff,
            PowerQuery,
            PowerEnable,
            PowerDisable,
            InputHdmi1,
            InputQuery,
            MuteOn,
            MuteOff,
            MuteQuery
        }

        private readonly Dictionary<eCommands, string> CommandStrings = new Dictionary<eCommands, string>()
		{ 
			{ eCommands.InputHdmi1, "IAVD1   \x0D" },
			{ eCommands.InputQuery, "IAVD?   \x0D" },
			{ eCommands.MuteOff, "MUTE2   \x0D" },
			{ eCommands.MuteOn, "MUTE1   \x0D" },
			{ eCommands.PowerOff, "POWR0   \x0D" },
			{ eCommands.PowerOn, "POWR1   \x0D" },
			{ eCommands.PowerQuery, "POWR?   \x0D" },
			{ eCommands.VolumeQuery, "VOLM?   \x0D" },
			{ eCommands.MuteQuery, "MUTE?   \x0D" },
			{ eCommands.PowerEnable, "RSPW1   \x0D"},
			{ eCommands.PowerDisable, "RSPW0   \x0D"}
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
    }
}