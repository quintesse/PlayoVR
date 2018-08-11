namespace NetBase {
    using UnityEngine;
    using Hashtable = ExitGames.Client.Photon.Hashtable;

    public abstract class NetworkBehaviour : MonoBehaviour {

        protected abstract string PropKey { get; }

        protected abstract void RecvState(Hashtable content);

        //
        // Custom property handling
        //

        protected sealed class PropertyEventHandler : MonoBehaviour {
            private static PropertyEventHandler instance;

            private PropertyEventHandler() { }

            public static PropertyEventHandler Instance {
                get {
                    if (instance == null) {
                        GameObject anchor = new GameObject("[PropertyEventHandlerAnchor]");
                        //anchor.hideFlags = HideFlags.HideAndDontSave;
                        instance = anchor.AddComponent<PropertyEventHandler>();
                    }
                    return instance;
                }
            }

            void OnPhotonCustomRoomPropertiesChanged(Hashtable props) {
                foreach (object key in props.Keys) {
                    if (key is string) {
                        var parts = key.ToString().Split('$');
                        if (parts.Length >= 3) {
                            // Could be one of our properties
                            string id = parts[0] + "$";
                            int parentId = int.Parse(parts[1]);
                            string path = parts[2];
                            Hashtable content = (Hashtable)props[key];
                            NetworkReference nref = NetworkReference.FromIdAndPath(parentId, path);
                            NetworkBehaviour[] comps = nref.FindComponents<NetworkBehaviour>();
                            foreach (NetworkBehaviour comp in comps) {
                                if (comp.PropKey.StartsWith(id)) {
                                    Debug.Log("RVD PROPS: " + content.ToString());
                                    comp.RecvState(content);
                                }
                            }
                        }
                    }
                }
            }
        }

        protected void SetProperties(Hashtable content) {
            Hashtable props = new Hashtable();
            props.Add(PropKey, content);
            PhotonNetwork.room.SetCustomProperties(props);
            Debug.Log("SNT PROPS: " + content.ToString());
        }
    }
}
