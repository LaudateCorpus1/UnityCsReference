// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using UnityEngine;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor.Rendering;
using Object = UnityEngine.Object;
using UnityEngine.Rendering;

namespace UnityEditor
{
    [CustomEditor(typeof(Shader))]
    internal class ShaderInspector : Editor
    {
        private static readonly string[] kPropertyTypes =
        {
            "Color: ",
            "Vector: ",
            "Float: ",
            "Range: ",
            "Texture: "
        };
        private static readonly string[] kTextureTypes =
        {
            "No Texture?: ",
            "Any texture: ",
            "2D: ",
            "3D: ",
            "Cube: ",
            "2DArray: ",
            "CubeArray: "
        };

        private const float kSpace = 5f;

        const float kValueFieldWidth = 200.0f;
        const float kArrayValuePopupBtnWidth = 25.0f;

        private static bool s_KeywordsUnfolded = false;
        private static bool s_PropertiesUnfolded = true;

        internal class Styles
        {
            public static Texture2D errorIcon = EditorGUIUtility.LoadIcon("console.erroricon.sml");
            public static Texture2D warningIcon = EditorGUIUtility.LoadIcon("console.warnicon.sml");

            public static GUIContent togglePreprocess = EditorGUIUtility.TrTextContent("Preprocess only", "Show preprocessor output instead of compiled shader code");
            public static GUIContent toggleStripLineDirective = EditorGUIUtility.TrTextContent("Strip #line directives", "Strip #line directives from preprocessor output");
            public static GUIContent showSurface = EditorGUIUtility.TrTextContent("Show generated code", "Show generated code of a surface shader");
            public static GUIContent showFF = EditorGUIUtility.TrTextContent("Show generated code", "Show generated code of a fixed function shader");
            public static GUIContent showCurrent = EditorGUIUtility.TrTextContent("Compile and show code \u007C \u25BE");  // vertical bar & dropdow arrow - due to lacking editor style of "mini button with a dropdown"
            public static GUIContent overridableKeywords = EditorGUIUtility.TrTextContent("Overridable", "Shader keywords overridable by global shader keyword state");
            public static GUIContent notOverridableKeywords = EditorGUIUtility.TrTextContent("Not overridable", "Shader keywords not overridable by global shader keyword state");

            public static GUIStyle messageStyle = "CN StatusInfo";
            public static GUIStyle evenBackground = "CN EntryBackEven";

            public static GUIContent no = EditorGUIUtility.TrTextContent("no");
            public static GUIContent builtinShader = EditorGUIUtility.TrTextContent("Built-in shader");

            public static readonly GUIContent arrayValuePopupButton = EditorGUIUtility.TrTextContent("...");
        }
        static readonly int kErrorViewHash = "ShaderErrorView".GetHashCode();

        private static bool s_PreprocessOnly = false;
        private static bool s_StripLineDirectives = true;

        Vector2 m_ScrollPosition = Vector2.zero;
        private Material m_SrpCompatibilityCheckMaterial = null;

        public Material srpCompatibilityCheckMaterial
        {
            get
            {
                if (m_SrpCompatibilityCheckMaterial == null)
                {
                    m_SrpCompatibilityCheckMaterial = new Material(target as Shader);
                }
                return m_SrpCompatibilityCheckMaterial;
            }
        }

        public virtual void OnEnable()
        {
            var s = target as Shader;
            if (s != null)
                ShaderUtil.FetchCachedMessages(s);
        }

        public virtual void OnDisable()
        {
            if (m_SrpCompatibilityCheckMaterial != null)
            {
                GameObject.DestroyImmediate(m_SrpCompatibilityCheckMaterial);
            }
        }

        private static string GetPropertyType(Shader s, int index)
        {
            var type = s.GetPropertyType(index);
            if (type == ShaderPropertyType.Texture)
            {
                return kTextureTypes[(int)s.GetPropertyTextureDimension(index)];
            }
            return kPropertyTypes[(int)type];
        }

