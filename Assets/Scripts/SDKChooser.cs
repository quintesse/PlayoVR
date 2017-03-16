using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.VR;
using VRTK;

public abstract class SDKChooser : MonoBehaviour
{
    public VRTK_SDKManager SDKManager;
    public List<SDKSetup> Setups;

    public UnityEvent SDKWasChosen = new UnityEvent();

    protected abstract void MakeSelection();

    private void OnEnable()
    {
        MakeSelection();
    }

    protected void SelectSDK(SDKSetup setup)
    {
        UnityAction<Scene, LoadSceneMode> onSceneLoaded = null;
        onSceneLoaded = (scene, loadMode) =>
        {
            if (scene.name != setup.SceneToLoad)
            {
                return;
            }

            SceneManager.sceneLoaded -= onSceneLoaded;

            SDKManager.PopulateObjectReferences(true);
            SDKManager.gameObject.SetActive(true);

            Destroy(gameObject);

            SDKWasChosen.Invoke();
        };
        SceneManager.sceneLoaded += onSceneLoaded;

        SDKManager.autoPopulateObjectReferences = false;

        Func<VRTK_SDKInfo, bool> predicate = info => info.description.prettyName == setup.PrettyName;
        SDKManager.systemSDKInfo = VRTK_SDKManager.InstalledSystemSDKInfos.First(predicate);
        SDKManager.boundariesSDKInfo = VRTK_SDKManager.InstalledBoundariesSDKInfos.First(predicate);
        SDKManager.headsetSDKInfo = VRTK_SDKManager.InstalledHeadsetSDKInfos.First(predicate);
        SDKManager.controllerSDKInfo = VRTK_SDKManager.InstalledControllerSDKInfos.First(predicate);

        VRSettings.LoadDeviceByName(setup.VRDeviceNameToLoad);
        StartCoroutine(LoadSceneAfterFrameDelay(setup));

        Debug.Log("Using SDK '" + setup.PrettyName + "'");
    }

    private static IEnumerator LoadSceneAfterFrameDelay(SDKSetup setup)
    {
        yield return null;

        SceneManager.LoadScene(
            setup.SceneToLoad,
            LoadSceneMode.Additive
        );
    }

    [Serializable]
    public sealed class SDKSetup
    {
        public string PrettyName;
        public string SceneToLoad;
        public string VRDeviceNameToLoad;
    }
}
