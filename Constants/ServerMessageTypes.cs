namespace OpenGSServer;

/// <summary>
/// OpenGSServer側で使用するメッセージ種別定数
/// </summary>
public static class ServerMessageTypes
{
    public const string MessageType = "MessageType";

    // Common
    public const string Error = "Error";
    public const string ServerInfo = "ServerInfo";
    public const string EncryptKey = "EncryptKey";
    public const string ConnectServerSuccessful = "ConnectServerSuccessful";

    // Lobby requests
    public const string LoginRequest = "LoginRequest";
    public const string LogoutRequest = "LogoutRequest";
    public const string CreateNewWaitRoomRequest = "CreateNewWaitRoomRequest";
    public const string EnterRoomRequest = "EnterRoomRequest";
    public const string QuickStartRequest = "QuickStartRequest";
    public const string UpdateRoomRequest = "UpdateRoomRequest";
    public const string ExitRoomRequest = "ExitRoomRequest";
    public const string MatchServerInformationRequest = "MatchServerInformationRequest";
    public const string AddNewLobbyChat = "AddNewLobbyChat";

    // Lobby notifications
    public const string MatchServerInformationNotification = "MatchServerInformationNotification";
}

