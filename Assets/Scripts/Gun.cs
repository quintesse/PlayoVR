using UnityEngine;
using System.Collections;
using VRTK;
using NetBase;
using NetVRTK;

public class Gun : Photon.MonoBehaviour {
    public GameObject bulletPrefab;
    public Transform bulletSpawn;
    public AudioClip fireGunSound;
    public Animation fireAnimation;

    private bool fired;

    // Use this for initialization
    void Awake() {
        GetComponent<VRTK_InteractableObject>().InteractableObjectUsed += new InteractableObjectEventHandler(DoFireGun);
    }

    // Update is called once per frame
    void Update() {
        // Handle firing
        if (fired) {
            CmdFire();
            fired = false;
        }
    }

    void CmdFire() {
        // Create the Bullet from the Bullet Prefab
        // (gets replicated automatically to all clients)
        var bullet = PhotonNetwork.Instantiate(
            "Bullet",
            bulletSpawn.position,
            bulletSpawn.rotation,
            0);

        // Now play sound and animation locally and on all other clients
        photonView.RPC("NetFire", PhotonTargets.All);
    }

    [PunRPC]
    void NetFire() {
        // Play sound of gun shooting
        AudioSource.PlayClipAtPoint(fireGunSound, transform.position, 1.0f);
        // Play animation of gun shooting
        fireAnimation.Play();
    }

    void DoFireGun(object sender, InteractableObjectEventArgs e) {
        fired = true;
    }
}
