// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System.Linq;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using AlwaysIncludedShadersEditor = UnityEditor.GraphicsSettingsWindow.AlwaysIncludedShadersEditor;
using BuiltinShadersEditor = UnityEditor.GraphicsSettingsWindow.BuiltinShadersEditor;
using Object = UnityEngine.Object;
using ShaderPreloadEditor = UnityEditor.GraphicsSettingsWindow.ShaderPreloadEditor;
using ShaderStrippingEditor = UnityEditor.GraphicsSettingsWindow.ShaderStrippingEditor;
using TierSettingsEditor = UnityEditor.GraphicsSettingsWindow.TierSettingsEditor;
using VideoShadersEditor = UnityEditor.GraphicsSettingsWindow.VideoShadersEditor;

namespace UnityEditor
{
    [CustomEditor(typeof(UnityEngine.Rendering.GraphicsSettings))]
    internal class GraphicsSettingsInspector : ProjectSettingsBaseEditor
    {
        internal class Styles
        {
            public static readonly GUIContent showEditorWindow = EditorGUIUtility.TrTextContent("Open Editor...");
            public static readonly GUIContent closeEditorWindow = EditorGUIUtility.TrTextContent("Close Editor");
            public static readonly GUIContent tierSettings = EditorGUIUtility.TrTextContent("Tier Settings");
            public static readonly GUIContent builtinSettings = EditorGUIUtility.TrTextContent("Built-in Shader Settings");
            public static readonly GUIContent shaderStrippingSettings = EditorGUIUtility.TrTextContent("Shader Stripping");
            public static readonly GUIContent shaderPreloadSettings = EditorGUIUtility.TrTextContent("Shader Loading");
            public static readonly GUIContent logWhenShaderIsCompiled = EditorGUIUtility.TrTextContent("Log Shader Compilation", "When enabled, the player will print shader information each time a shader is being compiled (development and debug mode only).");
            public static readonly GUIContent cameraSettings = EditorGUIUtility.TrTextContent("Camera Settings");
            public static readonly GUIContent renderPipeSettings = EditorGUIUtility.TrTextContent("Scriptable Render Pipeline Settings", "This defines the default render pipeline, which Unity uses when there is no override for a given quality level.");
            public static readonly GUIContent renderPipeLabel = EditorGUIUtility.TrTextContent("Scriptable Render Pipeline");
        }

        Editor m_TierSettingsEditor;
        Editor m_BuiltinShadersEditor;
        Editor m_VideoShadersEditor;
        Editor m_AlwaysIncludedShadersEditor;
        Editor m_ShaderStrippingEditor;
        Editor m_ShaderPreloadEditor;
        SerializedProperty m_TransparencySortMode;
        SerializedProperty m_TransparencySortAxis;
        SerializedProperty m_ScriptableRenderLoop;
        SerializedProperty m_LogWhenShaderIsCompiled;

        Object graphicsSettings
        {
            get { return UnityEngine.Rendering.GraphicsSettings.GetGraphicsSettings(); }
        }

        Editor tierSettingsEditor
        {
            get
            {
                Editor.CreateCachedEditor(graphicsSettings, typeof(TierSettingsEditor), ref m_TierSettingsEditor);
                ((TierSettingsEditor)m_TierSettingsEditor).verticalLayout = true;
                return m_TierSettingsEditor;
            }
        }
        Editor builtinShadersEditor
        {
            get { Editor.CreateCachedEditor(graphicsSettings, typeof(BuiltinShadersEditor), ref m_BuiltinShadersEditor); return m_BuiltinShadersEditor; }
        }
        Editor videoShadersEditor
        {
            get { Editor.CreateCachedEditor(graphicsSettings, typeof(VideoShadersEditor), ref m_VideoShadersEditor); return m_VideoShadersEditor; }
        }
        Editor alwaysIncludedShadersEditor
        {
            get { Editor.CreateCachedEditor(graphicsSettings, typeof(AlwaysIncludedShadersEditor), ref m_AlwaysIncludedShadersEditor); return m_AlwaysIncludedShadersEditor; }
        }
        Editor shaderStrippingEditor
        {
            get { Editor.CreateCachedEditor(graphicsSettings, typeof(ShaderStrippingEditor), ref m_ShaderStrippingEditor); return m_ShaderStrippingEditor; }
        }
        Editor shaderPreloadEditor
        {
            get { Editor.CreateCachedEditor(graphicsSettings, typeof(ShaderPreloadEditor), ref m_ShaderPreloadEditor); return m_ShaderPreloadEditor; }
        }

        public void OnEnable()
        {
            m_TransparencySortMode = serializedObject.FindProperty("m_TransparencySortMode");
            m_TransparencySortAxis = serializedObject.FindProperty("m_TransparencySortAxis");
            m_ScriptableRenderLoop = serializedObject.FindProperty("m_CustomRenderPipeline");
            m_LogWhenShaderIsCompiled = serializedObject.FindProperty("m_LogWhenShaderIsCompiled");
            tierSettingsAnimator = new AnimatedValues.AnimBool(showTierSettingsUI, Repaint);
        }

