using System;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using ProtocolLibrary.Packets;

namespace ProtocolLibrary {

    /// <summary>
    /// Менеджер пакетов
    /// </summary>
    public class PacketManager {
        private static readonly log4net.ILog LOGGER = log4net.LogManager.GetLogger(typeof(PacketManager));

        private static object lockObject = new Object();

        private TcpClient client;

        private static IFormatter formatter = new BinaryFormatter();

        public delegate void FileHeaderEvent(object sender, TcpClient client, FileHeader header);

        public delegate void FilePacketReceivedEvent(object sender, TcpClient client, FilePacket packet);

        /// <summary>
        /// Ивент запроса отправки файла
        /// </summary>
        public event FileHeaderEvent FileSendRequest;

        /// <summary>
        /// Ивент получения пакета с данными
        /// </summary>
        public event FilePacketReceivedEvent FilePacketReceived;

        /// <summary>
        /// Ивент готовности получения файла
        /// </summary>
        public event FileHeaderEvent FileReceiveReady;

        /// <summary>
        /// Ивент запрета получения файла
        /// </summary>
        public event FileHeaderEvent FileReceiveDenied;

        /// <summary>
        /// Ивент запрета получения файла
        /// </summary>
        public event FileHeaderEvent FileSendCancel;

        /// <summary>
        /// Ивент запроса пакета для передачи
        /// </summary>
        public event FilePacketReceivedEvent FilePacketRequest;

        /// <summary>
        /// Ивента заверщения передачи файла
        /// </summary>
        public event FileHeaderEvent FileSendDone;

        /// <summary>
        /// Ивент завершения приема файла
        /// </summary>
        public event FileHeaderEvent FileReceiveDone;

        public PacketManager(TcpClient client) {
            this.client = client;
        }

        /// <summary>
        /// Тело обработчика пакета
        /// </summary>
        /// <returns>True если пакет был обработан</returns>
        public bool ProcessPacket() {
            if (!client.GetStream().DataAvailable) {
                return false;
            }
            Packet packet;
            try {
                packet = (Packet)formatter.Deserialize(client.GetStream());
            } catch (Exception e) {
                LOGGER.Warn(e);
                return false;
            }
            switch (packet.Command) {
                case Command.FILE_SEND_REQUEST:
                    if (FileSendRequest != null) {
                        FileSendRequest(this, client, (FileHeader)packet.Data);
                    }
                    break;

                case Command.FILE_RECEIVE_READY:
                    if (FileReceiveReady != null) {
                        FileReceiveReady(this, client, (FileHeader)packet.Data);
                    }
                    break;

                case Command.FILE_RECEIVE_DENIED:
                    if (FileReceiveDenied != null) {
                        FileReceiveDenied(this, client, (FileHeader)packet.Data);
                    }
                    break;

                case Command.FILE_SEND_DONE:
                    if (FileSendDone != null) {
                        FileSendDone(this, client, (FileHeader)packet.Data);
                    }
                    break;

                case Command.FILE_RECEIVE_DONE:
                    if (FileReceiveDone != null) {
                        FileReceiveDone(this, client, (FileHeader)packet.Data);
                    }
                    break;

                case Command.FILE_SEND_CANCEL:
                    if (FileSendCancel != null) {
                        FileSendCancel(this, client, (FileHeader)packet.Data);
                    }
                    break;

                case Command.FILE_PACKET_DATA:
                    if (FilePacketReceived != null) {
                        FilePacketReceived(this, client, (FilePacket)packet.Data);
                    }
                    break;

                case Command.FILE_PACKET_REQUEST:
                    if (FilePacketRequest != null) {
                        FilePacketRequest(this, client, (FilePacket)packet.Data);
                    }
                    break;

                default:
                    break;
            }
            return true;
        }

        /// <summary>
        /// Отправка пакета клиенту
        /// </summary>
        /// <param name="client">Клиент</param>
        /// <param name="command">Команда пакета</param>
        /// <param name="data">Данные пакета</param>
        public static void SendPacket(TcpClient client, Command command, object data) {
            if (!Utils.IsConnected(client.Client)) {
                return;
            }

            Packet packet = new Packet() {
                Command = command,
                Data = data
            };
            Task.Factory.StartNew(() => {
                try {
                    lock (formatter) {
                        formatter.Serialize(client.GetStream(), packet);
                    }
                } catch (System.IO.IOException e) {
                    if (typeof(SocketException) != e.InnerException.GetType()) {
                        throw;
                    }
                } catch (ObjectDisposedException e) {
                    // ignore it
                }
            }, TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// Отправка пакета клиенту по-умолчанию
        /// </summary>
        /// <param name="command">Команда пакета</param>
        /// <param name="data">Данные пакета</param>
        public void SendPacket(Command command, object data) {
            SendPacket(client, command, data);
        }
    }
}