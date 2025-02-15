// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Modules;
using UnityEditor.Build;
using UnityEngine;

internal abstract class DesktopStandaloneBuildWindowExtension : DefaultBuildWindowExtension
{
    private GUIContent m_StandaloneTarget = EditorGUIUtility.TrTextContent("Target Platform", "Destination platform for standalone build");
    private GUIContent m_Architecture = EditorGUIUtility.TrTextContent("Architecture", "Build m_Architecture for standalone");
    private BuildTarget[] m_StandaloneSubtargets;
    private GUIContent[] m_StandaloneSubtargetStrings;

    protected bool m_HasMonoPlayers;
    protected bool m_HasIl2CppPlayers;
    protected bool m_HasServerPlayers;
    protected bool m_IsRunningOnHostPlatform;

    public DesktopStandaloneBuildWindowExtension(bool hasMonoPlayers, bool hasIl2CppPlayers, bool hasServerPlayers)
    {
        SetupStandaloneSubtargets();

        m_IsRunningOnHostPlatform = Application.platform == GetHostPlatform();
        m_HasIl2CppPlayers = hasIl2CppPlayers;
        m_HasMonoPlayers = hasMonoPlayers;
        m_HasServerPlayers = hasServerPlayers;
    }

    private void SetupStandaloneSubtargets()
    {
        List<BuildTarget> standaloneSubtargetsList = new List<BuildTarget>();
        List<GUIContent> standaloneSubtargetStringsList = new List<GUIContent>();

        if (ModuleManager.IsPlatformSupportLoadedByBuildTarget(BuildTarget.StandaloneWindows))
        {
            standaloneSubtargetsList.Add(BuildTarget.StandaloneWindows);
            standaloneSubtargetStringsList.Add(EditorGUIUtility.TrTextContent("Windows"));
        }
        if (ModuleManager.IsPlatformSupportLoadedByBuildTarget(BuildTarget.StandaloneOSX))
        {
            standaloneSubtargetsList.Add(BuildTarget.StandaloneOSX);
            standaloneSubtargetStringsList.Add(EditorGUIUtility.TrTextContent("macOS"));
        }
        if (ModuleManager.IsPlatformSupportLoadedByBuildTarget(BuildTarget.StandaloneLinux64))
        {
            standaloneSubtargetsList.Add(BuildTarget.StandaloneLinux64);
            standaloneSubtargetStringsList.Add(EditorGUIUtility.TrTextContent("Linux"));
        }

        m_StandaloneSubtargets = standaloneSubtargetsList.ToArray();
        m_StandaloneSubtargetStrings = standaloneSubtargetStringsList.ToArray();
    }

    internal static BuildTarget GetBestStandaloneTarget(BuildTarget selectedTarget)
    {
        if (ModuleManager.IsPlatformSupportLoadedByBuildTarget(selectedTarget))
            return selectedTarget;
        if (RuntimePlatform.WindowsEditor == Application.platform && ModuleManager.IsPlatformSupportLoadedByBuildTarget(BuildTarget.StandaloneWindows))
            return BuildTarget.StandaloneWindows64;
        if (RuntimePlatform.OSXEditor == Application.platform && ModuleManager.IsPlatformSupportLoadedByBuildTarget(BuildTarget.StandaloneOSX))
            return BuildTarget.StandaloneOSX;
        if (ModuleManager.IsPlatformSupportLoadedByBuildTarget(BuildTarget.StandaloneOSX))
            return BuildTarget.StandaloneOSX;
        if (ModuleManager.IsPlatformSupportLoadedByBuildTarget(BuildTarget.StandaloneLinux64))
            return BuildTarget.StandaloneLinux64;
        return BuildTarget.StandaloneWindows64;
    }

    private static Dictionary<GUIContent, BuildTarget> GetArchitecturesForPlatform(BuildTarget target)
    {
        switch (target)
        {
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                return new Dictionary<GUIContent, BuildTarget>
                {
                    { EditorGUIUtility.TrTextContent("Intel 64-bit"), BuildTarget.StandaloneWindows64 },
                    { EditorGUIUtility.TrTextContent("Intel 32-bit"), BuildTarget.StandaloneWindows },
                };
            default:
                return null;
        }
    }

    private static BuildTarget DefaultTargetForPlatform(BuildTarget target)
    {
        switch (target)
        {
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                return BuildTarget.StandaloneWindows64;
                // Deprecated
#pragma warning disable 612, 618
            case BuildTarget.StandaloneLinux:
            case BuildTarget.StandaloneLinuxUniversal:
#pragma warning restore 612, 618
            case BuildTarget.StandaloneLinux64:
                return BuildTarget.StandaloneLinux64;
            case BuildTarget.StandaloneOSX:
                // Deprecated
#pragma warning disable 612, 618
            case BuildTarget.StandaloneOSXIntel:
            case BuildTarget.StandaloneOSXIntel64:
#pragma warning restore 612, 618
                return BuildTarget.StandaloneOSX;
            default:
                return target;
        }
    }

