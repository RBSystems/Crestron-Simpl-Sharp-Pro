#define UseStaticGUID // use this to toggle between static guids (manually entered) and dynamic guids (created at start up for deployable code). Comment it out to switch to dynamic

using System;
using System.Text;
using Crestron.SimplSharp;                          
using Crestron.SimplSharpPro;                       
using Crestron.SimplSharpPro.CrestronThread;        
using Crestron.SimplSharpPro.Diagnostics;		    
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.Fusion;
using Crestron.SimplSharpPro.UI;     

namespace Example.Fusion
{
	public class ControlSystem : CrestronControlSystem
	{
		// Define local variables ...
		FusionRoom myRoom;
		
		FusionOccupancySensor mySensorAsset;
		FusionStaticAsset myTouchScreenAsset;
		FusionLightingLoad myLightingLoad;

		/// <summary>
		/// Constructor of the Control System Class. Make sure the constructor always exists.
		/// If it doesn't exit, the code will not run on your 3-Series processor.
		/// </summary>
		public ControlSystem() : base()
		{
			// Set the number of system and user threads which you want to use in your program .
			// User threads are created using the CrestronThread class
			// System threads are used for CTimers/CrestronInvoke/Async Socket operations
			// At this point the threads cannot be created but we should
			// define the max number of threads which we will use in the system.
			// the right number depends on your project; do not make this number unnecessarily large
			Thread.MaxNumberOfUserThreads = 20;

#if UseStaticGUID
			// Create the room object (parent of the assets and attributes). this contains all of the events you will need
			myRoom = new FusionRoom(0xf0, this, "SSP Fusion Room (Static)", "BA65B6F1-DC9B-47f5-8577-F32782045380");	// this uses a status guid i created with the "Create GUID" tool (Tools Menu >> Create GUID). Select "Registry Format"
			
			// Add some assets, there are several types you can add
			myRoom.AddAsset(eAssetType.OccupancySensor, 1, "Occupancy Sensor", "Occupancy Sensor", "540E4FA9-97BE-4e21-A87E-C37E5B150FD0");
			myRoom.AddAsset(eAssetType.StaticAsset, 2, "Xpanel", "UI", "8081EA14-4869-4b86-877B-0075366BA8C6");
			myRoom.AddAsset(eAssetType.LightingLoad, 3, "Lights", "Lighting Load", "A74A1501-78E6-4a19-B0A8-F004C999F896");
#endif
#if !UseStaticGUID
			// Create the room object (parent of the assets and attributes). this contains all of the events you will need
			myRoom = new FusionRoom(0xf1, this, "SSP Fusion Room (Dynamic)", GuidManager.GetGuid("FusionRoom"));	// this is the same as above but will create a unique guid at first start up, then read from file after that
			
			// Add some assets, there are several types you can add
			myRoom.AddAsset(eAssetType.OccupancySensor, 1, "Occupancy Sensor", "Occupancy Sensor", GuidManager.GetGuid("SensorAsset"));
			myRoom.AddAsset(eAssetType.StaticAsset, 2, "Xpanel", "UI", GuidManager.GetGuid("XpanelAsset"));
			myRoom.AddAsset(eAssetType.LightingLoad, 3, "Lights", "Lighting Load", GuidManager.GetGuid("Lights"));
#endif

			// assign a more friendly reference to these assets and cast to the propper type
			mySensorAsset = (FusionOccupancySensor)myRoom.UserConfigurableAssetDetails[1].Asset;
			myTouchScreenAsset = (FusionStaticAsset)myRoom.UserConfigurableAssetDetails[2].Asset;
			myLightingLoad = (FusionLightingLoad)myRoom.UserConfigurableAssetDetails[3].Asset;

			// adjust parameters for the assets, some have required parameters
			myTouchScreenAsset.ParamMake.Value = "Crestron";
			myTouchScreenAsset.ParamModel.Value = "TSW-1052-B-S";

			myLightingLoad.ParamDimmable = false;
			myLightingLoad.ParamMeterType = eMeterType.Simulated;
			
			// Add some custom sigs (attributes)
			// the attribute IDs start at 50, just like in simpl, but the SDK will add 49 to the number you provide here. The following three lines create one attribute of each type with ID "50"
			myRoom.AddSig(eSigType.Bool, 1, "_Digital Attribute", eSigIoMask.InputOutputSig);	// bool = digital, this is set for InputOutputSig (i.e. read from fusion / write to fusion)
			myRoom.AddSig(eSigType.String, 1, "_Serial Attribute", eSigIoMask.InputSigOnly);	// string = serial, this is set for InputSigOnly (i.e. write to fusion)
			myRoom.AddSig(eSigType.UShort, 1, "_Analog Attribyte", eSigIoMask.OutputSigOnly);	// ushort = analog, this is set for OutputSigOnly (i.e. read from fusion)

			// subscribe to events
			myRoom.FusionAssetStateChange += new FusionAssetStateEventHandler(myRoom_FusionAssetStateChange);
			myRoom.FusionStateChange += new FusionStateEventHandler(myRoom_FusionStateChange);
			myRoom.OnlineStatusChange += new OnlineStatusChangeEventHandler(myRoom_OnlineStatusChange);

			myRoom.Register(); // I usually add some sort of check to make sure it is registered okay, but i'm lazy today

			TouchscreenOnlineToggleTimer = new CTimer(TouchScreenOnlineToggleCallback, null, Timeout.Infinite, Timeout.Infinite);
		}








