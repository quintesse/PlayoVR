namespace NetVRTK {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using VRTK;

    [RequireComponent(typeof(VRTK_InteractableObject)), RequireComponent(typeof(PhotonView))]
    public class NetworkGrabManager : Photon.MonoBehaviour {
        private int grabOwner;
        private int grabParent;
        private string grabAttachPath;

        private bool oldActive;
        private int oldGrabOwner;
        private int oldGrabParent;
        private string oldGrabAttachPath;

        private VRTK_TransformFollow transformFollow = null;
        private Rigidbody rigidBody = null;
        private bool isKinematic;

        void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info) {
            bool curActive = this.gameObject.GetActive();
            if (stream.isWriting) {
                if (HasChanged()) {
                    stream.Serialize(ref curActive);
                    stream.Serialize(ref grabOwner);
                    stream.Serialize(ref grabParent);
                    stream.Serialize(ref grabAttachPath);
                }
            } else {
                stream.Serialize(ref curActive);
                stream.Serialize(ref grabOwner);
                stream.Serialize(ref grabParent);
                stream.Serialize(ref grabAttachPath);
                this.gameObject.SetActive(curActive);
                if (oldGrabParent != grabParent || oldGrabAttachPath != grabAttachPath) {
                    PerformGrab();
                }
            }
            MemorizeState();
        }

        private bool HasChanged() {
            return oldActive != this.gameObject.GetActive() || oldGrabOwner != grabOwner ||
                oldGrabParent != grabParent || oldGrabAttachPath != grabAttachPath;
        }

        private void PerformGrab() {
            if (grabOwner != 0) {
                PhotonView parentpv = PhotonView.Find(grabParent);
                if (parentpv != null) {
                    Transform newParent = parentpv.transform;
                    if (grabAttachPath != null) {
                        Transform attach = newParent.Find(grabAttachPath);
                        if (attach != null) {
                            newParent = attach;
                        }
                    }
                    if (transformFollow == null) {
                        transformFollow = gameObject.AddComponent<VRTK_TransformFollow>();
                    }
                    transformFollow.gameObjectToFollow = newParent.gameObject;
                    transformFollow.followsScale = false;
                    transformFollow.enabled = true;
                    if (rigidBody != null) {
                        isKinematic = rigidBody.isKinematic;
                        rigidBody.isKinematic = true;
                    }
                }
            } else {
                if (transformFollow != null) {
                    transformFollow.enabled = false;
                    if (rigidBody != null) {
                        rigidBody.isKinematic = isKinematic;
                    }
                }
            }
        }

        private PhotonView GetAvatarHandView() {
            if (transform.parent != null) {
                PhotonView pv = GetComponentInParent<PhotonView>();
                return pv;
            }
            return null;
        }

        private void InitState(int ownerId) {
            grabOwner = ownerId;
            grabParent = 0;
            grabAttachPath = null;
            if (grabOwner != 0) {
                PhotonView pv = GetAvatarHandView();
                if (pv != null) {
                    grabParent = pv.viewID;
                    //PlayerAvatarLink linker = GetComponentInParent<PlayerAvatarLink>();
                    //grabAttachPath = NetUtils.RelPath(transform.parent, linker.transform.parent);
                }
            }
        }

        private void MemorizeState() {
            oldActive = this.gameObject.GetActive(); ;
            oldGrabOwner = grabOwner;
            oldGrabParent = grabParent;
            oldGrabAttachPath = grabAttachPath;
        }

        void Awake() {
            rigidBody = GetComponent<Rigidbody>();
            VRTK_InteractableObject obj = GetComponent<VRTK_InteractableObject>();
            obj.InteractableObjectGrabbed += new InteractableObjectEventHandler(HandleGrab);
            obj.InteractableObjectUngrabbed += new InteractableObjectEventHandler(HandleUngrab);
            InitState(photonView.ownerId);
            MemorizeState();
        }

        private void HandleGrab(object sender, InteractableObjectEventArgs e) {
            photonView.TransferOwnership(PhotonNetwork.player);
            InitState(PhotonNetwork.player.ID);
        }

        private void HandleUngrab(object sender, InteractableObjectEventArgs e) {
            photonView.TransferOwnership(PhotonNetwork.player);
            InitState(0);
        }
    }
}
