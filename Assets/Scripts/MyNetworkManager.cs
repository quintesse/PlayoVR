namespace PlaypenVR {
    using UnityEngine;
    using Photon;

    public class MyNetworkManager : Photon.PunBehaviour {
        private GameObject[] spawns;

        [Tooltip("Reference to the player avatar prefab that will be instantiated when entering the room")]
        public GameObject playerAvatar;
        [Tooltip("The maximum number of players per room. When a room is full, it can't be joined by new players, and so new room will be created")]
        public byte MaxPlayersPerRoom = 4;
        public PhotonLogLevel Loglevel = PhotonLogLevel.Informational;

        void Awake() {
            if (playerAvatar == null) {
                Debug.LogError("MyNetworkManager is missing a reference to the player avatar prefab!");
            }
            PhotonNetwork.autoJoinLobby = true;
            PhotonNetwork.automaticallySyncScene = true;
            PhotonNetwork.logLevel = Loglevel;
            spawns = GameObject.FindGameObjectsWithTag("Respawn");
        }

        public override void OnJoinedLobby() {
            Debug.Log("Joined lobby");

            Debug.Log("Joining random room...");
            PhotonNetwork.JoinRandomRoom();
        }

        public override void OnPhotonRandomJoinFailed(object[] codeAndMsg) {
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

            if (PhotonNetwork.isMasterClient) {
                NewPlayer(0);
            }
        }

        public override void OnPhotonPlayerConnected(PhotonPlayer newPlayer) {
            Debug.Log("Player connected");

            if (PhotonNetwork.isMasterClient) {
                var idx = PhotonNetwork.otherPlayers.Length;
                photonView.RPC("NewPlayer", newPlayer, idx);
            }
        }

        public override void OnPhotonPlayerDisconnected(PhotonPlayer otherPlayer) {
            Debug.Log("Player disconnected");
        }

        public override void OnFailedToConnectToPhoton(DisconnectCause cause) {
            Debug.Log("Couldn't connect to Photon network");
        }

        public override void OnConnectionFail(DisconnectCause cause) {
            Debug.Log("Connection failed to the Photon network");
        }

        public override void OnDisconnectedFromPhoton() {
            Debug.Log("We got disconnected form the Photon network");
        }

        [PunRPC]
        void NewPlayer(int idx) {
            // Create a new player at the appropriate spawn spot
            var trans = spawns[idx].transform;
            var player = PhotonNetwork.Instantiate(playerAvatar.name, trans.position, trans.rotation, 0);
            player.name = "Player " + (idx + 1);
        }
    }
}