		/// <summary>
		/// Method to handle online/offline events raised by the fusion room object.
		/// </summary>
		/// <param name="currentDevice">Reference to the FusionRoom object that is raising this event.</param>
		/// <param name="args">Information about the online/offline event being raised.</param>
		void myRoom_OnlineStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
		{
			CrestronConsole.PrintLine("Fusion is {0}", args.DeviceOnLine ? "online" : "offline");

			// start/stop the timer to simulate the touchscreen asset going online/offline every 60sec (60,000ms)
			if (args.DeviceOnLine) TouchscreenOnlineToggleTimer.Reset(2000, 60000);
			else TouchscreenOnlineToggleTimer.Stop();
		}





		#region simulated logic
		/// <summary>
		/// Fake method. Used to simulate the room's power feedback.
		/// </summary>
		/// <param name="newState">TRUE = ON, FALSE = OFF</param>
		void SetSystemPower(bool newState)
		{
			myRoom.SystemPowerOn.InputSig.BoolValue = newState;
			myRoom.DisplayPowerOn.InputSig.BoolValue = newState;
			CrestronConsole.PrintLine("System power is {0}", newState ? "on" : "off");
		}

		/// <summary>
		/// Fake method. Used to simulate a real display's feedback.
		/// </summary>
		/// <param name="newState">TRUE = ON, FALSE = OFF</param>
		void SetDisplayPower(bool newState)
		{
			myRoom.DisplayPowerOn.InputSig.BoolValue = newState;
			CrestronConsole.PrintLine("Display power is {0}", newState ? "on" : "off");
		}

		/// <summary>
		/// Fake method. Used to simulate a lighting switch.
		/// </summary>
		/// <param name="newState"></param>
		void SetLights(bool newState)
		{
			myLightingLoad.LoadOn.InputSig.BoolValue = newState;
			CrestronConsole.PrintLine("Light is {0}", newState ? "on" : "off");
		}

		CTimer TouchscreenOnlineToggleTimer;

		/// <summary>
		/// Fake method. Used to toggle the touchscreen asset "connected" property on and off.
		/// </summary>
		/// <param name="o">Not used.</param>
		void TouchScreenOnlineToggleCallback(object o)
		{
			// toggle the bool value
			myTouchScreenAsset.Connected.InputSig.BoolValue = !myTouchScreenAsset.Connected.InputSig.BoolValue;
			CrestronConsole.PrintLine("Touchscreen {0}", myTouchScreenAsset.Connected.InputSig.BoolValue ? "online" : "offline");
		}
		#endregion





