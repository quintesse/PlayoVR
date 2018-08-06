using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerConnectManager : Photon.PunBehaviour {
    private GameObject[] spawns;

    [Tooltip("Reference to the player avatar prefab")]
    public GameObject playerAvatar;

    public delegate void OnCharacterInstantiated(GameObject character);

    public static event OnCharacterInstantiated CharacterInstantiated;

    void Awake() {
        if (playerAvatar == null) {
            Debug.LogError("PlayerConnectManager is missing a reference to the player avatar prefab!");
        }
        spawns = GameObject.FindGameObjectsWithTag("Respawn");
    }

    public override void OnJoinedRoom() {
        if (PhotonNetwork.isMasterClient) {
            NewPlayer(0, playerName(PhotonNetwork.player));
        }
    }

    public override void OnPhotonPlayerConnected(PhotonPlayer newPlayer) {
        if (PhotonNetwork.isMasterClient) {
            var idx = PhotonNetwork.otherPlayers.Length;
            photonView.RPC("NewPlayer", newPlayer, idx, playerName(newPlayer));
        }
    }

    private string playerName(PhotonPlayer ply) {
        return "Player " + ply.ID;
    }

    [PunRPC]
    void NewPlayer(int idx, string name) {
        // Create a new player at the appropriate spawn spot
        var trans = spawns[idx].transform;
        var player = PhotonNetwork.Instantiate(playerAvatar.name, trans.position, trans.rotation, 0);
        player.name = name;
    }
}
