namespace NetBase {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public class NetworkParentableObject : NetworkObject {
        private int parentView;
        private string parentPath;
        private int prevParentView;
        private string prevParentPath;

        private int GetParentView() {
            if (transform.parent != null) {
                PhotonView pv = transform.parent.GetComponentInParent<PhotonView>();
                if (pv != null) {
                    return pv.viewID;
                }
            }
            return -1;
        }

        private string GetParentPath() {
            if (transform.parent != null) {
                PhotonView pv = transform.parent.GetComponentInParent<PhotonView>();
                if (pv != null) {
                    return NetUtils.RelPath(transform.parent, pv.transform);
                } else {
                    return NetUtils.GetPath(transform.parent);
                }
            }
            return null;
        }

        protected override void Obtain() {
            base.Obtain();
            parentView = GetParentView();
            parentPath = GetParentPath();
        }

        protected override bool HasChanged() {
            return base.HasChanged() || parentView != prevParentView || parentPath != prevParentPath;
        }

        protected override void Write(PhotonStream stream, PhotonMessageInfo info) {
            base.Write(stream, info);
            stream.Serialize(ref parentView);
            stream.Serialize(ref parentPath);
        }

        protected override void Retain() {
            base.Retain();
            prevParentView = parentView;
            prevParentPath = parentPath;
        }

        protected override void Read(PhotonStream stream, PhotonMessageInfo info) {
            base.Read(stream, info);
            stream.Serialize(ref parentView);
            stream.Serialize(ref parentPath);
        }

        protected override void Apply() {
            int actualParentView = GetParentView();
            string actualParentPath = GetParentPath();
            //Debug.Log("Recvd " + parentView + ":" + parentPath);
            if (actualParentView != parentView || actualParentPath != parentPath) {
                //Debug.Log("Reparenting from " + actualParentView + ":" + actualParentPath + " to " + parentView + ":" + parentPath);
                PhotonView pv = PhotonView.Find(parentView);
                Transform newParent = pv != null ? pv.transform : null;
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
