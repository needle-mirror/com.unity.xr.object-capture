using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.XR.ObjectCapture
{
    /// <summary>
    /// Main window for ObjectCapture processing
    /// </summary>
    internal class ObjectCaptureWindow : EditorWindow
    {
        static class Styles
        {
            internal const string captureHintInfoContent =
                "\nFor best results, use 20-200 photos of an object from all sides and angles." +
                "\n\n · Capture the entire object in focus." +
                "\n · With 70%+ overlap between photos." +
                "\n · Use good diffused lighting." +
                "\n · Opaque, matte and varied surface texture objects work best.\n";

            internal static readonly Vector2 minWindowSize = new Vector2(500, 600);
        }

        const string k_WindowXMLPath = "Packages/com.unity.xr.object-capture/Editor/UI/ObjectCaptureWindow.uxml";
        const string k_StylesheetPath = "Packages/com.unity.xr.object-capture/Editor/UI/ObjectCaptureWindow.uss";

        const string k_PreviewWindowSeparatorName = "PreviewWindowSeparatorVisualElement";
        const string k_PreviewSeparatorDarkImagePath = "Packages/com.unity.xr.object-capture/Editor/UI/Resources/Images/PreviewSeparateDark.png";
        const string k_PreviewSeparatorLightImagePath = "Packages/com.unity.xr.object-capture/Editor/UI/Resources/Images/PreviewSeparateLight.png";

        const string k_SelectPhotosButtonName = "selectPhotos";
        const string k_CaptureHintInfoContainerName = "captureHintInfoVisualElement";
        const string k_ObjectPreviewName = "objectPreviewIMGUIContainer";
        const string k_CapturedObjectsFoldoutName = "capturedObjectsFoldout";
        const string k_ObjectQualityDropDownName = "objQualityImGUIDropDown";
        const string k_ObjectViewerCancelName = "objectViewerCancel";
        const string k_ObjectViewerGenerateModelName = "objViewerGenerateModel";
        const string k_AdjustBoundsName = "adjustBoundsButton";
        const string k_ResetViewName = "resetViewButton";

        const string k_ReusedObjCaptureNameTitle = "Object Capture file name already processing";
        const string k_ReusedObjCaptureNameMsg = "The selected filename is already being processed on another object capture. Please select a different name for this object capture.";

        static string objectCaptureDefaultFileName => "ObjCapture" + DateTime.Now.ToString("HH-mm-ss");

        static Dictionary<string, ObjectCaptureFileChecker> s_CheckedFiles = new Dictionary<string, ObjectCaptureFileChecker>();
        static readonly EditorObjectPreview k_ObjectPreview = new EditorObjectPreview();

        static readonly Color k_DarkSkinColor = new Color(0.2745f, 0.2745f, 0.2745f);
        static readonly Color k_LightSkinColor = new Color(0.713f, 0.713f, 0.713f);

        static readonly HashSet<ObjectCaptureWindow> k_ObjectCaptureWindows = new HashSet<ObjectCaptureWindow>();

        static ObjectCaptureDetailUI currentSelectedUIDetail = ObjectCaptureDetailUI.Full;

        internal static event Action ExecuteOnMainThread;

        internal static ObjectProgressRow CurrentPreviewObject;

        enum ObjectCaptureDetailUI
        {
            Reduced = 1,
            Medium = 2,
            Full = 3,
            Raw = 4
        }

        Foldout m_CapturedObjectsFoldout;

        Button m_CancelGenerateButton;
        Button m_GenerateModelButton;
        Button m_AdjustBoundsButton;
        Button m_ResetViewButton;

        VisualElement m_ObjPreviewVisualElement;

        List<ObjectProgressRow> m_ProgressRows = new List<ObjectProgressRow>();
        Dictionary<RequestKey, ObjectProgressRow> m_RequestMap = new Dictionary<RequestKey, ObjectProgressRow>();

        internal static void DeleteFromCheckedFiles(string key)
        {
            if (s_CheckedFiles.ContainsKey(key))
            {
                s_CheckedFiles[key].CancelCheck();
                s_CheckedFiles.Remove(key);
            }
        }

        internal static void AddCheckedFile(string key, ObjectCaptureFileChecker checkedFile)
        {
            if (s_CheckedFiles.ContainsKey(key))
            {
                Debug.LogError("Checked file already exists!");
                return;
            }

            s_CheckedFiles.Add(key, checkedFile);
        }

        [MenuItem("Window/Object Capture %#&o")]
        internal static void InitWindow()
        {
            var win = GetWindow<ObjectCaptureWindow>();
            win.titleContent = new GUIContent("Object Capture");
            win.minSize = Styles.minWindowSize;
            win.Show();
        }

        [MenuItem("Window/Object Capture %#&o", true)]
        internal static bool IsPhotogrammetryAvailable()
        {
#if OBJECT_CAPTURE_AVAILABLE
            return ObjectCaptureUtils.IsPhotogrammetryAvailable();
#else
            return false;
#endif
        }

        void OnEnable()
        {
            k_ObjectCaptureWindows.Add(this);
            InitUI();

            ObjectCaptureUtils.SetLoggingEnabled(false);
        }

        void Update()
        {
            if (ExecuteOnMainThread == null)
                return;

            lock (ExecuteOnMainThread)
            {
                ExecuteOnMainThread();
                ExecuteOnMainThread = null;
                Repaint();
            }
        }

        void InitUI()
        {
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_WindowXMLPath).CloneTree(rootVisualElement);
            rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(k_StylesheetPath));

            m_CapturedObjectsFoldout = rootVisualElement.Q<Foldout>(k_CapturedObjectsFoldoutName);
            m_CapturedObjectsFoldout.Q<Toggle>().style.backgroundColor =
                EditorGUIUtility.isProSkin ? k_DarkSkinColor : k_LightSkinColor;

            rootVisualElement.Q<Button>(k_SelectPhotosButtonName).clicked += SelectResourcesAndStartProcessing;
            m_CancelGenerateButton = rootVisualElement.Q<Button>(k_ObjectViewerCancelName);
            m_CancelGenerateButton.clicked += () =>
            {
                if (EditorObjectPreview.CurrentPreviewedProgressRow != null &&
                    EditorObjectPreview.CurrentPreviewedProgressRow.IsProcessing)
                {
                    // revert the status of the object to being complete
                    EditorObjectPreview.CurrentPreviewedProgressRow.ClearFileWatcherChecker();
                    EditorObjectPreview.CurrentPreviewedProgressRow.CancelSession();
                }
            };

            // We only show the cancel button when we are generating an object (not for preview mode)
            m_CancelGenerateButton.style.display = DisplayStyle.None;

            m_GenerateModelButton = rootVisualElement.Q<Button>(k_ObjectViewerGenerateModelName);
            m_GenerateModelButton.SetEnabled(false);
            m_GenerateModelButton.clicked += GenerateModel;

            rootVisualElement.Q<IMGUIContainer>(k_CaptureHintInfoContainerName).onGUIHandler += CaptureHintInfoOnGUI;

            k_ObjectPreview.Init();
            m_ObjPreviewVisualElement = rootVisualElement.Q<IMGUIContainer>(k_ObjectPreviewName);
            ((IMGUIContainer) m_ObjPreviewVisualElement).onGUIHandler += ObjectPreviewOnGUI;
            m_ObjPreviewVisualElement.focusable = true;

            var previewSeparateWindowVisualElement = rootVisualElement.Q<VisualElement>(k_PreviewWindowSeparatorName);
            previewSeparateWindowVisualElement.style.backgroundImage = EditorGUIUtility.isProSkin
                ? AssetDatabase.LoadAssetAtPath<Texture2D>(k_PreviewSeparatorDarkImagePath)
                : AssetDatabase.LoadAssetAtPath<Texture2D>(k_PreviewSeparatorLightImagePath);
            previewSeparateWindowVisualElement.RegisterCallback<ClickEvent>(evt =>
                m_ObjPreviewVisualElement.style.display = m_ObjPreviewVisualElement.style.display == DisplayStyle.Flex ? DisplayStyle.None : DisplayStyle.Flex
            );

            m_ObjPreviewVisualElement.style.display = DisplayStyle.Flex;

            rootVisualElement.Q<IMGUIContainer>(k_ObjectQualityDropDownName).onGUIHandler += ObjectQualityEnumPopupOnGUI;

            m_AdjustBoundsButton = rootVisualElement.Q<Button>(k_AdjustBoundsName);
            m_AdjustBoundsButton.style.backgroundImage = new StyleBackground((Texture2D) EditorGUIUtility.IconContent("d_Transform Icon").image);
            m_AdjustBoundsButton.clicked += () => EditorObjectPreview.SettingObjToManipulateTransform = !EditorObjectPreview.SettingObjToManipulateTransform;

            m_ResetViewButton = rootVisualElement.Q<Button>(k_ResetViewName);
            m_ResetViewButton.clicked += () => k_ObjectPreview.ResetView();
        }


        static void ObjectQualityEnumPopupOnGUI()
        {
            if (CurrentPreviewObject == null)
                return;

            GUILayout.Space(2);

            currentSelectedUIDetail = (ObjectCaptureDetailUI) EditorGUILayout.EnumPopup(currentSelectedUIDetail);
            CurrentPreviewObject.SelectedObjectDetail = ConvertToPhotogrammetryRequestDetail(currentSelectedUIDetail);
        }

        static ObjectCaptureUtils.PhotogrammetryRequestDetail ConvertToPhotogrammetryRequestDetail(ObjectCaptureDetailUI detailUISide)
        {
            switch (detailUISide)
            {
                case ObjectCaptureDetailUI.Reduced:
                    return ObjectCaptureUtils.PhotogrammetryRequestDetail.Reduced;
                case ObjectCaptureDetailUI.Medium:
                    return ObjectCaptureUtils.PhotogrammetryRequestDetail.Medium;
                case ObjectCaptureDetailUI.Full:
                    return ObjectCaptureUtils.PhotogrammetryRequestDetail.Full;
                case ObjectCaptureDetailUI.Raw:
                    return ObjectCaptureUtils.PhotogrammetryRequestDetail.Raw;
                default:
                    return ObjectCaptureUtils.PhotogrammetryRequestDetail.Full;
            }
        }

        void SelectResourcesAndStartProcessing()
        {
            var directory = EditorUtility.OpenFolderPanel("Select directory", string.Empty, string.Empty);
            StartProcessing(directory);
        }

        /// <summary>
        /// Start processing an object capture on the given directory.
        /// This will prompt the user for a path to save the processed object file and add a row to the UI.
        /// </summary>
        /// <param name="directory">A valid path containing images for processing</param>
        internal void StartProcessing(string directory)
        {
            if (!Directory.Exists(directory))
            {
                Debug.LogError("Please pick a valid directory of photos to use Object Capture.");
                return;
            }

            var saveLocation = RequestObjCaptureSaveLocationToUser("Save As");

            if (string.IsNullOrEmpty(saveLocation))
            {
                return;
            }

            var saveDirectory = Path.GetDirectoryName(saveLocation);
            if (!string.IsNullOrEmpty(saveDirectory))
            {
                if (!Directory.Exists(saveDirectory))
                    Directory.CreateDirectory(saveDirectory);
            }

            StartObjectCapture(directory, saveLocation);
        }

        internal static string RequestObjCaptureSaveLocationToUser(string title)
        {
            string saveLocation;
            do
            {
                saveLocation = EditorUtility.SaveFilePanel(title, "~/Desktop", objectCaptureDefaultFileName, "usdz");
                if (s_CheckedFiles.ContainsKey(saveLocation))
                {
                    var pressedOk = EditorUtility.DisplayDialog(k_ReusedObjCaptureNameTitle, k_ReusedObjCaptureNameMsg, "Ok", "Cancel");
                    if (!pressedOk)
                        return "";
                }
            } while (s_CheckedFiles.ContainsKey(saveLocation));

            return saveLocation;
        }

        void StartObjectCapture(string inputDirectory, string outputFilePath)
        {
            if (!Directory.Exists(inputDirectory))
            {
                Debug.LogErrorFormat("Invalid directory '{0}'. Canceling Object Capture.", inputDirectory);
                return;
            }

            var progressRow = new ObjectProgressRow(inputDirectory);
            if (progressRow.TryStartPhotogrammetrySessionPreview(inputDirectory, outputFilePath, RemoveObjectProgressRow,
                ShowPreviewForCapturedObject, m_RequestMap))
            {
                AddProgressRow(progressRow);
            }
        }

        void AddProgressRow(ObjectProgressRow progressRow)
        {
            m_ProgressRows.Add(progressRow);
            m_CapturedObjectsFoldout.Insert(0, progressRow);

            Repaint();
        }

        void RemoveObjectProgressRow(ObjectProgressRow row)
        {
            if (row != null && m_CapturedObjectsFoldout.Contains(row))
                m_CapturedObjectsFoldout.Remove(row);
        }

        internal static void ShowPreviewForCapturedObject(ObjectProgressRow objectInProcess)
        {
            if (objectInProcess.IsProcessing)
                objectInProcess.RestorePreviewPrefab();

            if (objectInProcess.GeneratedPrefab == null && !objectInProcess.IsProcessing)
            {
                Debug.LogError("Prefab not found.");
                return;
            }

            k_ObjectPreview.SetRowToPreview(objectInProcess);
        }

        void ObjectPreviewOnGUI()
        {
            // on the first frame the resolved size is 0; this causes an error on the first frame
            var width = (int) m_ObjPreviewVisualElement.resolvedStyle.width > 0 ? (int) m_ObjPreviewVisualElement.resolvedStyle.width : 1;
            var height = (int) m_ObjPreviewVisualElement.resolvedStyle.height > 0 ? (int) m_ObjPreviewVisualElement.resolvedStyle.height : 1;
            var top = (int) m_ObjPreviewVisualElement.resolvedStyle.top;

            k_ObjectPreview.DrawGUI(top, width, height);

            var showCancelButton = EditorObjectPreview.CurrentPreviewedProgressRow != null && EditorObjectPreview.CurrentPreviewedProgressRow.IsProcessing;
            m_CancelGenerateButton.style.display = showCancelButton ? DisplayStyle.Flex : DisplayStyle.None;

            var enableObjCaptureButtons = EditorObjectPreview.CurrentInstancedPrevGO != null && CurrentPreviewObject.ShowingPreview;
            m_GenerateModelButton.SetEnabled(enableObjCaptureButtons);
            m_AdjustBoundsButton.SetEnabled(enableObjCaptureButtons);
            m_ResetViewButton.SetEnabled(enableObjCaptureButtons);
            Repaint();
        }

        static void CaptureHintInfoOnGUI()
        {
            EditorGUILayout.HelpBox(Styles.captureHintInfoContent, MessageType.Info);
        }

        void GenerateModel()
        {
            if (CurrentPreviewObject == null)
                return;

            if (!CurrentPreviewObject.CanProcessBoundingBox())
            {
                Debug.LogError("The selected bounding box does not contain the preview mesh.");
                return;
            }

            CurrentPreviewObject.TryStartPhotogrammetrySessionFinal(m_RequestMap);

            ShowTab();
            Show();

            Repaint();
        }

        void OnDisable()
        {
            EditorObjectPreview.OnDestroy();

            foreach (var keyValuePair in s_CheckedFiles)
                keyValuePair.Value.CancelCheck();

            s_CheckedFiles.Clear();

            foreach (var row in m_ProgressRows)
            {
                RemoveObjectProgressRow(row);
                row.OnDestroy();
            }
        }

        internal static void MessageCallback(PhotogrammetrySessionMessage message)
        {
            var processedMessage = false;
            foreach (var window in k_ObjectCaptureWindows)
            {
                processedMessage |= window.OnMessageCallback(message);
            }

            if (!processedMessage)
            {
                if (message.messageType == PhotogrammetrySessionMessage.MessageType.RequestError ||
                    message.messageType == PhotogrammetrySessionMessage.MessageType.InvalidSample)
                {
                    Debug.LogError(message);
                }
                else
                {
                    Debug.Log(message);
                }
            }
        }

        bool OnMessageCallback(PhotogrammetrySessionMessage message)
        {
            var sessionId = message.sessionId;
            var requestId = message.requestId;
            var key = new RequestKey(sessionId, requestId);
            if (!m_RequestMap.TryGetValue(key, out var row))
                return false;

            switch (message.messageType)
            {
                case PhotogrammetrySessionMessage.MessageType.RequestProgress:
                    ExecuteOnMainThread += () => row.SetStatusUI(ObjectProgressRow.ProcessStatus.Generating);
                    row.Progress = message.requestProgress;
                    return true;
                case PhotogrammetrySessionMessage.MessageType.RequestComplete:
                    ExecuteOnMainThread += () => row.OnRequestCompleted(requestId);
                    return true;
            }

            return false;
        }
    }
}
