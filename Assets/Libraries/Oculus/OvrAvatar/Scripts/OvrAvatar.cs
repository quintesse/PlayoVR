using UnityEngine;
using System.Collections;
using System;
using System.Linq;
using Oculus.Avatar;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class AvatarLayer
{
    public int layerIndex;
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(AvatarLayer))]
public class AvatarLayerPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, GUIContent.none, property);
        SerializedProperty layerIndex = property.FindPropertyRelative("layerIndex");
        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
        layerIndex.intValue = EditorGUI.LayerField(position, layerIndex.intValue);
        EditorGUI.EndProperty();
    }
}
#endif

public class PacketRecordSettings
{
    public bool RecordingFrames = false;
    public float UpdateRate = 1.0f / 5.0f;  // Lower rate = bigger packets. Try to optimize against fidelity vs Network Overhead.
    public float AccumulatedTime = 0.0f;
};

public class OvrAvatar : MonoBehaviour {

    public OvrAvatarDriver Driver;
    public OvrAvatarBase Base;
    public OvrAvatarBody Body;
    public OvrAvatarTouchController ControllerLeft;
    public OvrAvatarTouchController ControllerRight;
    public OvrAvatarHand HandLeft;
    public OvrAvatarHand HandRight;
    public bool RecordPackets;
    public bool StartWithControllers;
    public AvatarLayer FirstPersonLayer;
    public AvatarLayer ThirdPersonLayer;
    public bool ShowFirstPerson = true;
    public bool ShowThirdPerson;
    public ovrAvatarCapabilities Capabilities = ovrAvatarCapabilities.All;
    public Shader SurfaceShader;
    public Shader SurfaceShaderSelfOccluding;
    public Shader SurfaceShaderPBS;

    int renderPartCount = 0;
    bool showLeftController;
    bool showRightController;
    List<float[]> voiceUpdates = new List<float[]>();

    public UInt64 oculusUserID;
    public bool CombineMeshes = false;

    private IntPtr sdkAvatar = IntPtr.Zero;
    private HashSet<UInt64> assetLoadingIds = new HashSet<UInt64>();
    private Dictionary<string, OvrAvatarComponent> trackedComponents =
        new Dictionary<string, OvrAvatarComponent>();

    public UnityEvent AssetsDoneLoading = new UnityEvent();
    bool assetsFinishedLoading = false;

    public Transform LeftHandCustomPose;
    public Transform RightHandCustomPose;
    Transform cachedLeftHandCustomPose;
    Transform[] cachedLeftHandJoints;
    ovrAvatarTransform[] cachedLeftHandTransforms;
    Transform cachedRightHandCustomPose;
    Transform[] cachedRightHandJoints;
    ovrAvatarTransform[] cachedRightHandTransforms;

    public class PacketEventArgs : EventArgs
    {
        public readonly OvrAvatarPacket Packet;
        public PacketEventArgs(OvrAvatarPacket packet)
        {
            Packet = packet;
        }
    }

    PacketRecordSettings PacketSettings = new PacketRecordSettings();

    void OnDestroy()
    {
        if( sdkAvatar != IntPtr.Zero)
        {
            CAPI.ovrAvatar_Destroy(sdkAvatar);
        }
    }

    public EventHandler<PacketEventArgs> PacketRecorded;

    public void AssetLoadedCallback(OvrAvatarAsset asset) {
        assetLoadingIds.Remove(asset.assetID);
    }

