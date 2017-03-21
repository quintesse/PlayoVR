using System;
using UnityEngine;

public class MyNetworkManager : Photon.PunBehaviour {
    private GameObject[] spawns;

    public string gameVersion = "1.0";

    [Tooltip("Reference to the player avatar prefab without voice support (DFVoice is not available)")]
    public GameObject mutePlayerAvatar;
    [Tooltip("Reference to the player avatar prefab with voice support (DFVoice is available)")]
    public GameObject voicedPlayerAvatar;
    [Tooltip("The maximum number of players per room. When a room is full, it can't be joined by new players, and so new room will be created")]
    public byte MaxPlayersPerRoom = 4;
    public PhotonLogLevel Loglevel = PhotonLogLevel.Informational;

    private GameObject playerAvatar;

    void Awake() {
        if (Type.GetType("NetVoice.LocalVoiceController") != null) {
            playerAvatar = voicedPlayerAvatar;
        } else {
            playerAvatar = mutePlayerAvatar;
        }
        if (playerAvatar == null) {
            Debug.LogError("MyNetworkManager is missing a reference to the player avatar prefab!");
        }
        spawns = GameObject.FindGameObjectsWithTag("Respawn");
        PhotonNetwork.autoJoinLobby = false;    // we join randomly. always. no need to join a lobby to get the list of rooms.
        PhotonNetwork.automaticallySyncScene = true;
        PhotonNetwork.logLevel = Loglevel;
        PhotonNetwork.ConnectUsingSettings(gameVersion);
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
