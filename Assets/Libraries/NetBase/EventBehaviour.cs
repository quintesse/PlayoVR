using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetBase;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public abstract class EventBehaviour : MonoBehaviour {
    protected const byte EVENT_CODE_BASE = 141;

    protected static void HandleOnEvent<T>(Hashtable content, int senderid) where T : EventBehaviour {
        int parentId = (int)content["$d"];
        string path = (string)content["$p"];
        NetworkReference nref = NetworkReference.FromIdAndPath(parentId, path);
        T comp = nref.FindComponent<T>();
        if (comp != null) {
            comp.OnEvent(content, senderid);
        }
    }

    protected void RaiseEvent(byte eventcode, NetworkReference nref, Hashtable content) {
        content.Add("$d", nref.parentHandleId);
        content.Add("$p", nref.pathFromParent != null ? nref.pathFromParent : "");
        PhotonNetwork.RaiseEvent(eventcode, content, true, null);
    }


    protected abstract void OnEvent(Hashtable content, int senderid);

    protected abstract void RaiseEvent(Hashtable content);
}
