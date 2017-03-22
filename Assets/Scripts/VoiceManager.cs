using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoiceManager : MonoBehaviour {
    public GameObject DF_Voice;
    public GameObject PUN_Voice;

    private static VoiceManager instance;
    private static object instanceLock = new object();

    private static GameObject selectedVoice;

    void Awake() {
        lock (instanceLock) {
            if (instance == null) {
                instance = this;
            } else {
                Debug.LogError("Error: Attempt to create multiple instances of VoiceManager");
            }
        }

        // Does DFVoice exist?
        if (Type.GetType("NetVoice.LocalVoiceController") == null) {
            DF_Voice = null;
        } else {
            Debug.Log("DFVoice detected");
        }

        // Does PUNVoice exist?
        if (Type.GetType("PhotonVoiceSettings") == null) {
            PUN_Voice = null;
        } else {
            Debug.Log("PUNVoice detected");
        }

        // Select a default (preferring PUNVoice)
        selectedVoice = PUN_Voice != null ? PUN_Voice : DF_Voice;

        if (Utils.HasStartupFlag("novoice")) {
            selectedVoice = null;
        } else if (Utils.HasStartupOption("voice")) {
            string voice = Utils.GetStartupOption("voice");
            for (int i = 0; i < transform.childCount; i++) {
                GameObject child = transform.GetChild(i).gameObject;
                if (child.name == voice) {
                    selectedVoice = child;
                }
            }
        }

        if (selectedVoice != null) {
            selectedVoice.SetActive(true);
            Debug.Log("Selected voice component: " + selectedVoice.name);
        } else {
            Debug.Log("Voice support disabled");
        }
    }

    public static string GetSelectedVoiceComponent() {
        return selectedVoice != null ? selectedVoice.name : null;
    }

}