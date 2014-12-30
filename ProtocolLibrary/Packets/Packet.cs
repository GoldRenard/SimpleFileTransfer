using System;

namespace ProtocolLibrary.Packets {

    [Serializable]
    public class Packet {
        public Command Command;
        public object Data;
    }
}