    private void AddAvatarComponent(GameObject componentObject, ovrAvatarComponent component) {
        OvrAvatarComponent ovrComponent = componentObject.AddComponent<OvrAvatarComponent>();
        trackedComponents.Add(component.name, ovrComponent);

        bool combine_meshes = CombineMeshes && componentObject.name == "body";
        for (UInt32 renderPartIndex = 0; renderPartIndex < component.renderPartCount; renderPartIndex++)
        {
            GameObject renderPartObject = new GameObject();
            renderPartObject.name = GetRenderPartName(component, renderPartIndex);
            renderPartObject.transform.SetParent(componentObject.transform);
            IntPtr renderPart = GetRenderPart(component, renderPartIndex);
            ovrAvatarRenderPartType type = CAPI.ovrAvatarRenderPart_GetType(renderPart);
            OvrAvatarRenderComponent ovrRenderPart;
            switch (type)
            {
                case ovrAvatarRenderPartType.SkinnedMeshRender:
                    ovrRenderPart = AddSkinnedMeshRenderComponent(renderPartObject, CAPI.ovrAvatarRenderPart_GetSkinnedMeshRender(renderPart));
                    break;
                case ovrAvatarRenderPartType.SkinnedMeshRenderPBS:
                    ovrRenderPart = AddSkinnedMeshRenderPBSComponent(renderPartObject, CAPI.ovrAvatarRenderPart_GetSkinnedMeshRenderPBS(renderPart));
                    break;
                case ovrAvatarRenderPartType.ProjectorRender:
                    combine_meshes = false;
                    ovrRenderPart = AddProjectorRenderComponent(renderPartObject, CAPI.ovrAvatarRenderPart_GetProjectorRender(renderPart));
                    break;
                default:
                    throw new NotImplementedException(
                        string.Format("Unsupported render part type: {0}",
                                      type.ToString()));
            }
            ovrComponent.RenderParts.Add(ovrRenderPart);
        }

        if (combine_meshes)
        {
            ovrComponent.StartMeshCombining(component);
        }
    }

    private OvrAvatarSkinnedMeshRenderComponent AddSkinnedMeshRenderComponent(GameObject gameObject, ovrAvatarRenderPart_SkinnedMeshRender skinnedMeshRender)
    {
        OvrAvatarSkinnedMeshRenderComponent skinnedMeshRenderer = gameObject.AddComponent<OvrAvatarSkinnedMeshRenderComponent>();
        skinnedMeshRenderer.Initialize(skinnedMeshRender, SurfaceShader, SurfaceShaderSelfOccluding, ThirdPersonLayer.layerIndex, FirstPersonLayer.layerIndex, renderPartCount++);
        return skinnedMeshRenderer;
    }

    private OvrAvatarSkinnedMeshRenderPBSComponent AddSkinnedMeshRenderPBSComponent(GameObject gameObject, ovrAvatarRenderPart_SkinnedMeshRenderPBS skinnedMeshRenderPBS)
    {
        OvrAvatarSkinnedMeshRenderPBSComponent skinnedMeshRenderer = gameObject.AddComponent<OvrAvatarSkinnedMeshRenderPBSComponent>();
        skinnedMeshRenderer.Initialize(skinnedMeshRenderPBS, SurfaceShaderPBS, ThirdPersonLayer.layerIndex, FirstPersonLayer.layerIndex, renderPartCount++);
        return skinnedMeshRenderer;
    }

    private OvrAvatarProjectorRenderComponent AddProjectorRenderComponent(GameObject gameObject, ovrAvatarRenderPart_ProjectorRender projectorRender)
    {
        ovrAvatarComponent component = CAPI.ovrAvatarComponent_Get(sdkAvatar, projectorRender.componentIndex);
        OvrAvatarComponent ovrComponent;
        if (trackedComponents.TryGetValue(component.name, out ovrComponent))
        {
            if (projectorRender.renderPartIndex < ovrComponent.RenderParts.Count)
            {
                OvrAvatarRenderComponent targetRenderPart = ovrComponent.RenderParts[(int)projectorRender.renderPartIndex];
                OvrAvatarProjectorRenderComponent projectorComponent = gameObject.AddComponent<OvrAvatarProjectorRenderComponent>();
                projectorComponent.InitializeProjectorRender(projectorRender, SurfaceShader, targetRenderPart);
                return projectorComponent;
            }
        }
        return null;
    }

    static public IntPtr GetRenderPart(ovrAvatarComponent component, UInt32 renderPartIndex)
    {
        long offset = Marshal.SizeOf(typeof(IntPtr)) * renderPartIndex;
        IntPtr marshalPtr = new IntPtr(component.renderParts.ToInt64() + offset);
        return (IntPtr)Marshal.PtrToStructure(marshalPtr, typeof(IntPtr));
    }

    private void UpdateAvatarComponent(ovrAvatarComponent component)
    {
        OvrAvatarComponent ovrComponent;
        if (!trackedComponents.TryGetValue(component.name, out ovrComponent))
        {
            throw new Exception(string.Format("trackedComponents didn't have {0}", component.name));
        }

        ovrComponent.UpdateAvatar(component, this);
    }

