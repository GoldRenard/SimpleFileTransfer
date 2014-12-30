using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using ProtocolLibrary.Packets;

namespace ProtocolLibrary {

    /// <summary>
    /// Коллекция контейнеров передаваемых файлов
    /// </summary>
    public static class FileCollection {
        private static ConcurrentDictionary<string, FileEntry> files = new ConcurrentDictionary<string, FileEntry>();

        /// <summary>
        /// Добавить новый файл в библиотеку (на основе заголовка и временного файла)
        /// </summary>
        /// <param name="fileHeader">Заголовок файла</param>
        /// <param name="outputFile">Путь до файла назначения (опционально)</param>
        /// <returns></returns>
        public static FileEntry AddFile(FileHeader fileHeader, TcpClient client, string outputFile = null, bool deleteOnClose = false) {
            FileEntry entry = new FileEntry(fileHeader, client, FileMode.Create, FileAccess.ReadWrite, outputFile, deleteOnClose);
            files.TryAdd(fileHeader.Guid, entry);
            return entry;
        }

        /// <summary>
        /// Добавить новый файл в библиотеку (на основе пути до файла и размера пакета)
        /// </summary>
        /// <param name="fileName">Путь до файла</param>
        /// <param name="PacketLength">Размер передаваемого пакета</param>
        /// <param name="fileMode">Режим открытия файла</param>
        /// <param name="fileAccess">Режим доступа к файлу</param>
        /// <returns>Запись файла библиотеки</returns>
        public static FileEntry AddFile(string fileName, int PacketLength, TcpClient client, FileMode fileMode, FileAccess fileAccess) {
            FileEntry entry = new FileEntry(fileName, PacketLength, client, fileMode, fileAccess);
            files.TryAdd(entry.Header.Guid, entry);
            return entry;
        }

        /// <summary>
        /// Получить запись файла для указанного идентификатора
        /// </summary>
        /// <param name="guid">Идентификатор</param>
        /// <returns>Запись файла библиотеки</returns>
        public static FileEntry GetEntry(String guid) {
            FileEntry file;
            files.TryGetValue(guid, out file);
            return file;
        }

        /// <summary>
        /// Удалить файл из библиотеки для указанного идентификатора
        /// </summary>
        /// <param name="guid">Идентификатор</param>
        /// <returns>True в случае успеха операции</returns>
        public static bool RemoveEntry(String guid) {
            FileEntry entry;
            if (files.TryRemove(guid, out entry)) {
                entry.Close();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Получение библиотеки файлов
        /// </summary>
        public static ConcurrentDictionary<string, FileEntry> Files {
            get {
                return files;
            }
        }
    }
}