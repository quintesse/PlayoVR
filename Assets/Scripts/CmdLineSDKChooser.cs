using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRTK;

public class CmdLineSDKChooser : SDKChooser {

    protected override void MakeSelection() {
        Func<VRTK_SDKInfo, string> selector = info => info.description.prettyName;
        var installedSDKInfoPrettyNames = VRTK_SDKManager.InstalledSystemSDKInfos
            .Select(selector)
            .Concat(VRTK_SDKManager.InstalledBoundariesSDKInfos.Select(selector))
            .Concat(VRTK_SDKManager.InstalledHeadsetSDKInfos.Select(selector))
            .Concat(VRTK_SDKManager.InstalledControllerSDKInfos.Select(selector))
            .Distinct()
            .ToList();

        bool selected = false;

        if (Utils.IsNoVr() || Utils.HasStartupOption("sdk")) {
            // Try to match an SDK against command line arguments
            string sdk = Utils.GetStartupOption("sdk");
            foreach (SDKSetup setup in Setups) {
                Debug.Log("1 : " + setup.PrettyName);
                if ((Utils.IsNoVr() && setup.PrettyName == "Simulator") || (setup.PrettyName == sdk)) {
                    Debug.Log("This");
                    SelectSDK(setup);
                    selected = true;
                    break;
                }
            }
        }

        if (!selected) {
            // Choose the first setup in our list that is actually available
            foreach (SDKSetup setup in Setups) {
                Debug.Log("2 : " + setup.PrettyName);
                if (installedSDKInfoPrettyNames.Contains(setup.PrettyName)) {
                    Debug.Log("This");
                    SelectSDK(setup);
                    selected = true;
                    break;
                }
            }
        }

        if (!selected) {
            Debug.LogError("No suitable SDK found!");
        }
    }

}
