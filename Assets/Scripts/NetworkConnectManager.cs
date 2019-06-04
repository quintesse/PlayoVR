namespace PlayoVR {
    using Photon.Pun;
    using Photon.Realtime;
    using UnityEngine;

    public class NetworkConnectManager : MonoBehaviourPunCallbacks
    {
        public static string gameVersion = "0.4";

        [Tooltip("The maximum number of players per room. When a room is full, it can't be joined by new players, and so new room will be created")]
        public byte MaxPlayersPerRoom = 4;
        public PunLogLevel Loglevel = PunLogLevel.Informational;

        void Awake() {
            //if (!PhotonNetwork.connecting && !PhotonNetwork.connected) {
            if (!PhotonNetwork.IsConnected) {
                //PhotonNetwork.autoJoinLobby = false;    // we join randomly. always. no need to join a lobby to get the list of rooms.
                PhotonNetwork.AutomaticallySyncScene = false;
                PhotonNetwork.LogLevel = Loglevel;
                //PhotonNetwork.ConnectUsingSettings(gameVersion);
                PhotonNetwork.ConnectUsingSettings();
            }
        }

        public override void OnConnectedToMaster() {
            Debug.Log("Connected to master");

            Debug.Log("Joining random room...");
            PhotonNetwork.JoinRandomRoom();
        }

        public override void OnJoinedLobby() {
            Debug.Log("Joined lobby");

            Debug.Log("Joining random room...");
            PhotonNetwork.JoinRandomRoom();
        }

        public override void OnLeftLobby() {
            Debug.Log("Left lobby");
        }

        public override void OnJoinRandomFailed(short numbr, string strg) {
            Debug.Log("Can't join random room!");

            Debug.Log("Creating room...");
            var options = new RoomOptions();
            options.MaxPlayers = MaxPlayersPerRoom;
            PhotonNetwork.CreateRoom(null, options, null);
        }

        public override void OnCreatedRoom() {
            Debug.Log("Created room");
        }

        public override void OnJoinedRoom() {
            Debug.Log("Joined room");
        }

        public override void OnLeftRoom() {
            Debug.Log("Left room");
        }

        public override void OnPlayerEnteredRoom(Player newPlayer) {
            Debug.Log("Player connected");
        }

        public override void OnPlayerLeftRoom(Player otherPlayer) {
            Debug.Log("Player disconnected");
        }

        public override void OnDisconnected(DisconnectCause cause) {
            Debug.Log("Couldn't connect to Photon network");
            Debug.Log(cause);
        }
    }
}
