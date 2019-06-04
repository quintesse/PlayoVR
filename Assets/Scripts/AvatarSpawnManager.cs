namespace PlayoVR {
    using Photon.Pun;
    using Photon.Realtime;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using VRTK;
    using Hashtable = ExitGames.Client.Photon.Hashtable;

    public class AvatarSpawnManager : MonoBehaviourPunCallbacks
    {
        [Tooltip("Reference to the player avatar prefab")]
        public GameObject playerAvatar;

        private GameObject[] spawnPoints;
        private bool sceneLoaded = false;
        private bool connected = false;

        // we set up our available spawnpoints
        void Awake() {
            if (playerAvatar == null) {
                Debug.LogError("AvatarSpawnManager is missing a reference to the player avatar prefab!");
            }
            spawnPoints = GameObject.FindGameObjectsWithTag("Respawn");
            if (spawnPoints.Length == 0) {
                Debug.LogError("No spawn points were found!");
            }
        }

        public override void OnEnable() {
            base.OnEnable();
            //Debug.Log("AvatarSpawnManager.OnEnable has been called");
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public override void OnDisable() {
            base.OnDisable();
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            Debug.Log("Scene loaded");
            sceneLoaded = true;
        }

        // when the local player connects we use the photon callback OnJoinedRoom in order to set the name for this player and if we are the master clint we also initialize the  
        public override void OnJoinedRoom() {
            //Debug.Log("AvatarSpawnManager.OnJoinedRoom has been called");
            connected = true;
            // Player sets its own name when joining
            PhotonNetwork.NickName = playerName(PhotonNetwork.LocalPlayer);
            // Initialize the master client
            InitPlayer(PhotonNetwork.LocalPlayer);
        }

        public override void OnPlayerEnteredRoom(Player newPlayer) {
            Debug.Log("Someone else tried to join");
            InitPlayer(newPlayer);
        }

        public override void OnPlayerLeftRoom(Player otherPlayer) {
        }

        void InitPlayer(Player newPlayer) {
            if (PhotonNetwork.IsMasterClient && connected && sceneLoaded) {
                // The master client tells everyone about the new player
                Hashtable props = new Hashtable();
                props[PlayerPropNames.PLAYER_NR] = playerNr(newPlayer);
                newPlayer.SetCustomProperties(props);
                photonView.RPC("SpawnAvatar", newPlayer);
            }
        }

        [PunRPC]
        void SpawnAvatar() {
            if (!PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(PlayerPropNames.PLAYER_NR)) {
                Debug.LogError("Player does not have a PLAYER_NR property!");
                return;
            }
            int nr = (int)PhotonNetwork.LocalPlayer.CustomProperties[PlayerPropNames.PLAYER_NR];
            // Create a new player at the appropriate spawn spot
            var trans = spawnPoints[nr].transform;
            var name = PhotonNetwork.NickName;
            var player = PhotonNetwork.Instantiate(playerAvatar.name, trans.position, trans.rotation, 0, new object[] { name });
        }

        private string playerName(Player ply) {
            return "Player " + ply.ActorNumber;
        }

        private int playerNr(Player ply) {
            // TODO: do something a bit more clever here
            // We want players to actually show up in an empty spot
            return PhotonNetwork.PlayerListOthers.Length;
        }
    }
}

