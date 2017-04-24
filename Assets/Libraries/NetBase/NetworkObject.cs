namespace NetBase {
    using System.Collections.Generic;
    using UnityEngine;

    [RequireComponent(typeof(PhotonView))]
    public class NetworkObject : NetworkBehaviour {
        public enum UpdateMode { None, Set, Lerp }

        [Tooltip("Update mode to use for the object's position")]
        public UpdateMode position = UpdateMode.Lerp;
        [Tooltip("Update mode to use for the object's rotation")]
        public UpdateMode rotation = UpdateMode.Lerp;
        [Tooltip("Update mode to use for the object's scale")]
        public UpdateMode scale = UpdateMode.None;
        [Tooltip("Update mode to use for the object's velocity")]
        public UpdateMode velocity = UpdateMode.Set;
        [Tooltip("Update mode to use for the object's angualr velocity")]
        public UpdateMode angularVelocity = UpdateMode.Set;

        [Tooltip("Use local coordinates if true, otherwise use world coordinates")]
        public bool useLocalValues = true;

        private ComponentInterpolator[] cipols = new ComponentInterpolator[0];

        // Allow us to globaly enable/disable interpolation
        public static bool interpolationEnabled = true;

        public void Awake() {
            Rigidbody rbody = GetComponent<Rigidbody>();
            cipols = new ComponentInterpolator[rbody == null ? 1 : 2];
            cipols[0] = new TransformInterpolator(this, transform);
            if (rbody != null) {
                cipols[1] = new RigidBodyInterpolator(this, rbody);
            }
        }

        public override void Obtain() {
            foreach (ComponentInterpolator ci in cipols) {
                ci.Obtain();
            }
        }

        public override bool HasChanged() {
            bool changed = false;
            foreach (ComponentInterpolator ci in cipols) {
                changed = changed || ci.HasChanged();
            }
            return changed;
        }

        public override void Serialize(PhotonStream stream, PhotonMessageInfo info) {
            foreach (ComponentInterpolator ci in cipols) {
                ci.Serialize(stream, info);
            }
        }

        public override void Retain() {
            foreach (ComponentInterpolator ci in cipols) {
                ci.Retain();
            }
        }

        public override void Apply() {
            foreach (ComponentInterpolator ci in cipols) {
                ci.Apply();
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

        abstract class ComponentInterpolator {
            protected NetworkObject nit;

            internal struct State {
                internal double timestamp;
                internal Vector3 pos;
                internal Quaternion rot;
                internal Vector3 scale;
                internal Vector3 v;
                internal Vector3 angv;
            }

            protected State state;
            protected State prevstate;

            // Circular buffer of last 20 updates
            private State[] states = new State[20];
            private int lastRcvdSlot = 0;
            private int nextFreeSlot = 0;
            private int slotsUsed = 0;

            public ComponentInterpolator(NetworkObject nit) {
                this.nit = nit;
            }

            public abstract void Obtain();

            public abstract bool HasChanged();

            public abstract void Serialize(PhotonStream stream, PhotonMessageInfo info);

            public virtual void Retain() {
                prevstate = state;
            }

            public virtual void Apply() {
                // Make sure the update was received in the correct order
                // If not we simply drop it
                if (slotsUsed == 0 || states[lastRcvdSlot].timestamp <= state.timestamp) {
                    // Save currect received state in the next free slot
                    states[nextFreeSlot] = state;
                    // Increment nextSlot wrapping around to 0 when getting to the end
                    lastRcvdSlot = nextFreeSlot;
                    nextFreeSlot = (nextFreeSlot + 1) % states.Length;
                    // Increment the number of unhandled updates currently in the buffer
                    slotsUsed = Mathf.Min(slotsUsed + 1, states.Length);
                }
            }

            public void Update(double interpolationTime) {
                // We have a window of interpolationBackTime where we basically play 
                // By having interpolationBackTime the average ping, you will usually use interpolation.
                // And only if no more data arrives we will use extrapolation

                if (slotsUsed > 0) {
                    // Check if latest state is older than interpolation time, then it is too old
                    // and extrapolation should be used, otherwise use interpolation
                    State latest = states[lastRcvdSlot];
                    if (NetworkObject.interpolationEnabled
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
                        updateStates(latest, latest, 1.0f);
                    }
                }
            }

            protected virtual void updateStates(State lhs, State rhs, float t) {
            }
        }

        class TransformInterpolator : ComponentInterpolator {
            private Transform transform;

            public TransformInterpolator(NetworkObject nit, Transform transform) : base(nit) {
                this.transform = transform;
                reset(ref state);
            }

            private static void reset(ref State state) {
                state.pos = Vector3.zero;
                state.rot = Quaternion.identity;
                state.scale = Vector3.one;
            }

            public override void Obtain() {
                reset(ref state);
                if (nit.useLocalValues) {
                    if (nit.position != UpdateMode.None)
                        state.pos = transform.localPosition;
                    if (nit.rotation != UpdateMode.None)
                        state.rot = transform.localRotation;
                } else {
                    if (nit.position != UpdateMode.None)
                        state.pos = transform.position;
                    if (nit.rotation != UpdateMode.None)
                        state.rot = transform.rotation;
                }
                if (nit.scale != UpdateMode.None)
                    state.scale = transform.localScale;
            }

            public override bool HasChanged() {
                return state.pos != prevstate.pos || state.rot != prevstate.rot || state.scale != prevstate.scale;
            }

            public override void Serialize(PhotonStream stream, PhotonMessageInfo info) {
                if (stream.isReading) {
                    state.timestamp = info.timestamp;
                    reset(ref state);
                }
                if (nit.position != UpdateMode.None)
                    stream.Serialize(ref state.pos);
                if (nit.rotation != UpdateMode.None)
                    stream.Serialize(ref state.rot);
                if (nit.scale != UpdateMode.None)
                    stream.Serialize(ref state.scale);
            }

            protected override void updateStates(State lhs, State rhs, float t) {
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
            }
        }

        class RigidBodyInterpolator : ComponentInterpolator {
            private Rigidbody rbody;

            public RigidBodyInterpolator(NetworkObject nit, Rigidbody rbody) : base(nit) {
                this.rbody = rbody;
                reset(ref state);
            }

            private static void reset(ref State state) {
                state.v = Vector3.zero;
                state.angv = Vector3.zero;
            }

            public override void Obtain() {
                reset(ref state);
                if (nit.velocity != UpdateMode.None)
                    state.v = rbody.velocity;
                if (nit.angularVelocity != UpdateMode.None)
                    state.angv = rbody.angularVelocity;
            }

            public override bool HasChanged() {
                return state.v != prevstate.v || state.angv != prevstate.angv;
            }

            public override void Serialize(PhotonStream stream, PhotonMessageInfo info) {
                if (stream.isReading) {
                    state.timestamp = info.timestamp;
                    reset(ref state);
                }
                if (nit.velocity != UpdateMode.None)
                    stream.Serialize(ref state.v);
                if (nit.angularVelocity != UpdateMode.None)
                    stream.Serialize(ref state.angv);
            }

            protected override void updateStates(State lhs, State rhs, float t) {
                base.updateStates(lhs, rhs, t);

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