        private void HandleEditorWindowButton()
        {
            TierSettingsWindow window = TierSettingsWindow.GetInstance();
            GUIContent text = window == null ? Styles.showEditorWindow : Styles.closeEditorWindow;
            if (GUILayout.Button(text, EditorStyles.miniButton, GUILayout.Width(110)))
            {
                if (window)
                {
                    window.Close();
                }
                else
                {
                    TierSettingsWindow.CreateWindow();
                    TierSettingsWindow.GetInstance().Show();
                }
            }
        }

        // this is category animation is blatantly copied from PlayerSettingsEditor.cs
        private bool showTierSettingsUI = true; // show by default, as otherwise users are confused
        private UnityEditor.AnimatedValues.AnimBool tierSettingsAnimator = null;

        private void TierSettingsGUI()
        {
            bool enabled = GUI.enabled;
            GUI.enabled = true; // we don't want to disable the expand behavior
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(20));

            EditorGUILayout.BeginHorizontal();
            Rect r = GUILayoutUtility.GetRect(20, 21); r.x += 3; r.width += 6;
            showTierSettingsUI = EditorGUI.FoldoutTitlebar(r, Styles.tierSettings, showTierSettingsUI, true, EditorStyles.inspectorTitlebarFlat, EditorStyles.inspectorTitlebarText);
            HandleEditorWindowButton();
            EditorGUILayout.EndHorizontal();

            tierSettingsAnimator.target = showTierSettingsUI;
            GUI.enabled = enabled;

            if (EditorGUILayout.BeginFadeGroup(tierSettingsAnimator.faded) && TierSettingsWindow.GetInstance() == null)
                tierSettingsEditor.OnInspectorGUI();
            EditorGUILayout.EndFadeGroup();
            EditorGUILayout.EndVertical();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUILayout.Label(Styles.renderPipeSettings, EditorStyles.boldLabel);
            RenderPipelineAssetSelector.Draw(serializedObject, m_ScriptableRenderLoop);
            EditorGUILayout.Space();

            bool usingSRP = GraphicsSettings.currentRenderPipeline != null;
            if (usingSRP)
                EditorGUILayout.HelpBox("A Scriptable Render Pipeline is in use, some settings will not be used and are hidden", MessageType.Info);

            if (!usingSRP)
            {
                GUILayout.Label(Styles.cameraSettings, EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(m_TransparencySortMode);
                EditorGUILayout.PropertyField(m_TransparencySortAxis);

                EditorGUILayout.Space();
            }

            float labelWidth = EditorGUIUtility.labelWidth;

            // Hide tier settings for SRPs and close tier settings window if open
            if (usingSRP)
            {
                TierSettingsWindow window = TierSettingsWindow.GetInstance();
                if (window != null)
                    window.Close();
            }
            else
            {
                TierSettingsGUI();
            }

            EditorGUIUtility.labelWidth = labelWidth;

            GUILayout.Label(Styles.builtinSettings, EditorStyles.boldLabel);
            if (!usingSRP)
                builtinShadersEditor.OnInspectorGUI();
            videoShadersEditor.OnInspectorGUI();
            alwaysIncludedShadersEditor.OnInspectorGUI();

            EditorGUILayout.Space();
            GUILayout.Label(Styles.shaderStrippingSettings, EditorStyles.boldLabel);
            shaderStrippingEditor.OnInspectorGUI();

            EditorGUILayout.Space();
            GUILayout.Label(Styles.shaderPreloadSettings, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_LogWhenShaderIsCompiled, Styles.logWhenShaderIsCompiled);
            shaderPreloadEditor.OnInspectorGUI();

            serializedObject.ApplyModifiedProperties();
        }

        public void SetSectionOpenListener(UnityAction action)
        {
            tierSettingsAnimator.valueChanged.RemoveAllListeners();
            tierSettingsAnimator.valueChanged.AddListener(action);
        }

        [SettingsProvider]
        static SettingsProvider CreateProjectSettingsProvider()
        {
            var provider = AssetSettingsProvider.CreateProviderFromAssetPath("Project/Graphics", "ProjectSettings/GraphicsSettings.asset");
            provider.keywords = SettingsProvider.GetSearchKeywordsFromGUIContentProperties<Styles>()
                .Concat(SettingsProvider.GetSearchKeywordsFromGUIContentProperties<TierSettingsEditor.Styles>())
                .Concat(SettingsProvider.GetSearchKeywordsFromGUIContentProperties<BuiltinShadersEditor.Styles>())
                .Concat(SettingsProvider.GetSearchKeywordsFromGUIContentProperties<ShaderStrippingEditor.Styles>())
                .Concat(SettingsProvider.GetSearchKeywordsFromGUIContentProperties<ShaderPreloadEditor.Styles>())
                .Concat(SettingsProvider.GetSearchKeywordsFromPath("ProjectSettings/GraphicsSettings.asset"));

            provider.activateHandler = (searchContext, rootElement) =>
            {
                (provider.settingsEditor as GraphicsSettingsInspector)?.SetSectionOpenListener(provider.Repaint);
            };

            provider.icon = EditorGUIUtility.FindTexture("UnityEngine/UI/GraphicRaycaster Icon");
            return provider;
        }
    }
}
