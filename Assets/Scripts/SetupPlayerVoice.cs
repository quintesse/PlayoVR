using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetupPlayerVoice : MonoBehaviour {
    public GameObject DF_Voice;
    public GameObject PUN_Voice;

	void Awake() {
        string voice = VoiceManager.GetSelectedVoiceComponent();
        if (voice != null) {
            Transform child = transform.Find(voice);
            if (child != null) {
                child.gameObject.SetActive(true);
            }
        }
	}

}
