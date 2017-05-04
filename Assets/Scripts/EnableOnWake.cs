using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnableOnWake : MonoBehaviour {
    public GameObject[] objects;

	void Awake() {
        foreach (GameObject obj in objects) {
            obj.SetActive(true);
        }
	}

}
