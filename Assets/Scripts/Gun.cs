namespace PlaypenVR {
    using UnityEngine;
    using System.Collections;
    using VRTK;

    public class Gun : Photon.MonoBehaviour {
        public GameObject bulletPrefab;
        public Transform bulletSpawn;

        private bool fired;

        // Use this for initialization
        void Awake() {
            GetComponent<VRTK_InteractableObject>().InteractableObjectUsed += new InteractableObjectEventHandler(DoFireGun);
        }

        // Update is called once per frame
        void Update() {
            if (!photonView.isMine) {
                return;
            }

            // Handle firing
            if (fired) {
                CmdFire();
                fired = false;
            }
        }

        void CmdFire() {
            // Create the Bullet from the Bullet Prefab
            var bullet = PhotonNetwork.Instantiate(
                "Bullet",
                bulletSpawn.position,
                bulletSpawn.rotation,
                0);

            // Add velocity to the bullet
            bullet.GetComponent<Rigidbody>().velocity = bullet.transform.forward * 12;
        }

        void DoFireGun(object sender, InteractableObjectEventArgs e) {
            fired = true;
        }
    }
}