using System;
using System.IO;

namespace ProtocolLibrary.Packets {

    [Serializable]
    public class FileHeader {
        public String Name;
        public String Guid;
        public long TotalLenght;
        public int PacketLength;

        public FileHeader(string filePath, int packetLenght) {
            Guid = System.Guid.NewGuid().ToString();
            this.PacketLength = packetLenght;
            FileInfo fInfo = new FileInfo(filePath);
            TotalLenght = fInfo.Length;
            Name = fInfo.Name;
        }

        public override string ToString() {
            return String.Format("FileHeader [Name={0}, Guid={1}, TotalLength={2}]", Name, Guid, TotalLenght);
        }
    }
}