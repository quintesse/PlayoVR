namespace PlayoVR {
    using UnityEngine;
    using VRTK;

    [RequireComponent(typeof(VRTK_InteractableObject))]
    public class PanoramaHeadFader : MonoBehaviour {
        public GameObject projectionSphere;
        [Tooltip("The distance where 100% opaqueness is reached")]
        public float minDistance = 0.25f;
        [Tooltip("The distance where 100% transparency is reached")]
        public float maxDistance = 0.5f;

        private VRTK_InteractableObject io;
        private Renderer projSphereRenderer;
        private Renderer objectRenderer;

        void Awake() {
            io = GetComponent<VRTK_InteractableObject>();
            io.InteractableObjectGrabbed += HandleGrab;
            io.InteractableObjectUngrabbed += HandleUngrab;
            projSphereRenderer = projectionSphere.GetComponent<Renderer>();
            objectRenderer = GetComponent<Renderer>();
            ResetAlpha();
            enabled = false;
        }

        void Update() {
            VRTK_SDKManager sdk = VRTK_SDKManager.instance;
            Vector3 hmdPos = sdk.loadedSetup.actualHeadset.transform.position;
            Vector3 objPos = transform.position;
            float dist = Vector3.Distance(hmdPos, objPos);
            float alpha;
            if (minDistance < maxDistance) {
                if (dist <= minDistance) {
                    alpha = 1f;
                } else if (dist >= maxDistance) {
                    alpha = 0f;
                } else {
                    alpha = 1f - (dist - minDistance) / (maxDistance - minDistance);
                }
            } else {
                alpha = 0f;
            }
            Color col = projSphereRenderer.material.GetColor("_Color");
            col.a = alpha;
            projSphereRenderer.material.SetColor("_Color", col);
        }

        private void HandleGrab(object sender, InteractableObjectEventArgs e) {
            projSphereRenderer.material.SetTexture("_MainTex", objectRenderer.material.GetTexture("_MainTex"));
            ResetAlpha();
            enabled = true;
        }

        private void HandleUngrab(object sender, InteractableObjectEventArgs e) {
            ResetAlpha();
            projSphereRenderer.material.SetTexture("_MainTex", null);
            enabled = false;
        }

        private void ResetAlpha() {
            Color col = projSphereRenderer.material.GetColor("_Color");
            col.a = 0f;
            projSphereRenderer.material.SetColor("_Color", col);
        }
    }
}
