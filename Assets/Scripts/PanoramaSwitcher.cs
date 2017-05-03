using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRTK;
using NetBase;

[RequireComponent(typeof(VRTK_SnapDropZone)), RequireComponent(typeof(PhotonView))]
public class PanoramaSwitcher : Photon.MonoBehaviour {
    public GameObject projectionSphere;

    private VRTK_SnapDropZone dropZone;
    private Renderer projSphereRenderer;

    private Color transparent = new Color(1, 1, 1, 0);

    void Awake() {
        dropZone = GetComponent<VRTK_SnapDropZone>();
        projSphereRenderer = projectionSphere.GetComponent<Renderer>();
    }

    void OnEnable() {
        dropZone.ObjectSnappedToDropZone += HandleSnappedToDropZone;
        dropZone.ObjectUnsnappedFromDropZone += HandleUnsnappedFromDropZone;
    }

    void OnDisable() {
        dropZone.ObjectSnappedToDropZone -= HandleSnappedToDropZone;
        dropZone.ObjectUnsnappedFromDropZone -= HandleUnsnappedFromDropZone;
    }

    private void HandleSnappedToDropZone(object sender, SnapDropZoneEventArgs e) {
        var nref = NetworkReference.FromObject(e.snappedObject);
        photonView.RPC("SetPanoramaFromObject", PhotonTargets.AllBufferedViaServer, nref.parentHandleId, nref.pathFromParent);
    }

    private void HandleUnsnappedFromDropZone(object sender, SnapDropZoneEventArgs e) {
        projSphereRenderer.material.SetColor("_Color", transparent);
        projSphereRenderer.material.SetTexture("_MainTex", null);
    }

    [PunRPC]
    private void SetPanoramaFromObject(int refId, string refPath) {
        GameObject obj = NetworkReference.FromIdAndPath(refId, refPath).FindObject();
        if (obj != null) {
            Renderer objectRenderer = obj.GetComponent<Renderer>();
            if (objectRenderer != null) {
                projSphereRenderer.material.SetTexture("_MainTex", objectRenderer.material.GetTexture("_MainTex"));
                projSphereRenderer.material.SetColor("_Color", Color.white);
            }
        }
    }
}
