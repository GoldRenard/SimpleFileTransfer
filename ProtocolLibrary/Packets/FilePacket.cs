using System;

namespace ProtocolLibrary.Packets {

    [Serializable]
    public class FilePacket {
        public String Guid;
        public int PacketNumber;
        public byte[] PacketData;

        public FilePacket(FileEntry fileEntry, int packetNumber) {
            this.Guid = fileEntry.Header.Guid;
            this.PacketNumber = packetNumber;
            this.PacketData = fileEntry.ReadPacket(packetNumber);
        }

        public override string ToString() {
            return String.Format("FilePacket [Guid={0}, PacketNumber={1}, PacketData.Lenght={2}]", Guid, PacketNumber, PacketData != null ? PacketData.Length : 0);
        }
    }
}