    private static string GetRenderPartName(ovrAvatarComponent component, uint renderPartIndex)
    {
        return component.name + "_renderPart_" + (int)renderPartIndex;
    }

    internal static void ConvertTransform(ovrAvatarTransform transform, Transform target)
    {
        Vector3 position = transform.position;
        position.z = -position.z;
        Quaternion orientation = transform.orientation;
        orientation.x = -orientation.x;
        orientation.y = -orientation.y;
        target.localPosition = position;
        target.localRotation = orientation;
        target.localScale = transform.scale;
    }

    public static ovrAvatarTransform CreateOvrAvatarTransform(Vector3 position, Quaternion orientation)
    {
        return new ovrAvatarTransform {
            position = new Vector3(position.x, position.y, -position.z),
            orientation = new Quaternion(-orientation.x, -orientation.y, orientation.z, orientation.w),
            scale = Vector3.one
        };
    }

    private void RemoveAvatarComponent(string name) {
        OvrAvatarComponent componentObject;
        trackedComponents.TryGetValue(name, out componentObject);
        Destroy(componentObject.gameObject);
        trackedComponents.Remove(name);
    }

    private void UpdateSDKAvatarUnityState()
    {
        //Iterate through all the render components
        UInt32 componentCount = CAPI.ovrAvatarComponent_Count(sdkAvatar);
        HashSet<string> componentsThisRun = new HashSet<string>();
        for (UInt32 i = 0; i < componentCount; i++) {
            IntPtr ptr = CAPI.ovrAvatarComponent_Get_Native(sdkAvatar, i);
            ovrAvatarComponent component = (ovrAvatarComponent)Marshal.PtrToStructure(ptr, typeof(ovrAvatarComponent));
            componentsThisRun.Add(component.name);
            if (!trackedComponents.ContainsKey(component.name))
            {
                GameObject componentObject = null;
                Type specificType = null;
                if ((Capabilities & ovrAvatarCapabilities.Base) != 0)
                {
                    ovrAvatarBaseComponent? baseComponent = CAPI.ovrAvatarPose_GetBaseComponent(sdkAvatar);
                    if (baseComponent.HasValue && ptr == baseComponent.Value.renderComponent)
                    {
                        specificType = typeof(OvrAvatarBase);
                        if (Base != null)
                        {
                            componentObject = Base.gameObject;
                        }
                    }
                }

                if (specificType == null && (Capabilities & ovrAvatarCapabilities.Body) != 0)
                {
                    ovrAvatarBodyComponent? bodyComponent = CAPI.ovrAvatarPose_GetBodyComponent(sdkAvatar);
                    if (bodyComponent.HasValue && ptr == bodyComponent.Value.renderComponent)
                    {
                        specificType = typeof(OvrAvatarBody);
                        if (Body != null)
                        {
                            componentObject = Body.gameObject;
                        }
                    }
                }

                if (specificType == null && (Capabilities & ovrAvatarCapabilities.Hands) != 0)
                {
                    ovrAvatarControllerComponent? controllerComponent = CAPI.ovrAvatarPose_GetLeftControllerComponent(sdkAvatar);
                    if (specificType == null && controllerComponent.HasValue && ptr == controllerComponent.Value.renderComponent)
                    {
                        specificType = typeof(OvrAvatarTouchController);
                        if (ControllerLeft != null)
                        {
                            componentObject = ControllerLeft.gameObject;
                        }
                    }

                    controllerComponent = CAPI.ovrAvatarPose_GetRightControllerComponent(sdkAvatar);
                    if (specificType == null && controllerComponent.HasValue && ptr == controllerComponent.Value.renderComponent)
                    {
                        specificType = typeof(OvrAvatarTouchController);
                        if (ControllerRight != null)
                        {
                            componentObject = ControllerRight.gameObject;
                        }
                    }

                    ovrAvatarHandComponent? handComponent = CAPI.ovrAvatarPose_GetLeftHandComponent(sdkAvatar);
                    if (specificType == null && handComponent.HasValue && ptr == handComponent.Value.renderComponent)
                    {
                        specificType = typeof(OvrAvatarHand);
                        if (HandLeft != null)
                        {
                            componentObject = HandLeft.gameObject;
                        }
                    }

                    handComponent = CAPI.ovrAvatarPose_GetRightHandComponent(sdkAvatar);
                    if (specificType == null && handComponent.HasValue && ptr == handComponent.Value.renderComponent)
                    {
                        specificType = typeof(OvrAvatarHand);
                        if (HandRight != null)
                        {
                            componentObject = HandRight.gameObject;
                        }
                    }
                }

                    // If this is an unknown type, just create an object for the rendering
                if (componentObject == null && specificType == null)
                {
                    componentObject = new GameObject();
                    componentObject.name = component.name;
                    componentObject.transform.SetParent(transform);
                }
                if (componentObject != null)
                {
                    AddAvatarComponent(componentObject, component); 
                }
            }
            UpdateAvatarComponent(component);
        }
        HashSet<string> deletableNames = new HashSet<string>(trackedComponents.Keys);
        deletableNames.ExceptWith(componentsThisRun);
        //deletableNames contains the name of all components which are tracked and were
        //not present in this run
        foreach (var name in deletableNames)
        {
            RemoveAvatarComponent(name);
        }
    }

