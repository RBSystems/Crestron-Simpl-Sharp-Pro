using System;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.CrestronXml.Serialization;

namespace S_100_Template
{
    public class S_100ConfigData : IConfigData
    {
        public S_100ConfigData()
        {
            _roomName = "S-159";
            _guid = Guid.NewGuid().ToString();
            _occTimeout = 1800;
            _displayType = eProjectorTypes.PanasonicPT_DW5500;
            _useDmRmc = false;
            //_externalSwitchIpHostname = string.Empty;
            _modified = true;
        }

        #region IConfigData Members

        private bool _modified;
        public bool Modified
        {
            get { return _modified; }
            set { _modified = value; }
        }

        #endregion

        #region Config properties

        private string _roomName;
        public string RoomName
        {
            get
            {
                return _roomName;
            }
            set
            {
                _roomName = value;
                _modified = true;
            }
        }

        private string _guid;
        public string RoomGuid
        {
            get
            {
                return _guid;
            }
            set
            {
                _guid = value;
                _modified = true;
            }
        }

        private ushort _occTimeout;
        public ushort OccTimeout
        {
            get
            {
                return _occTimeout;
            }
            set
            {
                _occTimeout = value;
                _modified = true;
            }
        }

        private eProjectorTypes _displayType;
        public eProjectorTypes DisplayType
        {
            get
            {
                return _displayType;
            }
            set
            {
                _displayType = value;
                _modified = true;
            }
        }

        private bool _useDmRmc;
        public bool UseDmRmc
        {
            get
            {
                return _useDmRmc;
            }
            set
            {
                _useDmRmc = value;
                _modified = true;
            }
        }

        //private string _externalSwitchIpHostname;
        //public string ExternalVideoSwitchIpHostname
        //{
        //    get { return _externalSwitchIpHostname; }
        //    set
        //    {
        //        _modified = true;
        //        _externalSwitchIpHostname = value;
        //    }
        //}

        #endregion
    }
}