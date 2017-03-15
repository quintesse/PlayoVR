namespace NetVRTK {
    using System.Collections.Generic;
    using UnityEngine;

    [RequireComponent(typeof(PhotonView))]
    public class NetworkInterpolatingTransform : Photon.MonoBehaviour {
        public enum UpdateMode { None, Set, Lerp }

        public UpdateMode position = UpdateMode.Lerp;
        public UpdateMode rotation = UpdateMode.Lerp;
        public UpdateMode scale = UpdateMode.None;
        public UpdateMode velocity = UpdateMode.Set;
        public UpdateMode angularVelocity = UpdateMode.Set;

        [Tooltip("Use local coordinates if true, otherwise use world coordinates")]
        public bool useLocalValues = true;
        [Tooltip("Only send updates if any of the tracked values have changed")]
        public bool onChangeOnly = true;
        [Tooltip("List of Transform or RigidBody to track. If left empty the local object's Tranform and Rigidbody (if any) will be tracked")]
        public List<Component> observedComponents;

        private ComponentInterpolator[] cipols = new ComponentInterpolator[0];

        // Allow us to globaly enable/disable interpolation
        public static bool interpolationEnabled = true;

        public void Awake() {
            // Create an array of TransformInterpolator for each observed Transformation
            if (observedComponents.Count > 0) {
                cipols = new ComponentInterpolator[observedComponents.Count];
                int i = 0;
                foreach (Component c in observedComponents) {
                    cipols[i++] = new ComponentInterpolator(this, c);
                }
            } else {
                // If no specific transforms are specified we use the one in the
                // GameObject that this script is attached to
                Rigidbody rbody = GetComponent<Rigidbody>();
                cipols = new ComponentInterpolator[rbody == null ? 1 : 2];
                cipols[0] = new ComponentInterpolator(this, transform);
                if (rbody != null) {
                    cipols[1] = new ComponentInterpolator(this, rbody);
                }
            }
        }

        void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info) {
            foreach (ComponentInterpolator ci in cipols) {
                ci.OnPhotonSerializeView(stream, info);
            }
        }

        void Update() {
            if (!photonView.isMine) {
                double currentTime = PhotonNetwork.time;
                double interpolationTime = currentTime - GetInterpolationBackTime();
                foreach (ComponentInterpolator ci in cipols) {
                    ci.Update(interpolationTime);
                }
            }
        }

        private double GetInterpolationBackTime() {
            int interpolationBackTime;
            int ping = PhotonNetwork.GetPing();
            if (ping < 50) {
                interpolationBackTime = 50;
            } else if (ping < 100) {
                interpolationBackTime = 100;
            } else if (ping < 200) {
                interpolationBackTime = 200;
            } else if (ping < 400) {
                interpolationBackTime = 400;
            } else if (ping < 600) {
                interpolationBackTime = 600;
            } else {
                interpolationBackTime = 1000;
            }
            return interpolationBackTime / 1000d;
        }

        class ComponentInterpolator {
            private NetworkInterpolatingTransform nit;
            private Component component;

            internal struct State {
                internal double timestamp;
                internal Vector3 pos;
                internal Quaternion rot;
                internal Vector3 scale;
                internal Vector3 v;
                internal Vector3 angv;
            }

            // Circular buffer of last 20 updates
            private State[] states = new State[20];
            private int lastRcvdSlot = 0;
            private int nextFreeSlot = 0;
            private int slotsUsed = 0;

            public ComponentInterpolator(NetworkInterpolatingTransform nit, Component component) {
                this.nit = nit;
                this.component = component;
            }

            public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info) {
                Vector3 pos = Vector3.zero;
                Quaternion rot = Quaternion.identity;
                Vector3 scale = Vector3.one;
                Vector3 v = Vector3.zero;
                Vector3 angv = Vector3.zero;
                if (stream.isWriting) {
                    if (component is Transform) {
                        Transform transform = (Transform)component;
                        if (nit.useLocalValues) {
                            pos = transform.localPosition;
                            rot = transform.localRotation;
                        } else {
                            pos = transform.position;
                            rot = transform.rotation;
                        }
                        scale = transform.localScale;
                        if (!nit.onChangeOnly || slotsUsed == 0 || hasChanged(pos, rot, scale, v, angv)) {
                            // Send update
                            if (nit.position != UpdateMode.None)
                                stream.Serialize(ref pos);
                            if (nit.rotation != UpdateMode.None)
                                stream.Serialize(ref rot);
                            if (nit.scale != UpdateMode.None)
                                stream.Serialize(ref scale);
                        }
                    } else if (component is Rigidbody) {
                        Rigidbody rbody = (Rigidbody)component;
                        v = rbody.velocity;
                        angv = rbody.angularVelocity;
                        if (!nit.onChangeOnly || slotsUsed == 0 || hasChanged(pos, rot, scale, v, angv)) {
                            // Send update
                            if (nit.velocity != UpdateMode.None)
                                stream.Serialize(ref v);
                            if (nit.angularVelocity != UpdateMode.None)
                                stream.Serialize(ref angv);
                        }
                    }
                    if (nit.onChangeOnly) {
                        // Keep a copy
                        State state;
                        state.timestamp = info.timestamp;
                        state.pos = pos;
                        state.rot = rot;
                        state.scale = scale;
                        state.v = v;
                        state.angv = angv;
                        states[0] = state;
                        slotsUsed = 1;
                    }
                } else {
                    // Receive updated state information
                    if (component is Transform) {
                        Transform transform = component.transform;
                        if (nit.position != UpdateMode.None)
                            stream.Serialize(ref pos);
                        if (nit.rotation != UpdateMode.None)
                            stream.Serialize(ref rot);
                        if (nit.scale != UpdateMode.None)
                            stream.Serialize(ref scale);
                    } else if (component is Rigidbody) {
                        Rigidbody rbody = (Rigidbody)component;
                        if (nit.velocity != UpdateMode.None)
                            stream.Serialize(ref v);
                        if (nit.angularVelocity != UpdateMode.None)
                            stream.Serialize(ref angv);
                    }
                    // Make sure the update was received in the correct order
                    // If not we simply drop it
                    if (slotsUsed == 0 || states[lastRcvdSlot].timestamp <= info.timestamp) {
                        // Save currect received state in the next free slot
                        State state;
                        state.timestamp = info.timestamp;
                        state.pos = pos;
                        state.rot = rot;
                        state.scale = scale;
                        state.v = v;
                        state.angv = angv;
                        states[nextFreeSlot] = state;
                        // Increment nextSlot wrapping around to 0 when getting to the end
                        lastRcvdSlot = nextFreeSlot;
                        nextFreeSlot = (nextFreeSlot + 1) % states.Length;
                        // Increment the number of unhandled updates currently in the buffer
                        slotsUsed = Mathf.Min(slotsUsed + 1, states.Length);
                    }
                }
            }

            private bool hasChanged(Vector3 pos, Quaternion rot, Vector3 scale, Vector3 v, Vector3 angv) {
                // TODO enable check again, but for that we need to implement sparse data checks
                // TODO allow for some fuzziness in the checks
                return true; // states[0].pos != pos || states[0].rot != rot || states[0].scale != scale || states[0].v != v || states[0].angv != angv;
            }

            public void Update(double interpolationTime) {
                // We have a window of interpolationBackTime where we basically play 
                // By having interpolationBackTime the average ping, you will usually use interpolation.
                // And only if no more data arrives we will use extrapolation

                if (slotsUsed > 0) {
                    // Check if latest state is older than interpolation time, then it is too old
                    // and extrapolation should be used, otherwise use interpolation
                    State latest = states[lastRcvdSlot];
                    if (NetworkInterpolatingTransform.interpolationEnabled
                            && latest.timestamp > interpolationTime) {
                        // Loop over the available slots from newest to oldest
                        for (int n = 0; n < slotsUsed; n++) {
                            int i = (lastRcvdSlot + states.Length - n) % states.Length;
                            // Find the state which matches the interpolation time (time+0.1) or use last state
                            if (states[i].timestamp <= interpolationTime || n == (states.Length - 1)) {
                                // The state one slot newer (<100ms) than the best playback state
                                int previ = (n == 0) ? i : (i + 1) % states.Length;
                                State rhs = states[previ];
                                // The best playback state (closest to 100 ms old (default time))
                                State lhs = states[i];

                                // Use the time between the two slots to determine if interpolation is necessary
                                double length = rhs.timestamp - lhs.timestamp;
                                //Debug.Log("last:" + lastRcvdSlot + " i:" + i + " pi:" + previ + " len:" + length);
                                float t = 0.0f;
                                // As the time difference gets closer to 100 ms t gets closer to 1 in 
                                // which case rhs is only used
                                if (length > 0.0001)
                                    t = (float)((interpolationTime - lhs.timestamp) / length);
                                updateStates(lhs, rhs, t);
                                break;
                            }
                        }
                    } else {
                        // Use extrapolation. Here we do something really simple and just repeat the last
                        // received state. You can do clever stuff with predicting what should happen.
                        if (component is Transform) {
                            Transform transform = component.transform;
                            if (nit.useLocalValues) {
                                if (nit.position != UpdateMode.None)
                                    transform.localPosition = latest.pos;
                                if (nit.rotation != UpdateMode.None)
                                    transform.localRotation = latest.rot;
                            } else {
                                if (nit.position != UpdateMode.None)
                                    transform.position = latest.pos;
                                if (nit.rotation != UpdateMode.None)
                                    transform.rotation = latest.rot;
                            }
                            if (nit.scale != UpdateMode.None)
                                transform.localScale = latest.scale;
                        }
                    }
                }
            }

            protected void updateStates(State lhs, State rhs, float t) {
                if (component is Transform) {
                    Transform transform = component.transform;
                    if (nit.useLocalValues) {
                        // Position
                        if (nit.position == UpdateMode.Set) {
                            transform.localPosition = lhs.pos;
                        } else if (nit.position == UpdateMode.Lerp) {
                            transform.localPosition = Vector3.Lerp(lhs.pos, rhs.pos, t);
                        }

                        // Rotation
                        if (nit.rotation == UpdateMode.Set) {
                            transform.localRotation = lhs.rot;
                        } else if (nit.rotation == UpdateMode.Lerp) {
                            transform.localRotation = Quaternion.Slerp(lhs.rot, rhs.rot, t);
                        }
                    } else {
                        // Position
                        if (nit.position == UpdateMode.Set) {
                            transform.position = lhs.pos;
                        } else if (nit.position == UpdateMode.Lerp) {
                            transform.position = Vector3.Lerp(lhs.pos, rhs.pos, t);
                        }

                        // Rotation
                        if (nit.rotation == UpdateMode.Set) {
                            transform.rotation = lhs.rot;
                        } else if (nit.rotation == UpdateMode.Lerp) {
                            transform.rotation = Quaternion.Slerp(lhs.rot, rhs.rot, t);
                        }
                    }

                    // Scale
                    if (nit.scale == UpdateMode.Set) {
                        transform.localScale = lhs.scale;
                    } else if (nit.scale == UpdateMode.Lerp) {
                        transform.localScale = Vector3.Lerp(lhs.scale, rhs.scale, t);
                    }
                } else if (component is Rigidbody) {
                    Rigidbody rbody = (Rigidbody)component;
                    // Velocity
                    if (nit.velocity == UpdateMode.Set) {
                        rbody.velocity = lhs.v;
                    } else if (nit.velocity == UpdateMode.Lerp) {
                        rbody.velocity = Vector3.Lerp(lhs.v, rhs.v, t);
                    }

                    // Angular Velocity
                    if (nit.angularVelocity == UpdateMode.Set) {
                        rbody.angularVelocity = lhs.angv;
                    } else if (nit.angularVelocity == UpdateMode.Lerp) {
                        rbody.angularVelocity = Vector3.Lerp(lhs.angv, rhs.angv, t);
                    }
                }
            }
        }
    }
}