        public override void OnInspectorGUI()
        {
            var s = target as Shader;
            if (s == null)
                return;

            GUI.enabled = true;

            EditorGUI.indentLevel = 0;

            ShowShaderCodeArea(s);

            if (s.isSupported)
            {
                EditorGUILayout.LabelField("Cast shadows", (ShaderUtil.HasShadowCasterPass(s)) ? "yes" : "no");
                EditorGUILayout.LabelField("Render queue", ShaderUtil.GetRenderQueue(s).ToString(CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("LOD", ShaderUtil.GetLOD(s).ToString(CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Ignore projector", ShaderUtil.DoesIgnoreProjector(s) ? "yes" : "no");
                string disableBatchingString;
                switch (s.disableBatching)
                {
                    case DisableBatchingType.False:
                        disableBatchingString = "no";
                        break;
                    case DisableBatchingType.True:
                        disableBatchingString = "yes";
                        break;
                    case DisableBatchingType.WhenLODFading:
                        disableBatchingString = "when LOD fading is on";
                        break;
                    default:
                        disableBatchingString = "unknown";
                        break;
                }
                EditorGUILayout.LabelField("Disable batching", disableBatchingString);
                ShowKeywords(s);

                // If any SRP is active, then display the SRP Batcher compatibility status
                if (RenderPipelineManager.currentPipeline != null)
                {
                    // NOTE: Force the shader compilation to ensure GetSRPBatcherCompatibilityCode will be up to date
                    srpCompatibilityCheckMaterial.SetPass(0);
                    int subShader = ShaderUtil.GetShaderActiveSubshaderIndex(s);
                    int SRPErrCode = ShaderUtil.GetSRPBatcherCompatibilityCode(s, subShader);
                    string result = (0 == SRPErrCode) ? "compatible" : "not compatible";
                    EditorGUILayout.LabelField("SRP Batcher", result);
                    if (SRPErrCode != 0)
                    {
                        EditorGUILayout.HelpBox(ShaderUtil.GetSRPBatcherCompatibilityIssueReason(s, subShader, SRPErrCode), MessageType.Info);
                    }
                }

                ShowShaderProperties(s);
            }
        }

        private void ShowKeywords(Shader s)
        {
            EditorGUILayout.BeginVertical();
            s_KeywordsUnfolded = EditorGUILayout.Foldout(s_KeywordsUnfolded, "Keywords");
            if (s_KeywordsUnfolded)
            {
                var keywords = s.keywordSpace.keywords;
                var overridable = new List<LocalKeyword>();
                var nonOverridable = new List<LocalKeyword>();

                foreach (var k in keywords)
                {
                    if (k.isOverridable)
                        overridable.Add(k);
                    else
                        nonOverridable.Add(k);
                }

                overridable.Sort((x, y) => string.CompareOrdinal(x.name, y.name));
                nonOverridable.Sort((x, y) => string.CompareOrdinal(x.name, y.name));

                EditorGUILayout.LabelField(Styles.overridableKeywords, EditorStyles.boldLabel);
                foreach (var k in overridable)
                    EditorGUILayout.LabelField(k.name);

                EditorGUILayout.LabelField(Styles.notOverridableKeywords, EditorStyles.boldLabel);
                foreach (var k in nonOverridable)
                    EditorGUILayout.LabelField(k.name);
            }

            EditorGUILayout.EndVertical();
        }

        private void ShowShaderCodeArea(Shader s)
        {
            ShowSurfaceShaderButton(s);
            ShowFixedFunctionShaderButton(s);
            ShowCompiledCodeButton(s);
            ShowShaderErrors(s);
        }

        private static void ShowShaderProperties(Shader s)
        {
            GUILayout.Space(kSpace);
            s_PropertiesUnfolded = EditorGUILayout.Foldout(s_PropertiesUnfolded, "Properties");
            if (s_PropertiesUnfolded)
            {
                int n = s.GetPropertyCount();
                for (int i = 0; i < n; ++i)
                {
                    string pname = s.GetPropertyName(i);
                    string pdesc = s.GetPropertyDescription(i) + " (" + s.GetPropertyType(i) + ")";
                    EditorGUILayout.LabelField(pname, pdesc);
                }
            }
        }

        // shared by compute shader inspector too
        internal static void ShaderErrorListUI(Object shader, ShaderMessage[] messages, ref Vector2 scrollPosition)
        {
            int n = messages.Length;

            GUILayout.Space(kSpace);
            GUILayout.Label(string.Format("Errors ({0}):", n), EditorStyles.boldLabel);
            int errorListID = GUIUtility.GetControlID(kErrorViewHash, FocusType.Passive);
            float height = Mathf.Min(n * 20f + 40f, 150f);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUISkin.current.box, GUILayout.MinHeight(height));

            EditorGUIUtility.SetIconSize(new Vector2(16.0f, 16.0f));
            float lineHeight = Styles.messageStyle.CalcHeight(EditorGUIUtility.TempContent(Styles.errorIcon), 100);

            Event e = Event.current;

            for (int i = 0; i < n; ++i)
            {
                Rect r = EditorGUILayout.GetControlRect(false, lineHeight);

                string err = messages[i].message;
                string plat = messages[i].platform.ToString();
                bool warn = messages[i].severity != ShaderCompilerMessageSeverity.Error;
                string fileName = FileUtil.GetLastPathNameComponent(messages[i].file);
                int line = messages[i].line;

                // Double click opens shader file at error line
                if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
                {
                    GUIUtility.keyboardControl = errorListID;
                    if (e.clickCount == 2)
                    {
                        string filePath = messages[i].file;
                        Object asset = string.IsNullOrEmpty(filePath) ? null : AssetDatabase.LoadMainAssetAtPath(filePath);

                        // if we don't have an asset and the filePath is an absolute path, it's an error in a system
                        // cginc - open that instead
                        if (asset == null && System.IO.Path.IsPathRooted(filePath))
                            ShaderUtil.OpenSystemShaderIncludeError(filePath, line);
                        else
                            AssetDatabase.OpenAsset(asset ?? shader, line);
                        GUIUtility.ExitGUI();
                    }
                    e.Use();
                }

                // Context menu, "Copy"
                if (e.type == EventType.ContextClick && r.Contains(e.mousePosition))
                {
                    e.Use();
                    var menu = new GenericMenu();
                    // need to copy current value to be used in delegate
                    // (C# closures close over variables, not their values)
                    var errorIndex = i;
                    menu.AddItem(EditorGUIUtility.TrTextContent("Copy error text"), false, delegate {
                        string errMsg = messages[errorIndex].message;
                        if (!string.IsNullOrEmpty(messages[errorIndex].messageDetails))
                        {
                            errMsg += '\n';
                            errMsg += messages[errorIndex].messageDetails;
                        }
                        EditorGUIUtility.systemCopyBuffer = errMsg;
                    });
                    menu.ShowAsContext();
                }

                // background
                if (e.type == EventType.Repaint)
                {
                    if ((i & 1) == 0)
                    {
                        GUIStyle st = Styles.evenBackground;
                        st.Draw(r, false, false, false, false);
                    }
                }

                // error location on the right side
                Rect locRect = r;
                locRect.xMin = locRect.xMax;
                if (line > 0)
                {
                    GUIContent gc;
                    if (string.IsNullOrEmpty(fileName))
                        gc = EditorGUIUtility.TempContent(line.ToString(CultureInfo.InvariantCulture));
                    else
                        gc = EditorGUIUtility.TempContent(fileName + ":" + line.ToString(CultureInfo.InvariantCulture));

                    // calculate size so we can right-align it
                    Vector2 size = EditorStyles.miniLabel.CalcSize(gc);
                    locRect.xMin -= size.x;
                    GUI.Label(locRect, gc, EditorStyles.miniLabel);
                    locRect.xMin -= 2;
                    // ensure some minimum width so that platform field next will line up
                    if (locRect.width < 30)
                        locRect.xMin = locRect.xMax - 30;
                }

                // platform to the left of it
                Rect platRect = locRect;
                platRect.width = 0;
                if (plat.Length > 0)
                {
                    GUIContent gc = EditorGUIUtility.TempContent(plat);
                    // calculate size so we can right-align it
                    Vector2 size = EditorStyles.miniLabel.CalcSize(gc);
                    platRect.xMin -= size.x;

                    // draw platform in dimmer color; it's often not very important information
                    Color oldColor = GUI.contentColor;
                    GUI.contentColor = new Color(1, 1, 1, 0.5f);
                    GUI.Label(platRect, gc, EditorStyles.miniLabel);
                    GUI.contentColor = oldColor;
                    platRect.xMin -= 2;
                }

                // error message
                Rect msgRect = r;
                msgRect.xMax = platRect.xMin;
                GUI.Label(msgRect, EditorGUIUtility.TempContent(err, warn ? Styles.warningIcon : Styles.errorIcon), Styles.messageStyle);
            }
            EditorGUIUtility.SetIconSize(Vector2.zero);
            GUILayout.EndScrollView();
        }

        ShaderMessage[] m_ShaderMessages;
        private void ShowShaderErrors(Shader s)
        {
            if (Event.current.type == EventType.Layout)
            {
                int n = ShaderUtil.GetShaderMessageCount(s);
                m_ShaderMessages = null;
                if (n >= 1)
                {
                    m_ShaderMessages = ShaderUtil.GetShaderMessages(s);
                }
            }

            if (m_ShaderMessages == null)
                return;
            ShaderErrorListUI(s, m_ShaderMessages, ref m_ScrollPosition);
        }

        // Compiled shader code button+dropdown
        private void ShowCompiledCodeButton(Shader s)
        {
            EditorGUILayout.BeginVertical();
            ShaderImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(s.GetInstanceID())) as ShaderImporter;

            s_PreprocessOnly = EditorGUILayout.Toggle(Styles.togglePreprocess, s_PreprocessOnly);
            if (s_PreprocessOnly)
                s_StripLineDirectives = EditorGUILayout.Toggle(Styles.toggleStripLineDirective, s_StripLineDirectives);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Compiled code", EditorStyles.miniButton);

            var hasCode = ShaderUtil.HasShaderSnippets(s) || ShaderUtil.HasSurfaceShaders(s) || ShaderUtil.HasFixedFunctionShaders(s);
            if (hasCode)
            {
                // button with a drop-down part on the right
                var modeContent = Styles.showCurrent;
                var modeRect = GUILayoutUtility.GetRect(modeContent, EditorStyles.miniButton, GUILayout.ExpandWidth(false));
                var modeDropRect = new Rect(modeRect.xMax - 16, modeRect.y, 16, modeRect.height);
                if (EditorGUI.DropdownButton(modeDropRect, GUIContent.none, FocusType.Passive, GUIStyle.none))
                {
                    Rect rect = GUILayoutUtility.topLevel.GetLast();
                    PopupWindow.Show(rect, new ShaderInspectorPlatformsPopup(s));
                    GUIUtility.ExitGUI();
                }
                if (GUI.Button(modeRect, modeContent, EditorStyles.miniButton))
                {
                    ShaderUtil.OpenCompiledShader(s, ShaderInspectorPlatformsPopup.currentMode, ShaderInspectorPlatformsPopup.currentPlatformMask, ShaderInspectorPlatformsPopup.currentVariantStripping == 0, s_PreprocessOnly, s_StripLineDirectives);
                    GUIUtility.ExitGUI();
                }
            }
            else
            {
                // Note: PrefixLabel is sometimes buggy if followed by a non-control (like Label).
                // We just want to show a label here, but have to pretend it's a button so it is treated like
                // a control.
                GUILayout.Button("none (precompiled shader)", GUI.skin.label);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // "show surface shader" button
        private static void ShowSurfaceShaderButton(Shader s)
        {
            var hasSurface = ShaderUtil.HasSurfaceShaders(s);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Surface shader", EditorStyles.miniButton);
            if (hasSurface)
            {
                // check if this is a built-in shader (has no importer);
                // we can't show generated code in that case
                var builtinShader = (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(s)) == null);
                if (!builtinShader)
                {
                    if (GUILayout.Button(Styles.showSurface, EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                    {
                        ShaderUtil.OpenParsedSurfaceShader(s);
                        GUIUtility.ExitGUI();
                    }
                }
                else
                {
                    // See comment below why this is a button.
                    GUILayout.Button(Styles.builtinShader, GUI.skin.label);
                }
            }
            else
            {
                // Note: PrefixLabel is sometimes buggy if followed by a non-control (like Label).
                // We just want to show a label here, but have to pretend it's a button so it is treated like
                // a control.
                GUILayout.Button(Styles.no, GUI.skin.label);
            }
            EditorGUILayout.EndHorizontal();
        }

        // "show fixed function shader" button
        private static void ShowFixedFunctionShaderButton(Shader s)
        {
            var hasFF = ShaderUtil.HasFixedFunctionShaders(s);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Fixed function", EditorStyles.miniButton);
            if (hasFF)
            {
                // check if this is a built-in shader (has no importer);
                // we can't show generated code in that case
                var builtinShader = (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(s)) == null);
                if (!builtinShader)
                {
                    if (GUILayout.Button(Styles.showFF, EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                    {
                        ShaderUtil.OpenGeneratedFixedFunctionShader(s);
                        GUIUtility.ExitGUI();
                    }
                }
                else
                {
                    // See comment below why this is a button.
                    GUILayout.Button(Styles.builtinShader, GUI.skin.label);
                }
            }
            else
            {
                // Note: PrefixLabel is sometimes buggy if followed by a non-control (like Label).
                // We just want to show a label here, but have to pretend it's a button so it is treated like
                // a control.
                GUILayout.Button(Styles.no, GUI.skin.label);
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    // Popup window to select which platforms to compile a shader for.
    internal class ShaderInspectorPlatformsPopup : PopupWindowContent
    {
        private class Styles
        {
            static public readonly GUIStyle menuItem = "MenuItem";
            static public readonly GUIStyle separator = "sv_iconselector_sep";
        }

        static internal readonly string[] s_PlatformModes =
        {
            "Current graphics device",
            "Current build platform",
            "All platforms",
            "Custom:"
        };

        private static string[] s_ShaderPlatformNames;
        private static int[] s_ShaderPlatformIndices;

        const float kFrameWidth = 1f;
        const float kSeparatorHeight = 6;

        private readonly Shader m_Shader;

        private ulong totalVariants;
        private ulong variantsWithUsage;


        public static int currentMode
        {
            get
            {
                if (s_CurrentMode < 0)
                    s_CurrentMode = EditorPrefs.GetInt("ShaderInspectorPlatformMode", 1);
                return s_CurrentMode;
            }
            set
            {
                s_CurrentMode = value;
                EditorPrefs.SetInt("ShaderInspectorPlatformMode", value);
            }
        }
        static int s_CurrentMode = -1;

        public static int currentPlatformMask
        {
            get
            {
                if (s_CurrentPlatformMask < 0)
                {
                    int defaultMask = (1 << Enum.GetNames(typeof(Rendering.ShaderCompilerPlatform)).Length - 1);
                    s_CurrentPlatformMask = EditorPrefs.GetInt("ShaderInspectorPlatformMask", defaultMask);
                }
                return s_CurrentPlatformMask;
            }
            set
            {
                s_CurrentPlatformMask = value;
                EditorPrefs.SetInt("ShaderInspectorPlatformMask", value);
            }
        }
        static int s_CurrentPlatformMask = -1;

        public static int currentVariantStripping
        {
            get
            {
                if (s_CurrentVariantStripping < 0)
                    s_CurrentVariantStripping = EditorPrefs.GetInt("ShaderInspectorVariantStripping", 1);
                return s_CurrentVariantStripping;
            }
            set
            {
                s_CurrentVariantStripping = value;
                EditorPrefs.SetInt("ShaderInspectorVariantStripping", value);
            }
        }
        static int s_CurrentVariantStripping = -1;


        public ShaderInspectorPlatformsPopup(Shader shader)
        {
            m_Shader = shader;
            InitializeShaderPlatforms();
            totalVariants = 0;
            variantsWithUsage = 0;
        }

        static void InitializeShaderPlatforms()
        {
            if (s_ShaderPlatformNames != null)
                return;
            int platformMask = ShaderUtil.GetAvailableShaderCompilerPlatforms();
            var names = new List<string>();
            var indices = new List<int>();
            for (int i = 0; i < 32; ++i)
            {
                if ((platformMask & (1 << i)) == 0)
                    continue;
                names.Add(((Rendering.ShaderCompilerPlatform)i).ToString());
                indices.Add(i);
            }
            s_ShaderPlatformNames = names.ToArray();
            s_ShaderPlatformIndices = indices.ToArray();
            currentPlatformMask &= platformMask;
        }

        public override Vector2 GetWindowSize()
        {
            var rowCount = s_PlatformModes.Length + s_ShaderPlatformNames.Length + 2;
            var windowHeight = rowCount * EditorGUI.kSingleLineHeight + kSeparatorHeight * 3;
            windowHeight += 2 * kFrameWidth;

            var windowSize = new Vector2(210, windowHeight);
            return windowSize;
        }

        public override void OnGUI(Rect rect)
        {
            if (m_Shader == null)
                return;

            // We do not use the layout event
            if (Event.current.type == EventType.Layout)
                return;

            Draw(editorWindow, rect.width);

            // Use mouse move so we get hover state correctly in the menu item rows
            if (Event.current.type == EventType.MouseMove)
                Event.current.Use();

            // Escape closes the window
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                editorWindow.Close();
                GUIUtility.ExitGUI();
            }
        }

        private void DrawSeparator(ref Rect rect)
        {
            GUI.Label(new Rect(rect.x + 5, rect.y + 3, rect.width - 10, 3), GUIContent.none, Styles.separator);
            rect.y += kSeparatorHeight;
        }

        private void Draw(EditorWindow caller, float listElementWidth)
        {
            var drawPos = new Rect(0, 0, listElementWidth, EditorGUI.kSingleLineHeight);

            // Generic platform modes
            for (var i = 0; i < s_PlatformModes.Length; ++i)
            {
                DoOneMode(drawPos, i);
                drawPos.y += EditorGUI.kSingleLineHeight;
            }

            // Custom platform settings
            Color oldColor = GUI.color;
            if (currentMode != 3) // darker color when "Custom" is not selected
                GUI.color *= new Color(1, 1, 1, 0.7f);
            drawPos.xMin += 16.0f;
            for (var i = 0; i < s_ShaderPlatformNames.Length; ++i)
            {
                DoCustomPlatformBit(drawPos, i);
                drawPos.y += EditorGUI.kSingleLineHeight;
            }
            GUI.color = oldColor;
            drawPos.xMin -= 16.0f;
            DrawSeparator(ref drawPos);

            DoShaderVariants(caller, ref drawPos);
        }

        void DoOneMode(Rect rect, int index)
        {
            EditorGUI.BeginChangeCheck();
            GUI.Toggle(rect, currentMode == index, EditorGUIUtility.TempContent(s_PlatformModes[index]), Styles.menuItem);
            if (EditorGUI.EndChangeCheck())
                currentMode = index;
        }

        void DoCustomPlatformBit(Rect rect, int index)
        {
            EditorGUI.BeginChangeCheck();
            int maskBit = 1 << s_ShaderPlatformIndices[index];
            bool on = (currentPlatformMask & maskBit) != 0;
            on = GUI.Toggle(rect, on, EditorGUIUtility.TempContent(s_ShaderPlatformNames[index]), Styles.menuItem);
            if (EditorGUI.EndChangeCheck())
            {
                if (on)
                    currentPlatformMask |= maskBit;
                else
                    currentPlatformMask &= ~maskBit;
                currentMode = 3; // custom
            }
        }

        static string FormatCount(ulong count)
        {
            if (count > 1000 * 1000 * 1000)
                return ((double)count / 1000000000.0).ToString("f2", CultureInfo.InvariantCulture.NumberFormat) + "B";
            if (count > 1000 * 1000)
                return ((double)count / 1000000.0).ToString("f2", CultureInfo.InvariantCulture.NumberFormat) + "M";
            if (count > 1000)
                return ((double)count / 1000.0).ToString("f2", CultureInfo.InvariantCulture.NumberFormat) + "K";
            return count.ToString();
        }

        void DoShaderVariants(EditorWindow caller, ref Rect drawPos)
        {
            // setting for whether shader variants should be stripped
            EditorGUI.BeginChangeCheck();
            bool strip = GUI.Toggle(drawPos, currentVariantStripping == 1, EditorGUIUtility.TempContent("Skip unused shader_features"), Styles.menuItem);
            drawPos.y += EditorGUI.kSingleLineHeight;
            if (EditorGUI.EndChangeCheck())
                currentVariantStripping = strip ? 1 : 0;

            // display included variant count, and a button to show list of them
            drawPos.y += kSeparatorHeight;
            ulong variantCount = 0;
            if (strip)
            {
                if (variantsWithUsage == 0)
                    variantsWithUsage = ShaderUtil.GetVariantCount(m_Shader, true);
                variantCount = variantsWithUsage;
            }
            else
            {
                if (totalVariants == 0)
                    totalVariants = ShaderUtil.GetVariantCount(m_Shader, false);
                variantCount = totalVariants;
            }
            var variantText = FormatCount(variantCount) +
                (strip ?
                    " variants included" :
                    " variants total");
            Rect buttonRect = drawPos;
            buttonRect.x += Styles.menuItem.padding.left;
            buttonRect.width -= Styles.menuItem.padding.left + 4;
            GUI.Label(buttonRect, variantText);
            buttonRect.xMin = buttonRect.xMax - 40;
            if (GUI.Button(buttonRect, "Show", EditorStyles.miniButtonMid))
            {
                ShaderUtil.OpenShaderCombinations(m_Shader, strip);
                caller.Close();
                GUIUtility.ExitGUI();
            }
        }
    }
}
