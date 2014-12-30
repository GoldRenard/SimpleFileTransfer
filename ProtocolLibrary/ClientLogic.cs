using System;
using System.Net;
using System.Net.Sockets;
using ProtocolLibrary.Packets;

namespace ProtocolLibrary {

    /// <summary>
    /// Логика клиента передачи данных
    /// </summary>
    public class ClientLogic {

        public delegate void DisconnectedEvent(ClientLogic sender, string message);

        public delegate void DoneEvent(ClientLogic sender, FileEntry entry);

        /// <summary>
        /// Происходит при изменении статуса передачи файла
        /// </summary>
        public event FileHandler.FileTransferStatusChangedEvent StatusChanged;

        public event FileHandler.FileRequestEvent SendRequest;

        /// <summary>
        /// Ивент завершения передачи файла
        /// </summary>
        public event DoneEvent SendDone;

        /// <summary>
        /// Ивент завершения приема файла
        /// </summary>
        public event DoneEvent ReceiveDone;

        /// <summary>
        /// Ивент готовности приема файла
        /// </summary>
        public event DoneEvent SendReady;

        /// <summary>
        /// Ивент отключения от сервера
        /// </summary>
        public event DisconnectedEvent Disconnected;

        public event DisconnectedEvent Connected;

        private ClientHandler _Handler;

        private readonly TcpClient _Client;

        /// <summary>
        /// Получение инстанса сервера
        /// </summary>
        public TcpClient Client {
            get {
                return _Client;
            }
        }

        public ClientLogic(IPAddress serverHost, int serverPort) {
            _Client = new TcpClient(AddressFamily.InterNetwork);
            _Client.BeginConnect(serverHost, serverPort, new AsyncCallback(ConnectCallback), _Client);
        }

        /// <summary>
        /// Отключиться от сервера
        /// </summary>
        public void Disconnect() {
            foreach (FileEntry entry in FileCollection.Files.Values) {
                if (entry.IsClosed) {
                    continue;
                }
                _Handler.PacketManager.SendPacket(Command.FILE_SEND_CANCEL, entry.Header);
                entry.Close();
            }
            _Client.Close();
            OnDisconnected();
        }

        /// <summary>
        /// Обратная связь подключения клиента
        /// </summary>
        /// <param name="result">Результат обработки</param>
        private void ConnectCallback(IAsyncResult result) {
            TcpClient client = (TcpClient)result.AsyncState;

            if (!client.Connected || !Utils.IsConnected(client.Client)) {
                OnDisconnected("Couldn't connect to server");
                return;
            }
            ClientPool.Clear();
            _Handler = ClientHandler.AsyncHandler(client);
            _Handler.Disconnected += (e) => {
                OnDisconnected();
            };

            _Handler.FileHandler.FileSendRequest += PacketManager_FileSendRequest;
            _Handler.FileHandler.FileSendDone += FileHandler_FileSendDone;
            _Handler.FileHandler.FileReceiveDone += FileHandler_FileReceiveDone;
            _Handler.FileHandler.FileTransferStatusChanged += FileHandler_FileTransferStatusChanged;
            _Handler.FileHandler.FileReceiveReady += FileHandler_FileReceiveReady;
            _Handler.Client.Client.Send(Utils.MAGIC_HEADER);
            if (Connected != null) {
                Connected(this, null);
            }
        }

        /// <summary>
        /// Перенаправление ответа готовности сервера на локальное событие начала отправки
        /// </summary>
        /// <param name="sender">Обработчик</param>
        /// <param name="client">Клиент-инициатор</param>
        /// <param name="entry">Запись файла</param>
        private void FileHandler_FileReceiveReady(FileHandler sender, TcpClient client, FileEntry entry) {
            if (SendReady != null) {
                SendReady(this, entry);
            }
        }

        /// <summary>
        /// Изменение статуса передачи файла
        /// </summary>
        /// <param name="sender">Обработчик</param>
        /// <param name="client">Клиент</param>
        /// <param name="entry">Запись файла</param>
        /// <param name="packetNumber">Связанный с изменением номер пакета</param>
        private void FileHandler_FileTransferStatusChanged(FileHandler sender, TcpClient client, FileEntry entry, int packetNumber) {
            if (StatusChanged != null) {
                StatusChanged(sender, client, entry, packetNumber);
            }
        }

        /// <summary>
        /// Отключиться от сервера
        /// </summary>
        /// <param name="message">Сообщение отключения (опционально)</param>
        private void OnDisconnected(string message = null) {
            if (Disconnected != null) {
                Disconnected(this, message);
            }
        }

        /// <summary>
        /// Происходит при завершении передачи файла
        /// </summary>
        /// <param name="sender">Обработчик</param>
        /// <param name="client">Инстанс сервера</param>
        /// <param name="entry">Запись файла</param>
        private void FileHandler_FileReceiveDone(FileHandler sender, TcpClient client, FileEntry entry) {
            FileCollection.RemoveEntry(entry.Header.Guid);
            if (ReceiveDone != null) {
                ReceiveDone(this, entry);
            }
        }

        /// <summary>
        /// Происходит при завершении отправки файла
        /// </summary>
        /// <param name="sender">Обработчик</param>
        /// <param name="client">Инстанс сервера</param>
        /// <param name="entry">Запись файла</param>
        private void FileHandler_FileSendDone(FileHandler sender, TcpClient client, FileEntry entry) {
            FileCollection.RemoveEntry(entry.Header.Guid);
            if (SendDone != null) {
                SendDone(this, entry);
            }
        }

        private void PacketManager_FileSendRequest(FileHandler sender, FileHeader data) {
            if (SendRequest != null) {
                SendRequest(sender, data);
            }
        }

        public bool IsConnected {
            get {
                bool connected = _Handler != null;
                if (connected) {
                    connected = Utils.IsConnected(_Client.Client);
                }
                return connected;
            }
        }

        public void SendFile(string filePath) {
            _Handler.FileHandler.SendFile(filePath);
        }

        public ClientHandler Handler {
            get {
                return _Handler;
            }
        }
    }
}