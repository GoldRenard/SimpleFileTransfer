using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using ProtocolLibrary.Packets;

namespace ProtocolLibrary {

    /// <summary>
    /// Обработчик команд и пакетов передачи файлов
    /// </summary>
    public class FileHandler {

        /// <summary>
        /// Стандартный размер пакета (буфера) для передачи данных - 4MB
        /// </summary>
        public const int PACKET_DEFAULT_LENGTH = 4194304;

        private static readonly log4net.ILog LOGGER = log4net.LogManager.GetLogger(typeof(FileHandler));

        public delegate void FileRequestEvent(FileHandler sender, FileHeader header);

        public delegate void FileTransferEvent(FileHandler sender, TcpClient client, FileEntry entry);

        public delegate void FileTransferStatusChangedEvent(FileHandler sender, TcpClient client, FileEntry entry, int packetNumber);

        /// <summary>
        /// Ивент запроса отправки файла
        /// </summary>
        public event FileRequestEvent FileSendRequest;

        /// <summary>
        /// Ивент завершения приема файла
        /// </summary>
        public event FileTransferEvent FileReceiveDone;

        /// <summary>
        /// Ивент отказа приема файла
        /// </summary>
        public event FileTransferEvent FileReceiveDenied;

        /// <summary>
        /// Ивент готовности приема файла
        /// </summary>
        public event FileTransferEvent FileReceiveReady;

        /// <summary>
        /// Ивент завершения отправки файла
        /// </summary>
        public event FileTransferEvent FileSendDone;

        /// <summary>
        /// Ивент изменения статуса передачи файла
        /// </summary>
        public event FileTransferStatusChangedEvent FileTransferStatusChanged;

        private readonly ClientHandler ClientHandler;
        private readonly Dictionary<FileHeader, TcpClient> FileSendRequests;

        public FileHandler(ClientHandler clientHandler) {
            FileSendRequests = new Dictionary<FileHeader, TcpClient>();
            this.ClientHandler = clientHandler;
            this.ClientHandler.PacketManager.FileSendRequest += PacketManager_FileSendRequest;
            this.ClientHandler.PacketManager.FileReceiveReady += PacketManager_FileReceiveReady;
            this.ClientHandler.PacketManager.FilePacketReceived += PacketManager_FilePacketReceived;
            this.ClientHandler.PacketManager.FilePacketRequest += PacketManager_FilePacketRequest;
            this.ClientHandler.PacketManager.FileSendDone += PacketManager_FileSendDone;
            this.ClientHandler.PacketManager.FileReceiveDone += PacketManager_FileReceiveDone;
            this.ClientHandler.PacketManager.FileReceiveDenied += PacketManager_FileReceiveDenied;
            this.ClientHandler.PacketManager.FileSendCancel += PacketManager_FileSendCancel;
        }

        /// <summary>
        /// Обработчик отмены отправки
        /// </summary>
        /// <param name="sender">Обработчик</param>
        /// <param name="client">Клиент-инициатор</param>
        /// <param name="header">Заголовок файла</param>
        private void PacketManager_FileSendCancel(object sender, TcpClient client, FileHeader header) {
            LOGGER.DebugFormat("PacketManager_FileSendCancel -> {0}", header);
            FileEntry entry = FileCollection.GetEntry(header.Guid);
            if (entry != null) {
                entry.Close();
            }
        }

        /// <summary>
        /// Отправка файла клиенту по-умолчанию
        /// </summary>
        /// <param name="filePath">Путь до файла</param>
        public void SendFile(string filePath) {
            SendFile(ClientHandler.Client, filePath);
        }

        /// <summary>
        /// Отправка клиенту по-умолчанию файла
        /// </summary>
        /// <param name="fileEntry">Запись файла для отправки</param>
        public void SendFile(FileEntry fileEntry) {
            SendFile(ClientHandler.Client, fileEntry);
        }

        /// <summary>
        /// Отправка файла указанному клиенту
        /// </summary>
        /// <param name="client">Клиент назначения</param>
        /// <param name="filePath">Файл</param>
        public static void SendFile(TcpClient client, string filePath) {
            LOGGER.DebugFormat("SendFile -> {0}", filePath);
            FileEntry fileEntry = FileCollection.AddFile(filePath, PACKET_DEFAULT_LENGTH, client, FileMode.Open, FileAccess.Read);
            PacketManager.SendPacket(client, Command.FILE_SEND_REQUEST, fileEntry.Header);
        }

        /// <summary>
        /// Отправка файла указанному клиенту
        /// </summary>
        /// <param name="client">Клиент назначения</param>
        /// <param name="filePath">Файл</param>
        public static void SendFile(TcpClient client, FileEntry fileEntry) {
            LOGGER.DebugFormat("SendFile -> {0}", fileEntry);
            PacketManager.SendPacket(client, Command.FILE_SEND_REQUEST, fileEntry.Header);
        }

        /// <summary>
        /// Подтверждение приема файла с указанным заголовком
        /// </summary>
        /// <param name="accept">True если одобрить прием файла</param>
        /// <param name="header">Заголовок</param>
        /// <param name="outputFile">опционально - файл назначения</param>
        public void AcceptFileRequest(bool accept, FileHeader header, string outputFile = null, bool deleteOnClose = false) {
            LOGGER.DebugFormat("AcceptFileRequest -> FileHeader={0}", header);
            if (FileSendRequests.ContainsKey(header)) {
                TcpClient client = FileSendRequests[header];
                if (accept) {
                    FileCollection.AddFile(header, client, outputFile, deleteOnClose);
                    PacketManager.SendPacket(client, Command.FILE_RECEIVE_READY, header);
                } else {
                    PacketManager.SendPacket(client, Command.FILE_RECEIVE_DENIED, header);
                }
                FileSendRequests.Remove(header);
            }
        }

