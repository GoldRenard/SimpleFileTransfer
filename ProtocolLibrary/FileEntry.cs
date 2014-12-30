using System;
using System.IO;
using System.Net.Sockets;
using ProtocolLibrary.Packets;

namespace ProtocolLibrary {

    /// <summary>
    /// Запись файла
    /// </summary>
    public class FileEntry : Stream {
        public readonly FileInfo Info;
        public readonly Stream Stream;
        public readonly FileHeader Header;
        public readonly long PacketsCount;
        public readonly TcpClient Owner;
        private bool ShouldDelete;
        private bool Closed = false;

        public bool TransferDone {
            get;
            set;
        }

        public FileEntry(FileHeader fileHeader, TcpClient owner, FileMode fileMode, FileAccess fileAccess, String filePath = null, bool deleteOnClose = false) {
            if (filePath == null) {
                filePath = Path.GetTempFileName();
            }
            this.Owner = owner;
            this.ShouldDelete = deleteOnClose;
            this.TransferDone = false;
            Header = fileHeader;
            Info = new FileInfo(filePath);
            Stream = File.Open(filePath, fileMode, fileAccess);
            SetLength(fileHeader.TotalLenght);
            PacketsCount = (long)Math.Ceiling((double)Header.TotalLenght / (double)Header.PacketLength);
        }

        public FileEntry(String filePath, int PacketLenght, TcpClient owner, FileMode fileMode, FileAccess fileAccess) {
            this.Owner = owner;
            this.TransferDone = false;
            Header = new FileHeader(filePath, PacketLenght);
            Info = new FileInfo(filePath);
            Stream = File.Open(filePath, fileMode, fileAccess);
            PacketsCount = (long)Math.Ceiling((double)Header.TotalLenght / (double)Header.PacketLength);
        }

        public override void Close() {
            if (!Closed) {
                Stream.Close();
                if (ShouldDelete) {
                    try {
                        File.Delete(Info.FullName);
                    } catch {
                        // ignore
                    }
                }
                Closed = true;
            }
        }

        public override bool CanRead {
            get {
                return Stream.CanRead;
            }
        }

        public override bool CanSeek {
            get {
                return Stream.CanSeek;
            }
        }

        public override bool CanWrite {
            get {
                return Stream.CanWrite;
            }
        }

        public override void Flush() {
            Stream.Flush();
        }

        public override long Length {
            get {
                return Stream.Length;
            }
        }

        public override long Position {
            get {
                return Stream.Position;
            }
            set {
                Stream.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count) {
            return Stream.Read(buffer, offset, count);
        }

        /// <summary>
        /// Чтение пакета файла по его номеру
        /// </summary>
        /// <param name="packetNum">Номер пакета</param>
        /// <returns>Массив байт пакета</returns>
        public byte[] ReadPacket(int packetNum) {
            lock (Header) {
                if (packetNum > PacketsCount || Closed) {
                    return null;
                }
                int offset = (packetNum - 1) * Header.PacketLength;
                int size = Header.PacketLength;
                if (offset + size > Header.TotalLenght) {
                    size = (int)(Header.TotalLenght - offset);
                }
                byte[] buffer = new byte[size];
                Seek(offset, SeekOrigin.Begin);
                Read(buffer, 0, size);
                return buffer;
            }
        }

        /// <summary>
        /// Запись пакета и его данных
        /// </summary>
        /// <param name="packet">Объект пакета</param>
        /// <returns>Объект следующего пакета или null если это был последний</returns>
        public FilePacket WritePacket(FilePacket packet) {
            lock (Header) {
                if (packet.PacketNumber > PacketsCount || Closed) {
                    return null;
                }
                int offset = (packet.PacketNumber - 1) * Header.PacketLength;
                Seek(offset, SeekOrigin.Begin);
                Write(packet.PacketData, 0, packet.PacketData.Length);
                packet.PacketNumber += 1;
                return packet;
            }
        }

        public override long Seek(long offset, SeekOrigin origin) {
            return Stream.Seek(offset, origin);
        }

        public override void SetLength(long value) {
            Stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count) {
            Stream.Write(buffer, offset, count);
        }

        public bool IsClosed {
            get {
                return Closed;
            }
        }
    }
}