    private static BuildTarget DefaultArchitectureForTarget(BuildTarget target)
    {
        switch (target)
        {
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                return BuildTarget.StandaloneWindows64;
                // Deprecated
#pragma warning disable 612, 618
            case BuildTarget.StandaloneLinux:
            case BuildTarget.StandaloneLinuxUniversal:
#pragma warning restore 612, 618
            case BuildTarget.StandaloneLinux64:
                return BuildTarget.StandaloneLinux64;
            case BuildTarget.StandaloneOSX:
                // Deprecated
#pragma warning disable 612, 618
            case BuildTarget.StandaloneOSXIntel:
            case BuildTarget.StandaloneOSXIntel64:
#pragma warning restore 612, 618
                return BuildTarget.StandaloneOSX;
            default:
                return target;
        }
    }

    protected virtual void ShowArchitectureSpecificOptions() {}

    public override void ShowPlatformBuildOptions()
    {
        BuildTarget selectedTarget = GetBestStandaloneTarget(EditorUserBuildSettings.selectedStandaloneTarget);
        BuildTarget newTarget = EditorUserBuildSettings.selectedStandaloneTarget;

        int selectedIndex = Math.Max(0, Array.IndexOf(m_StandaloneSubtargets, DefaultTargetForPlatform(selectedTarget)));
        int newIndex = EditorGUILayout.Popup(m_StandaloneTarget, selectedIndex, m_StandaloneSubtargetStrings);

        if (newIndex == selectedIndex)
        {
            Dictionary<GUIContent, BuildTarget> architectures = GetArchitecturesForPlatform(selectedTarget);
            if (null != architectures)
            {
                // Display architectures for the current target platform
                GUIContent[] architectureNames = new List<GUIContent>(architectures.Keys).ToArray();
                int selectedArchitecture = 0;

                // Grab m_Architecture index for currently selected target
                foreach (var architecture in architectures)
                {
                    if (architecture.Value == selectedTarget)
                    {
                        selectedArchitecture = System.Math.Max(0, System.Array.IndexOf(architectureNames, architecture.Key));
                        break;
                    }
                }

                selectedArchitecture = EditorGUILayout.Popup(m_Architecture, selectedArchitecture, architectureNames);
                newTarget = architectures[architectureNames[selectedArchitecture]];
            }
        }
        else
        {
            newTarget = DefaultArchitectureForTarget(m_StandaloneSubtargets[newIndex]);
        }

        if (newTarget != EditorUserBuildSettings.selectedStandaloneTarget)
        {
            // setting selectedStandaloneTarget has side-effect: stops playmode
            EditorUserBuildSettings.selectedStandaloneTarget = newTarget;
            GUIUtility.ExitGUI();
        }

        ShowArchitectureSpecificOptions();

        ShowBackendErrorIfNeeded();
    }

    protected void ShowBackendErrorIfNeeded()
    {
        var error = GetCannotBuildPlayerInCurrentSetupError();
        if (string.IsNullOrEmpty(error))
            return;

        EditorGUILayout.HelpBox(error, MessageType.Error);
    }

    public override bool EnabledBuildButton()
    {
        return string.IsNullOrEmpty(GetCannotBuildPlayerInCurrentSetupError());
    }

    protected virtual string GetCannotBuildPlayerInCurrentSetupError()
    {
        var namedBuildTarget = EditorUserBuildSettingsUtils.CalculateSelectedNamedBuildTarget();

        if (namedBuildTarget == NamedBuildTarget.Server)
        {
            if(!m_HasServerPlayers)
                return $"Dedicated Server support for {GetHostPlatformName()} is not installed";

            if (PlayerSettings.GetScriptingBackend(namedBuildTarget) == ScriptingImplementation.IL2CPP && !m_IsRunningOnHostPlatform)
                return string.Format("{0} IL2CPP player can only be built on {0}.", GetHostPlatformName());

            return null;
        }

        if (PlayerSettings.GetScriptingBackend(namedBuildTarget) != ScriptingImplementation.IL2CPP)
        {
            if (!m_HasMonoPlayers)
                return "Currently selected scripting backend (Mono) is not installed.";
        }
        else
        {
            if (!m_IsRunningOnHostPlatform)
                return string.Format("{0} IL2CPP player can only be built on {0}.", GetHostPlatformName());

            if (!m_HasIl2CppPlayers)
                return "Currently selected scripting backend (IL2CPP) is not installed."; // Note: error should match UWP player error message for consistency.
        }

        return null;
    }

    protected abstract RuntimePlatform GetHostPlatform();
    protected abstract string GetHostPlatformName();

    public override bool EnabledBuildAndRunButton()
    {
        return true;
    }

    public override bool ShouldDrawWaitForManagedDebugger()
    {
        return true;
    }
}