    void UpdateCustomPoses()
    {
        // Check to see if the pose roots changed
        if (UpdatePoseRoot(LeftHandCustomPose, ref cachedLeftHandCustomPose, ref cachedLeftHandJoints, ref cachedLeftHandTransforms))
        {
            if (cachedLeftHandCustomPose == null && sdkAvatar != IntPtr.Zero)
            {
                CAPI.ovrAvatar_SetLeftHandGesture(sdkAvatar, ovrAvatarHandGesture.Default);
            }
        }
        if (UpdatePoseRoot(RightHandCustomPose, ref cachedRightHandCustomPose, ref cachedRightHandJoints, ref cachedRightHandTransforms))
        {
            if (cachedRightHandCustomPose == null && sdkAvatar != IntPtr.Zero)
            {
                CAPI.ovrAvatar_SetRightHandGesture(sdkAvatar, ovrAvatarHandGesture.Default);
            }
        }

        // Check to see if the custom gestures need to be updated
        if (sdkAvatar != IntPtr.Zero)
        {
            if (cachedLeftHandCustomPose != null && UpdateTransforms(cachedLeftHandJoints, cachedLeftHandTransforms))
            {
                CAPI.ovrAvatar_SetLeftHandCustomGesture(sdkAvatar, (uint)cachedLeftHandTransforms.Length, cachedLeftHandTransforms);
            }
            if (cachedRightHandCustomPose != null && UpdateTransforms(cachedRightHandJoints, cachedRightHandTransforms))
            {
                CAPI.ovrAvatar_SetRightHandCustomGesture(sdkAvatar, (uint)cachedRightHandTransforms.Length, cachedRightHandTransforms);
            }
        }
    }

    static bool UpdatePoseRoot(Transform poseRoot, ref Transform cachedPoseRoot, ref Transform[] cachedPoseJoints, ref ovrAvatarTransform[] transforms)
    {
        if (poseRoot == cachedPoseRoot)
        {
            return false;
        }

        if (!poseRoot)
        {
            cachedPoseRoot = null;
            cachedPoseJoints = null;
            transforms = null;
        }
        else
        {
            List<Transform> joints = new List<Transform>();
            OrderJoints(poseRoot, joints);
            cachedPoseRoot = poseRoot;
            cachedPoseJoints = joints.ToArray();
            transforms = new ovrAvatarTransform[joints.Count];
        }
        return true;
    }

    static bool UpdateTransforms(Transform[] joints, ovrAvatarTransform[] transforms)
    {
        bool updated = false;
        for (int i = 0; i < joints.Length; ++i)
        {
            Transform joint = joints[i];
            ovrAvatarTransform transform = CreateOvrAvatarTransform(joint.localPosition, joint.localRotation);
            if (transform.position != transforms[i].position || transform.orientation != transforms[i].orientation)
            {
                transforms[i] = transform;
                updated = true;
            }
        }
        return updated;
    }


    private static void OrderJoints(Transform transform, List<Transform> joints)
    {
        joints.Add(transform);
        for (int i = 0; i < transform.childCount; ++i)
        {
            Transform child = transform.GetChild(i);
            OrderJoints(child, joints);
        }
    }

