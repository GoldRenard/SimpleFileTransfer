using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using ProtocolLibrary;
using ProtocolLibrary.Packets;

namespace FileTransferServer {

    /// <summary>
    /// Сервер передачи файлов
    /// </summary>
    internal class Server {
        private static readonly log4net.ILog LOGGER = log4net.LogManager.GetLogger(typeof(Server));
        private TcpListener Listener;

        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

        private delegate bool EventHandler(CtrlType sig);

        private static EventHandler _handler;

        private enum CtrlType {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        private const string TITLE_FORMAT = "Connected clients: {0}";

        /// <summary>
        /// Инициализация сервера на указанном порту
        /// </summary>
        /// <param name="port"></param>
        public Server(int port) {
            _handler += new EventHandler(ShutdownHandler);
            SetConsoleCtrlHandler(_handler, true);
            LOGGER.InfoFormat("Starting server in {0} port", port);
            Listener = new TcpListener(IPAddress.Any, port);
            Listener.Start();

            Console.Title = String.Format(TITLE_FORMAT, 0);
            ClientPool.Changed += (e) => {
                Console.Title = String.Format(TITLE_FORMAT, e);
            };

            while (true) {
                TcpClient client = Listener.AcceptTcpClient();
                byte[] checkMagic = new byte[Utils.MAGIC_HEADER.Length];
                client.Client.Receive(checkMagic);
                if (!checkMagic.SequenceEqual(Utils.MAGIC_HEADER)) {
                    LOGGER.InfoFormat("Wrong client protocol: {0}", client.Client.RemoteEndPoint);
                    client.Close();
                    continue;
                }
                LOGGER.InfoFormat("Client {0} connected", client.Client.RemoteEndPoint);
                ClientHandler handler = ClientHandler.AsyncHandler(client);
                handler.Disconnected += client_Disconnected;
                handler.FileHandler.FileSendRequest += fileHandler_FileSendRequest;
                handler.FileHandler.FileReceiveDone += FileHandler_FileReceiveDone;
                handler.FileHandler.FileSendDone += FileHandler_FileSendDone;
                handler.FileHandler.FileReceiveDenied += FileHandler_FileReceiveDenied;
            }
        }

        /// <summary>
        /// По завершении программы нужно удалить все временные файлы
        /// </summary>
        /// <param name="sig"></param>
        /// <returns></returns>
        private bool ShutdownHandler(CtrlType sig) {
            foreach (FileEntry entry in FileCollection.Files.Values) {
                entry.Close();
            }
            return true;
        }

        private void client_Disconnected(ClientHandler sender) {
            List<FileEntry> toRemove = new List<FileEntry>();
            foreach (FileEntry entry in FileCollection.Files.Values) {
                if (!entry.TransferDone && entry.Owner.Equals(sender.Client)) {
                    toRemove.Add(entry);
                }
            }

            FileEntry targetEntry;
            foreach (FileEntry entry in toRemove) {
                if (FileCollection.Files.TryRemove(entry.Header.Guid, out targetEntry)) {
                    targetEntry.Close();
                }
            }
        }

        /// <summary>
        /// Получен запретный ответ на передачу файла
        /// </summary>
        /// <param name="sender">Обработчик</param>
        /// <param name="client">Клиент</param>
        /// <param name="entry">Запись файла</param>
        private void FileHandler_FileReceiveDenied(FileHandler sender, TcpClient client, FileEntry entry) {
            LOGGER.InfoFormat("Client '{0}' denied file transfer of {1}", client, entry.Header);
        }

        /// <summary>
        /// Ивент получения файла для ретрансляции клиентам
        /// </summary>
        /// <param name="sender">Обработчик</param>
        /// <param name="client">Клиент-отправитель</param>
        /// <param name="entry">Запись файла</param>
        private void FileHandler_FileReceiveDone(FileHandler sender, TcpClient client, FileEntry entry) {
            LOGGER.InfoFormat("File '{0}' receive done for {1}: {2}", entry.Header, client.Client.RemoteEndPoint, entry.Info.FullName);
            entry.Flush();
            foreach (TcpClient target in ClientPool.Clients.Keys) {
                if (!target.Equals(client)) {
                    FileHandler.SendFile(target, entry);
                }
            }
        }

        /// <summary>
        /// Завершение отправки файла клиенту
        /// </summary>
        /// <param name="sender">Обработчик</param>
        /// <param name="client">Клиент-получатель</param>
        /// <param name="entry">Запись файла</param>
        private void FileHandler_FileSendDone(FileHandler sender, TcpClient client, FileEntry entry) {
            LOGGER.InfoFormat("File '{0}' send done for {1}", entry.Header, client.Client.RemoteEndPoint);
        }

        /// <summary>
        /// Запрос на прием файла - мы всегда подтверждаем
        /// </summary>
        /// <param name="sender">Обработчик</param>
        /// <param name="header">Заголовок файла</param>
        private void fileHandler_FileSendRequest(FileHandler sender, FileHeader header) {
            sender.AcceptFileRequest(true, header, null, true);
        }

        ~Server() {
            if (Listener != null) {
                Listener.Stop();
            }
        }
    }
}