        /// <summary>
        /// Перенаправление ивента отправки файлов
        /// </summary>
        /// <param name="sender">Инстанс связанного PacketManager</param>
        /// <param name="client">Клиент-инициатор</param>
        /// <param name="header">Заголовок файла</param>
        private void PacketManager_FileSendRequest(object sender, TcpClient client, FileHeader header) {
            LOGGER.DebugFormat("PacketManager_FileSendRequest -> FileHeader={0}", header);
            FileSendRequests.Add(header, client);
            if (FileSendRequest != null) {
                FileSendRequest(this, header);
            }
        }

        /// <summary>
        /// Ответ на готовность к отправке файла (отсылается первый пакет)
        /// </summary>
        /// <param name="sender">Инстанс связанного PacketManager</param>
        /// <param name="client">Клиент-инициатор</param>
        /// <param name="header">Заголовок файла</param>
        private void PacketManager_FileReceiveReady(object sender, TcpClient client, FileHeader header) {
            LOGGER.DebugFormat("PacketManager_FileReceiveReady -> FileHeader={0}", header);
            FileEntry entry = FileCollection.GetEntry(header.Guid);
            FilePacket packet = new FilePacket(entry, 1);
            PacketManager.SendPacket(client, Command.FILE_PACKET_DATA, packet);
            if (FileReceiveReady != null) {
                FileReceiveReady(this, client, entry);
            }
        }

        /// <summary>
        /// Ответ на отказ к отправке файла
        /// </summary>
        /// <param name="sender">Инстанс связанного PacketManager</param>
        /// <param name="client">Клиент-инициатор</param>
        /// <param name="header">Заголовок файла</param>
        private void PacketManager_FileReceiveDenied(object sender, TcpClient client, FileHeader header) {
            LOGGER.DebugFormat("PacketManager_FileReceiveDenied -> FileHeader={0}", header);
            if (FileReceiveDenied != null) {
                FileEntry entry = FileCollection.GetEntry(header.Guid);
                FileReceiveDenied(this, client, entry);
            }
        }

        /// <summary>
        /// Получение и запись пакета файла и запрос следующего пакета
        /// </summary>
        /// <param name="sender">Инстанс связанного PacketManager</param>
        /// <param name="client">Клиент-инициатор</param>
        /// <param name="header">Пакет файла</param>
        private void PacketManager_FilePacketReceived(object sender, TcpClient client, FilePacket packet) {
            LOGGER.DebugFormat("PacketManager_FilePacketReceived -> FilePacket={0}", packet);
            FileEntry entry = FileCollection.GetEntry(packet.Guid);
            if (entry.IsClosed) {
                return;
            }
            if (FileTransferStatusChanged != null) {
                FileTransferStatusChanged(this, client, entry, packet.PacketNumber);
            }
            FilePacket nextPacket = entry.WritePacket(packet);
            if (nextPacket == null) {
                PacketManager.SendPacket(client, Command.FILE_SEND_DONE, entry.Header);
                PacketManager_FileReceiveDone(this, client, entry.Header);
                return;
            }
            PacketManager.SendPacket(client, Command.FILE_PACKET_REQUEST, nextPacket);
        }

        /// <summary>
        /// Обработка запроса на отправку следующего пакета
        /// </summary>
        /// <param name="sender">Инстанс связанного PacketManager</param>
        /// <param name="client">Клиент-инициатор</param>
        /// <param name="header">Пакет файла</param>
        private void PacketManager_FilePacketRequest(object sender, TcpClient client, FilePacket packet) {
            LOGGER.DebugFormat("PacketManager_FilePacketRequest -> FilePacket={0}", packet);
            FileEntry entry = FileCollection.GetEntry(packet.Guid);
            if (entry.IsClosed) {
                return;
            }
            FilePacket nextPacket = new FilePacket(entry, packet.PacketNumber);
            if (nextPacket.PacketData == null) {
                PacketManager.SendPacket(client, Command.FILE_RECEIVE_DONE, entry.Header);
                PacketManager_FileSendDone(this, client, entry.Header);
                return;
            }
            PacketManager.SendPacket(client, Command.FILE_PACKET_DATA, nextPacket);
            if (FileTransferStatusChanged != null) {
                FileTransferStatusChanged(this, client, entry, nextPacket.PacketNumber);
            }
        }

        /// <summary>
        /// Оповещение о завершении передачи файла
        /// </summary>
        /// <param name="sender">Инстанс связанного PacketManager</param>
        /// <param name="client">Клиент-инициатор</param>
        /// <param name="header">Заголовок файла</param>
        private void PacketManager_FileSendDone(object sender, TcpClient client, FileHeader header) {
            LOGGER.DebugFormat("PacketManager_FileSendDone -> FileHeader={0}", header);
            if (FileSendDone != null) {
                FileEntry entry = FileCollection.GetEntry(header.Guid);
                entry.TransferDone = true;
                FileSendDone(this, client, entry);
            }
        }

        /// <summary>
        /// Оповещение о завершении приема файла
        /// </summary>
        /// <param name="sender">Инстанс связанного PacketManager</param>
        /// <param name="client">Клиент-инициатор</param>
        /// <param name="header">Заголовок файла</param>
        private void PacketManager_FileReceiveDone(object sender, TcpClient client, FileHeader header) {
            LOGGER.DebugFormat("PacketManager_FileReceiveDone -> FileHeader={0}", header);
            if (FileReceiveDone != null) {
                FileEntry entry = FileCollection.GetEntry(header.Guid);
                entry.TransferDone = true;
                FileReceiveDone(this, client, entry);
            }
        }
    }
}