    void AvatarSpecificationCallback(IntPtr avatarSpecification)
    {
#if UNITY_ANDROID
        Capabilities &= ~ovrAvatarCapabilities.BodyTilt;
#endif
        sdkAvatar = CAPI.ovrAvatar_Create(avatarSpecification, Capabilities);
        ShowLeftController(showLeftController);
        ShowRightController(showRightController);

        //Fetch all the assets that this avatar uses.
        UInt32 assetCount = CAPI.ovrAvatar_GetReferencedAssetCount(sdkAvatar);
        for (UInt32 i = 0; i < assetCount; ++i)
        {
            UInt64 id = CAPI.ovrAvatar_GetReferencedAsset(sdkAvatar, i);
            if (OvrAvatarSDKManager.Instance.GetAsset(id) == null)
            {
                OvrAvatarSDKManager.Instance.BeginLoadingAsset(id, this.AssetLoadedCallback);
                assetLoadingIds.Add(id);
            }
        }
    }

    void Start() {
        ShowLeftController(StartWithControllers);
        ShowRightController(StartWithControllers);
        OvrAvatarSDKManager.Instance.RequestAvatarSpecification(
            oculusUserID, this.AvatarSpecificationCallback);
    }

    void Update()
    {
        if (sdkAvatar == IntPtr.Zero)
        {
            return;
        }

        if (Driver != null)
        {
            Driver.UpdateTransforms(sdkAvatar);

            foreach (float[] voiceUpdate in voiceUpdates)
            {
                CAPI.ovrAvatarPose_UpdateVoiceVisualization(sdkAvatar, voiceUpdate);
            }

            voiceUpdates.Clear();

            CAPI.ovrAvatarPose_Finalize(sdkAvatar, Time.deltaTime);
        }

        if (RecordPackets)
        {
            RecordFrame();
        }

        if (assetLoadingIds.Count == 0)
        {
            UpdateSDKAvatarUnityState();
            UpdateCustomPoses();

            if (!assetsFinishedLoading)
            {
                AssetsDoneLoading.Invoke();
                assetsFinishedLoading = true;
            }
        }
    }

    public static ovrAvatarHandInputState CreateInputState(ovrAvatarTransform transform, OvrAvatarDriver.ControllerPose pose)
    {
        ovrAvatarHandInputState inputState = new ovrAvatarHandInputState();
        inputState.transform = transform;
        inputState.buttonMask = pose.buttons;
        inputState.touchMask = pose.touches;
        inputState.joystickX = pose.joystickPosition.x;
        inputState.joystickY = pose.joystickPosition.y;
        inputState.indexTrigger = pose.indexTrigger;
        inputState.handTrigger = pose.handTrigger;
        inputState.isActive = pose.isActive;
        return inputState;
    }

    public void ShowControllers(bool show)
    {
        ShowLeftController(show);
        ShowRightController(show);
    }

    public void ShowLeftController(bool show)
    {
        if (sdkAvatar != IntPtr.Zero)
        {
            CAPI.ovrAvatar_SetLeftControllerVisibility(sdkAvatar, show);
        }
        showLeftController = show;
    }

    public void ShowRightController(bool show)
    {
        if (sdkAvatar != IntPtr.Zero)
        {
            CAPI.ovrAvatar_SetRightControllerVisibility(sdkAvatar, show);
        }
        showRightController = show;
    }

    public void UpdateVoiceVisualization(float[] voiceSamples)
    {
        voiceUpdates.Add(voiceSamples);
    }

    void RecordFrame()
    {
        if (sdkAvatar == IntPtr.Zero)
        {
            return;
        }

        if (!PacketSettings.RecordingFrames)
        {
            CAPI.ovrAvatarPacket_BeginRecording(sdkAvatar);
            PacketSettings.AccumulatedTime = 0.0f;
            PacketSettings.RecordingFrames = true;
        }

        PacketSettings.AccumulatedTime += Time.deltaTime;

        if (PacketSettings.AccumulatedTime >= PacketSettings.UpdateRate)
        {
            PacketSettings.AccumulatedTime = 0.0f;
            var packet = CAPI.ovrAvatarPacket_EndRecording(sdkAvatar);
            CAPI.ovrAvatarPacket_BeginRecording(sdkAvatar);

            if (PacketRecorded != null)
            {
                PacketRecorded(this, new PacketEventArgs(new OvrAvatarPacket { ovrNativePacket = packet }));
            }

            CAPI.ovrAvatarPacket_Free(packet);
        }
    }
}
