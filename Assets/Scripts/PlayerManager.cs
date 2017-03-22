using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : Photon.PunBehaviour {
    private GameObject[] spawns;

    [Tooltip("Reference to the player avatar prefab")]
    public GameObject playerAvatar;

    public delegate void OnCharacterInstantiated(GameObject character);

    public static event OnCharacterInstantiated CharacterInstantiated;

    void Awake() {
        if (playerAvatar == null) {
            Debug.LogError("MyNetworkManager is missing a reference to the player avatar prefab!");
        }
        spawns = GameObject.FindGameObjectsWithTag("Respawn");
    }

    public override void OnJoinedRoom() {
        if (PhotonNetwork.isMasterClient) {
            NewPlayer(0);
        }
    }

    public override void OnPhotonPlayerConnected(PhotonPlayer newPlayer) {
        if (PhotonNetwork.isMasterClient) {
            var idx = PhotonNetwork.otherPlayers.Length;
            photonView.RPC("NewPlayer", newPlayer, idx);
        }
    }

    [PunRPC]
    void NewPlayer(int idx) {
        // Create a new player at the appropriate spawn spot
        var trans = spawns[idx].transform;
        var player = PhotonNetwork.Instantiate(playerAvatar.name, trans.position, trans.rotation, 0);
        player.name = "Player " + (idx + 1);
    }
}
