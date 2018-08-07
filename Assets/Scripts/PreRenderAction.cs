using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class PreRenderAction : MonoBehaviour {

    protected abstract void Action();

    protected virtual void OnEnable() {
        Camera.onPreRender += OnCamPreRender;
    }

    protected virtual void OnDisable() {
        Camera.onPreRender -= OnCamPreRender;
    }

    protected virtual void OnCamPreRender(Camera cam) {
        Action();
    }
}
