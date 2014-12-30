namespace ProtocolLibrary.Packets {

    public enum Command {
        FILE_SEND_REQUEST,
        FILE_RECEIVE_READY,
        FILE_RECEIVE_DENIED,
        FILE_PACKET_DATA,
        FILE_PACKET_REQUEST,
        FILE_SEND_DONE,
        FILE_RECEIVE_DONE,
        FILE_SEND_CANCEL
    }
}