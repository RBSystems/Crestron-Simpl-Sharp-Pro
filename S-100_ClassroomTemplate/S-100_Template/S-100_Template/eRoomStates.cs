using System;

namespace S_100_Template
{
    /// <summary>
    /// Enumeration of the valid room states.
    /// </summary>
    public enum eRoomStates
    {
        /// <summary>
        /// The room is fully off and not in use.
        /// </summary>
        Off,

        /// <summary>
        /// The room has devices that are starting up.
        /// </summary>
        Starting,

        /// <summary>
        /// The room is fully on and in use.
        /// </summary>
        On,

        /// <summary>
        /// The room is off, but waiting for devices to cool down.
        /// </summary>
        ShuttingDown
    }
}