		/// <summary>
		/// Method to handle attribute changes on the FusionRoom object. This will be called for the default attributes, and any custom ones added using the FusionRoom.AddSig() method.
		/// </summary>
		/// <param name="device">Reference to the FusionRoom object raising this event.</param>
		/// <param name="args">Information about the event being raised.</param>
		void myRoom_FusionStateChange(FusionBase device, FusionStateEventArgs args)
		{
			// determine what type of event is being raised
			switch (args.EventId)
			{
				#region custom sigs (attributes)
				case FusionEventIds.UserConfiguredStringSigChangeEventId:
					var incomingStringSig = (StringSigData)args.UserConfiguredSigDetail;

					switch (incomingStringSig.Number)
					{
						// there are no user string sigs in this program, but this is how you would trap them
						default:
							break;
					}
					break;

				case FusionEventIds.UserConfiguredUShortSigChangeEventId:
					var incomingUshortSig = (UShortSigData)args.UserConfiguredSigDetail;

					switch (incomingUshortSig.Number)
					{
						case 1:
							CrestronConsole.PrintLine("Received a new Ushort value from fusion: {0}", incomingUshortSig.OutputSig.UShortValue);
							break;

						default:
							break;
					}
					break;

				case FusionEventIds.UserConfiguredBoolSigChangeEventId:
					var incomingBoolSig = (BooleanSigData)args.UserConfiguredSigDetail;

					if (incomingBoolSig.OutputSig.BoolValue)
					{
						// determine which user bool sig is raising this event
						switch (incomingBoolSig.Number)
						{
							case 1:
								CrestronConsole.PrintLine("Received bool sig change from fusion!");
								break;
							default:
								break;
						}
					}

					break;
				#endregion

				#region System Power Events
				case FusionEventIds.SystemPowerOffReceivedEventId:
					// you need to add the following line to all fusion digital / bool sigs
					// when you send a digital from fusion will pulse the property on the Fusion room object (FALSE > TRUE, then TRUE > FALSE)
					// omitting this line will cause this code to be called twice
					if (myRoom.SystemPowerOff.OutputSig.BoolValue)
					{
						// do what ever needs to happen when the sig is set
						SetSystemPower(false);
					}
					break;

				case FusionEventIds.SystemPowerOnReceivedEventId:
					// you need to add the following line to all fusion digital / bool sigs
					// when you send a digital from fusion will pulse the property on the Fusion room object (FALSE > TRUE, then TRUE > FALSE)
					// omitting this line will cause this code to be called twice
					if (myRoom.SystemPowerOn.OutputSig.BoolValue)
					{
						// do what ever needs to happen when the sig is set
						SetSystemPower(true);
					}
					break;
				#endregion

				#region Display Power Events
				case FusionEventIds.DisplayPowerOnReceivedEventId:
					// you need to add the following line to all fusion digital / bool sigs
					// when you send a digital from fusion will pulse the property on the Fusion room object (FALSE > TRUE, then TRUE > FALSE)
					// omitting this line will cause this code to be called twice
					if (myRoom.DisplayPowerOn.OutputSig.BoolValue)
					{
						// do what ever needs to happen when the sig is set
						SetDisplayPower(true);
					}
					break;

				case FusionEventIds.DisplayPowerOffReceivedEventId:
					// you need to add the following line to all fusion digital / bool sigs
					// when you send a digital from fusion will pulse the property on the Fusion room object (FALSE > TRUE, then TRUE > FALSE)
					// omitting this line will cause this code to be called twice
					if (myRoom.DisplayPowerOff.OutputSig.BoolValue)
					{
						// do what ever needs to happen when the sig is set
						SetDisplayPower(false);
					}
					break;
				#endregion

				#region other possible events to explore
				case FusionEventIds.AuthenticateFailedReceivedEventId:
				case FusionEventIds.AuthenticateSucceededReceivedEventId:
				case FusionEventIds.BroadcastMessageReceivedEventId:
				case FusionEventIds.BroadcastMessageTypeReceivedEventId:
				case FusionEventIds.GroupMembershipRequestReceivedEventId:
				case FusionEventIds.HelpMessageReceivedEventId:
				case FusionEventIds.TextMessageFromRoomReceivedEventId:
				#endregion
				default:
					break;
			}
		}

		/// <summary>
		/// Method to handle asset changes from fusion (
		/// </summary>
		/// <param name="device"></param>
		/// <param name="args"></param>
		void myRoom_FusionAssetStateChange(FusionBase device, FusionAssetStateEventArgs args)
		{
			// determine which asset is raising this event
			switch (args.UserConfigurableAssetDetailIndex)
			{
				case 3: // the FusionLightingLoad

					// determine what event is being raised
					switch (args.EventId)
					{
						case FusionAssetEventId.LoadOffReceivedEventId:
							if (myLightingLoad.LoadOff.OutputSig.BoolValue)
								SetLights(false);
							break;

						case FusionAssetEventId.LoadOnReceivedEventId:
							if (myLightingLoad.LoadOn.OutputSig.BoolValue)
								SetLights(true);
							break;

						default:
							break;
					}
					break;

				default:
					break;
			}
		}

		/// <summary>
		/// Overridden function... Invoked before any traffic starts flowing back and forth between the devices and the 
		/// user program. 
		/// This is used to start all the user threads and create all events / mutexes etc.
		/// This function should exit ... If this function does not exit then the program will not start
		/// </summary>
		public override void InitializeSystem()
		{
			// this method should be called after all of the fusion room devices have been created, and assets/attributes are added to them
			// this is what generates the *.rvi file in the project directory at start up (used for autodiscover)
			FusionRVI.GenerateFileForAllFusionDevices();
		}
	}
}
