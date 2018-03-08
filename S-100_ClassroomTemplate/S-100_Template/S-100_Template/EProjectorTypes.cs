using System;

namespace S_100_Template
{
    /// <summary>
    /// Enumeration of the pre-defined display types that can be used.
    /// </summary>
    public enum eProjectorTypes
    {
        /// <summary>
        /// No display control is available.
        /// </summary>
        NoControl,
        /// <summary>
        /// Use standard CEC power controls and DMPS volume control (if available).
        /// </summary>
        PanasonicPT_DW5500,
    }
}