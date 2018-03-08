using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace S_100_Template
{
    public interface IConfigData
    {
        bool Modified { get; set; }
    }
}