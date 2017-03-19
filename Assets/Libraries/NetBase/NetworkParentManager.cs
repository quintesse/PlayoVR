namespace NetBase {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public class NetworkParentManager : NetworkBehaviour {
        private int parentId;
        private string parentPath;
        private int prevParentId;
        private string prevParentPath;

        private int GetParentId() {
            if (transform.parent != null) {
                NetworkAttachment na = transform.parent.GetComponentInParent<NetworkAttachment>();
                if (na != null) {
                    return -na.id;
                }
                PhotonView pv = transform.parent.GetComponentInParent<PhotonView>();
                if (pv != null) {
                    return pv.viewID;
                }
                PhotonViewLink pvl = transform.parent.GetComponentInParent<PhotonViewLink>();
                if (pvl != null) {
                    return pvl.linkedView.viewID;
                }
            }
            return 0;
        }

        private string GetParentPath() {
            if (transform.parent != null) {
                NetworkAttachment na = transform.parent.GetComponentInParent<NetworkAttachment>();
                if (na != null) {
                    return null; // TODO see if we can return some path here
                }
                PhotonView pv = transform.parent.GetComponentInParent<PhotonView>();
                if (pv != null) {
                    return NetUtils.RelPath(transform.parent, pv.transform);
                } else {
                    PhotonViewLink pvl = transform.parent.GetComponentInParent<PhotonViewLink>();
                    if (pvl != null) {
                        return null; // TODO see if we can return some path here
                    } else {
                        return NetUtils.GetPath(transform.parent);
                    }
                }
            }
            return null;
        }

        protected override void Obtain() {
            parentId = GetParentId();
            parentPath = GetParentPath();
        }

        protected override bool HasChanged() {
            return parentId != prevParentId || parentPath != prevParentPath;
        }

        protected override void Serialize(PhotonStream stream, PhotonMessageInfo info) {
            Debug.Log("Ser NPM " + stream.isWriting + " " + NetUtils.GetPath(transform));
            stream.Serialize(ref parentId);
            stream.Serialize(ref parentPath);
        }

        protected override void Retain() {
            prevParentId = parentId;
            prevParentPath = parentPath;
        }

        protected override void Apply() {
            int actualParentView = GetParentId();
            string actualParentPath = GetParentPath();
            //Debug.Log("Recvd " + parentView + ":" + parentPath);
            if (actualParentView != parentId || actualParentPath != parentPath) {
                //Debug.Log("Reparenting from " + actualParentView + ":" + actualParentPath + " to " + parentView + ":" + parentPath);
                Transform newParent;
                if (parentId > 0) {
                    PhotonView pv = PhotonView.Find(parentId);
                    newParent = pv != null ? pv.transform : null;
                } else if (parentId < 0) {
                    NetworkAttachment na = NetworkAttachment.Find(parentId);
                    newParent = na != null ? na.transform : null;
                } else {
                    newParent = null;
                }
                Transform child = NetUtils.Find(newParent, parentPath);
                if (child != null) {
                    newParent = child;
                } else {
                    //Debug.Log("No child path");
                }
                //Debug.Log("New parent " + newParent);
                transform.parent = newParent != null ? newParent.transform : null;
            }
            base.Apply();
        }
    }
}
