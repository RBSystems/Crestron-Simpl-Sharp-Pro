using System;
using System.ComponentModel;

namespace S_100_Template
{
    /// <summary>
    /// Enumeration of the possible video inputs on a IBasicProjector object.
    /// </summary>
    public enum eVideoInputs : byte
    {
        Hdmi1,
        Hdmi2,
        Hdmi3,
        Hdmi4,
        DisplayPort1,
        DisplayPort2,
        DisplayPort3,
        DisplayPort4,
        Vga1,
        Vga2,
        Dvi1,
        Dvi2,
        Component1,
        Component2,
        Rgbhv,
        SVideo,
        Composite,
        Usb,
        TvTuner,
        Unknown
    }

    public interface IBasicProjector
    {
        /// <summary>
        /// Method to turn the display on.
        /// </summary>
        void PowerOn();

        /// <summary>
        /// Method to turn the display off.
        /// </summary>
        void PowerOff();

        /// <summary>
        /// Method to poll power and lamp status.
        /// </summary>
        void PollPowerAndLamp();

        /// <summary>
        /// Method to mute the display.
        /// </summary>
        void VideoMuteOn();

        /// <summary>
        /// Method to unmute the display.
        /// </summary>
        void VideoMuteOff();

        /// <summary>
        /// Gets / sets the current video input.
        /// </summary>
        eVideoInputs Input { get; set; }

        /// <summary>
        /// Gets array of the usable inputs on this display.
        /// </summary>
        eVideoInputs[] UsableInputs { get; }

        /// <summary>
        /// Gets the current Power state.
        /// </summary>
        bool IsOn { get; }

        /// <summary>
        /// Gets the current Lamp 1 Hours.
        /// </summary>
        ushort LampHour1 { get; set; }

        /// <summary>
        /// Gets the current Lamp 2 Hours.
        /// </summary>
        ushort LampHour2 { get; set; }

        /// <summary>
        /// Gets the Warming State.
        /// </summary>
        bool IsWarming { get;}

        /// <summary>
        /// Gets the Cooling State.
        /// </summary>
        bool IsCooling { get; }

        /// <summary>
        /// Gets the current Mute state.
        /// </summary>
        bool IsVideoMuted { get; }

        /// <summary>
        /// Alerts to a change in power state.
        /// </summary>
        event PowerChangeHandler PowerEvent;

        /// <summary>
        /// Alerts to a change in mute state.
        /// </summary>
        event VideoMuteChangeHandler VideoMuteEvent;

        /// <summary>
        /// Alerts to a change of input.
        /// </summary>
        event InputChangeHandler InputEvent;

        /// <summary>
        /// Alerts to a change of lamp hours.
        /// </summary>
        event LampHourChangeHandler LampEvent;

        /// <summary>
        /// Alerts to a change of lamp hours.
        /// </summary>
        event DoneWarmingHandler DoneWarmingEvent;
    }

    /// <summary>
    /// Delegate for a power change event on a display.
    /// </summary>
    /// <param name="sender">Reference to the devices raising this event.</param>
    /// <param name="IsOn">Power state of the device raising this event.</param>
    public delegate void PowerChangeHandler(IBasicProjector sender, ushort pwrNum);

    /// <summary>
    /// Delegate for a mute change event on a display.
    /// </summary>
    /// <param name="sender">Reference to the devices raising this event.</param>
    /// <param name="IsOn">Mute state of the device raising this event.</param>
    public delegate void VideoMuteChangeHandler(IBasicProjector sender, bool IsVideoMuted);

    /// <summary>
    /// Delegate for a source input change event on a projector.
    /// </summary>
    /// <param name="sender">Reference to the devices raising this event.</param>
    /// <param name="IsMuted">Current input of the device raising this event.</param>
    public delegate void InputChangeHandler(IBasicProjector sender, eVideoInputs Input);

    /// <summary>
    /// Delegate for a lamp hour 1 or 2 change event on a projector.
    /// </summary>
    /// <param name="sender">Reference to the devices raising this event.</param>
    /// <param name="IsLampour1,LampHour2">Current input of the device raising this event.</param>
    public delegate void LampHourChangeHandler(IBasicProjector sender, ushort LampHour1, ushort LampHour2);

    /// <summary>
    /// Delegate for a event when projector is no longer warming -- set input .
    /// </summary>
    /// <param name="sender">Reference to the devices raising this event.</param>
    /// <param name="IsLampour1,LampHour2">Current input of the device raising this event.</param>
    public delegate void DoneWarmingHandler(IBasicProjector sender, bool IsWarming);


    public class InvalidInputException : Exception
    {
        /// <summary>
        /// Creates a generic InvalidInputExcpetion with no additional information.
        /// </summary>
        public InvalidInputException()
            : base("Invalid selection")
        {
            this.AttemptedInput = eVideoInputs.Unknown;
        }

        /// <summary>
        /// Creates a detailed InvalidInputException with error message and the invalid input number.
        /// </summary>
        /// <param name="message">Message to be included in the exception.</param>
        /// <param name="input">Value of the invalid input.</param>
        public InvalidInputException(string message, eVideoInputs input)
            : base(message)
        {
            this.AttemptedInput = input;
        }

        /// <summary>
        /// Value of the invalid input.
        /// </summary>
        public eVideoInputs AttemptedInput { get; private set; }
    }


}