using System;
using System.Collections.Generic;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using Unity.EditorCoroutines.Editor;
using Unity.Formats.USD;
using UnityEditor.ObjectCapture.Usd;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UnityEditor.XR.ObjectCapture
{
    class ObjectProgressRow : VisualElement
    {
        internal enum ProcessStatus
        {
            NotInitiated,
            Started,
            Generating,
            Complete
        }

        const string k_StartedText = "Started";
        const string k_GeneratingText = "Generating";
        const string k_DoneText = "Done";

        const string k_CancelDialogTitle = "Cancel Object Capture?";
        const string k_CancelDialogMessage =
            "This object is currently being processed. Are you sure you want to cancel?";

        const string k_ObjectProgressRowPath = "Packages/com.unity.xr.object-capture/Editor/UI/ObjectProgressRow.uxml";

        const string k_RowContainerName = "rowContainer";
        const string k_ObjectIconName = "objectIcon";
        const string k_ObjectNameName = "objectName";
        const string k_ProgressBarName = "progressBar";
        const string k_StatusName = "status";
        const string k_TypeOfDetailStatusName = "typeOfDetailStatus";
        const string k_DeleteButtonName = "deleteButton";

        const int k_ProgressBarPosX = 0;
        const int k_ProgressBarPosY = 4;
        const int k_ProgressBarHeight = 16;
        const int k_ProgressBarWidth = 80;

        const string k_HeicExtension = ".heic";
        const string k_PngExtension = ".png";
        const string k_JpgExtension = ".jpg";
        const string k_UsdcExtension = ".usdc";
        const string k_UsdaExtension = ".usda";

        static readonly Color k_DarkSkinColor = new Color(0.24f, 0.24f, 0.24f);
        static readonly Color k_LightSkinColor = new Color(0.8f, 0.8f, 0.8f);

        string m_CurrentProcessedFilePath = string.Empty;
        string m_ObjectPicturesInputDirectoryPath;

        Label m_StatusLabel;
        Label m_TypeOfDetailStatusLabel;

        Action<ObjectProgressRow> m_RemoveRow;
        Action<ObjectProgressRow> m_ShowPreview;

        Guid m_SessionId;
        Texture2D m_Thumbnail;

        bool m_HasPreviewBeenGenerated;
        bool m_ShowingPreviewObject;
        GameObject m_GeneratedPreviewPrefab;

        ProcessStatus m_ProcessStatus = ProcessStatus.NotInitiated;
        internal bool IsProcessing => m_ProcessStatus == ProcessStatus.Generating;

        readonly Dictionary<Guid, bool> m_RequestsCompleted = new Dictionary<Guid, bool>();

        internal int InstancedGeneratedPrefabID { get; set; }
        internal GameObject GeneratedPrefab { get; private set; }
        internal Transform ObjectTransformPivot { get; set; }

        internal bool ShowingPreview => m_ShowingPreviewObject;

        internal bool InitialBoundsSet { get; set; }
        internal Bounds ObjBounds { get; set; }

        internal float Progress { private get; set; }

        Guid GetSessionId() => m_SessionId;

        internal ObjectCaptureUtils.PhotogrammetryRequestDetail SelectedObjectDetail = ObjectCaptureUtils.PhotogrammetryRequestDetail.Full;

        internal ObjectProgressRow(string inputDirectory)
        {
            m_ObjectPicturesInputDirectoryPath = inputDirectory;
        }

        void SetupUI(string outputFilePath)
        {
            ObjBounds = new Bounds(Vector3.zero, Vector3.one * 0.2f);
            InitialBoundsSet = false;

            var progressRowAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_ObjectProgressRowPath);
            progressRowAsset.CloneTree(this);

            var objectIcon = this.Q<VisualElement>(k_ObjectIconName);
            var objectName = this.Q<Label>(k_ObjectNameName);
            var progressBar = this.Q<IMGUIContainer>(k_ProgressBarName);
            m_StatusLabel = this.Q<Label>(k_StatusName);
            m_TypeOfDetailStatusLabel = this.Q<Label>(k_TypeOfDetailStatusName);

            var rowContainerVisualElement = this.Q<VisualElement>(k_RowContainerName);
            rowContainerVisualElement.RegisterCallback<MouseDownEvent>(ShowPreviewButtonClicked);
            rowContainerVisualElement.style.backgroundColor =
                EditorGUIUtility.isProSkin ? k_DarkSkinColor : k_LightSkinColor;

            this.Q<Button>(k_DeleteButtonName).clicked += Delete;

            progressBar.onGUIHandler += ProgressBarOnGUI;

            objectIcon.style.backgroundImage = new StyleBackground(m_Thumbnail);
            var lastSlash = outputFilePath.LastIndexOf("/");
            objectName.text = lastSlash != -1 ? outputFilePath.Substring(lastSlash + 1) : outputFilePath;

            SetStatusUI(ProcessStatus.Started);
        }

        void ShowPreviewButtonClicked(MouseDownEvent evt)
        {
            ObjectCaptureWindow.CurrentPreviewObject = this;
            m_ShowPreview(this);
        }

        void SetupFileWatcher(string outputFilePath)
        {
            var fullPath = Path.GetFullPath(outputFilePath);
            var fileChecker = new ObjectCaptureFileChecker();

            if (File.Exists(fullPath))
                File.Delete(fullPath);

            m_CurrentProcessedFilePath = fullPath;
            ObjectCaptureWindow.AddCheckedFile(fullPath, fileChecker);
            EditorCoroutineUtility.StartCoroutine(fileChecker.CheckFileCreated(fullPath, UnzipUsdAndCreatePrefab), this);
        }

        internal bool TryStartPhotogrammetrySessionPreview(string inputDirectory, string outputPath,
            Action<ObjectProgressRow> removeRow, Action<ObjectProgressRow> showPreview,
            Dictionary<RequestKey, ObjectProgressRow> requestMap)
        {
#if OBJECT_CAPTURE_AVAILABLE
            if (ObjectCaptureUtils.TryCreatePhotogrammetrySession(inputDirectory, out var sessionId, out var sessionErrorCode))
            {
                if (ObjectCaptureUtils.TryAppendPhotogrammetryBoundingBoxRequest(sessionId, out var requestId, out var bboxRequestErrorCode))
                {
                    requestMap[new RequestKey(sessionId, requestId)] = this;
                    m_RequestsCompleted[requestId] = false;
                }
                else
                {
                    Debug.LogError($"Cannot create photogrammetry bounding request because {bboxRequestErrorCode}");
                }

                if (ObjectCaptureUtils.TryAppendPhotogrammetryRequest(sessionId, outputPath, ObjectCaptureUtils.PhotogrammetryRequestDetail.Preview,
                    out requestId, out var previewRequestErrorCode))
                {
                    requestMap[new RequestKey(sessionId, requestId)] = this;
                    m_RequestsCompleted[requestId] = false;
                }
                else
                {
                    Debug.LogError($"Cannot create photogrammetry request because {previewRequestErrorCode}");
                    return false;
                }

                if (ObjectCaptureUtils.TryStartPhotogrammetrySession(sessionId, ObjectCaptureWindow.MessageCallback, out var startErrorCode))
                {
                    SetUp(sessionId, outputPath, GetThumbnail(inputDirectory), showPreview, removeRow);
                    SetObjectDetailType(ObjectCaptureUtils.PhotogrammetryRequestDetail.Preview);
                    return true;
                }

                Debug.LogError($"Cannot start session:{m_SessionId} because {startErrorCode}");
            }
            else
            {
                Debug.LogError($"Cannot create photogrammetry session because {sessionErrorCode}");
            }
#endif

            return false;
        }

        static Texture2D GetThumbnail(string inputDirectory)
        {
            var files = Directory.GetFiles(inputDirectory);
            var thumbnailPath = string.Empty;
            var imageExtension = string.Empty;

            foreach (var file in files)
            {
                imageExtension = Path.GetExtension(file).ToLower();
                if (imageExtension != k_JpgExtension && imageExtension != k_PngExtension && imageExtension != k_HeicExtension)
                    continue;

                thumbnailPath = file;
                break;
            }

            if (thumbnailPath == string.Empty || !File.Exists(thumbnailPath))
            {
                Debug.LogWarning("Unable to find suitable thumbnail image for Object Capture object.");
                return null;
            }

            var thumbnailTexture = new Texture2D(64, 64, TextureFormat.RGB24, false);

            if (imageExtension == k_HeicExtension)
            {
                var imageBytes = ObjectCaptureUtils.TryLoadImage(thumbnailPath, 64, out var width, out var height, out var errorCode);
                if (errorCode == ObjectCaptureUtils.LoadImageErrorCode.None)
                {
                    var imageData = imageBytes.ToArray();
                    thumbnailTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    thumbnailTexture.LoadRawTextureData(imageData);
                    thumbnailTexture.Apply();
                }

                imageBytes.Dispose();
            }
            else
            {
                var imageData = File.ReadAllBytes(thumbnailPath);
                thumbnailTexture.LoadImage(imageData);
            }

            return thumbnailTexture;
        }

        void Delete()
        {
            if (ObjectCaptureWindow.CurrentPreviewObject != null &&
                ObjectCaptureWindow.CurrentPreviewObject.GetSessionId() == this.GetSessionId())
            {
                if (EditorObjectPreview.CurrentInstancedPrevGO.GetInstanceID() == InstancedGeneratedPrefabID)
                    Object.DestroyImmediate(EditorObjectPreview.CurrentInstancedPrevGO);

                ObjectCaptureWindow.CurrentPreviewObject = null;
            }

            switch (m_ProcessStatus)
            {
                case ProcessStatus.Generating:
                    CancelSession();
                    break;
                case ProcessStatus.Complete:
                    ObjectCaptureUtils.DestroyPhotogrammetrySession(m_SessionId);
                    break;
            }

            m_RemoveRow(this);

            ClearFileWatcherChecker();
        }

        internal void ClearFileWatcherChecker()
        {
            if (m_CurrentProcessedFilePath != string.Empty)
            {
                ObjectCaptureWindow.DeleteFromCheckedFiles(m_CurrentProcessedFilePath);
                m_CurrentProcessedFilePath = string.Empty;
            }
        }

        internal void CancelSession()
        {
            if (EditorUtility.DisplayDialog(k_CancelDialogTitle, k_CancelDialogMessage, "Yes", "No"))
            {
                ObjectCaptureUtils.TryClearPhotogrammetrySessionCallback(m_SessionId, out _);

                if (ObjectCaptureUtils.TryCancelPhotogrammetrySession(m_SessionId, out var errorCode))
                {
                    Debug.Log($"Cancelled session:{m_SessionId}");

                    // We have to set the UI to complete one frame late so we can wait for the TryClearPhotogrammetrySessionCallback callback to be called
                    EditorApplication.delayCall += () =>
                    {
                        SetStatusUI(ProcessStatus.Complete);
                        RestorePreviewPrefab();
                        Progress = 1;
                    };
                }
                else
                {
                    Debug.LogError($"Cannot cancel session:{m_SessionId} because {errorCode}");
                }
            }
        }

        void ProgressBarOnGUI()
        {
            EditorGUI.ProgressBar(new Rect(k_ProgressBarPosX, k_ProgressBarPosY, k_ProgressBarWidth, k_ProgressBarHeight), Progress, string.Empty);
        }

        internal void SetStatusUI(ProcessStatus processStatus)
        {
            m_ProcessStatus = processStatus;

            switch (m_ProcessStatus)
            {
                case ProcessStatus.NotInitiated:
                    m_StatusLabel.text = string.Empty;
                    break;
                case ProcessStatus.Started:
                    m_StatusLabel.text = k_StartedText;
                    break;
                case ProcessStatus.Generating:
                    m_StatusLabel.text = k_GeneratingText;
                    break;
                case ProcessStatus.Complete:
                    m_TypeOfDetailStatusLabel.text = string.Empty;
                    m_StatusLabel.text = k_DoneText;
                    m_StatusLabel.SetEnabled(false);
                    break;
                default:
                    Debug.LogError("Unknown processing status: " + processStatus);
                    break;
            }
        }

        void UnzipUsdAndCreatePrefab(string destinationPath)
        {
            var fastZip = new FastZip();
            var filePath = destinationPath;
            var fileName = filePath.Substring(filePath.LastIndexOf("/") + 1);
            var targetDirectory = destinationPath.Substring(0, destinationPath.LastIndexOf("/") + 1) + $"/{fileName}-extract/";
            fastZip.ExtractZip(filePath, targetDirectory, null);

            EditorApplication.delayCall += () =>
            {
                var files = Directory.GetFiles(targetDirectory);
                var foundUsd = string.Empty;

                foreach (var file in files)
                {
                    var ext = Path.GetExtension(file);
                    if (ext != k_UsdcExtension && ext != k_UsdaExtension)
                        continue;

                    foundUsd = file;

                    var handle = File.Open(file, FileMode.Open);
                    handle.Close();

                    break;
                }

                if (foundUsd == string.Empty)
                {
                    Debug.LogError("No valid USD file found.");
                    return;
                }

                var usdScene = ImportHelpers.InitForOpen(foundUsd);
                if (usdScene == null)
                {
                    Debug.LogError("Unable to initialize USD scene.");
                    return;
                }

                EditorApplication.delayCall += () =>
                {
                    var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                    var folderPath = $"Assets/{nameWithoutExtension}";
                    if (!AssetDatabase.IsValidFolder(folderPath))
                        AssetDatabase.CreateFolder("Assets", nameWithoutExtension);

                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(folderPath);

                    var prefabPath = UsdUtils.ImportAsPrefab(usdScene);
                    GeneratedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                    SetStatusUI(ProcessStatus.Complete);
                    ObjectCaptureWindow.CurrentPreviewObject = this;
                    ObjectCaptureWindow.ShowPreviewForCapturedObject(this);

                    m_ShowingPreviewObject = false;

                    if (!m_HasPreviewBeenGenerated)
                    {
                        m_GeneratedPreviewPrefab = GeneratedPrefab;
                        ObjectTransformPivot = m_GeneratedPreviewPrefab.transform;
                        m_HasPreviewBeenGenerated = true;
                        m_ShowingPreviewObject = true;
                    }
                };

                EditorObjectPreview.SettingObjToManipulateTransform = false;
            };
        }

        internal void RestorePreviewPrefab()
        {
            GeneratedPrefab = m_GeneratedPreviewPrefab;
            m_ShowingPreviewObject = true;
        }

        internal bool CanProcessBoundingBox()
        {
            var maxBoundingBox = EditorObjectPreview.CalculateBounds(m_GeneratedPreviewPrefab);
            return ObjBounds.Intersects(maxBoundingBox);
        }

        internal void TryStartPhotogrammetrySessionFinal(Dictionary<RequestKey, ObjectProgressRow> requestMap)
        {
            var fileSavePath = ObjectCaptureWindow.RequestObjCaptureSaveLocationToUser("Save Selected Quality Asset");

            if (string.IsNullOrEmpty(fileSavePath))
                return;

            if (ObjectCaptureUtils.TryCreatePhotogrammetrySession(m_ObjectPicturesInputDirectoryPath, out var sessionId, out var sessionErrorCode))
            {
                m_SessionId = sessionId;

                var objPreviewPose = new Pose(ObjectTransformPivot.position, ObjectTransformPivot.rotation);
                var inverseRotation = Quaternion.Inverse(objPreviewPose.rotation);
                var geoPose = new Pose(inverseRotation * -objPreviewPose.position, inverseRotation);
                var geo = new PhotogrammetryRequestGeometry(ObjBounds, Vector3.one, geoPose);

                if (ObjectCaptureUtils.TryAppendPhotogrammetryGeometryRequest(m_SessionId, fileSavePath,
                    SelectedObjectDetail, geo, out var requestId, out var errorCode))
                {
                    requestMap[new RequestKey(m_SessionId, requestId)] = this;
                    m_RequestsCompleted[requestId] = false;
                }
                else
                {
                    Debug.LogError($"Error appending geometry request: {errorCode}");
                    return;
                }

                if (ObjectCaptureUtils.TryStartPhotogrammetrySession(m_SessionId, ObjectCaptureWindow.MessageCallback, out var startErrorCode))
                {
                    SetupFileWatcher(fileSavePath);
                    SetObjectDetailType(SelectedObjectDetail);
                    SetStatusUI(ProcessStatus.Started);

                    return;
                }

                Debug.LogError($"Cannot start session:{m_SessionId} because {startErrorCode}");
            }
            else
            {
                Debug.LogError($"Cannot create photogrammetry session because {sessionErrorCode}");
            }
        }

        void SetObjectDetailType(ObjectCaptureUtils.PhotogrammetryRequestDetail objQuality)
        {
            m_TypeOfDetailStatusLabel.text = objQuality.ToString();
        }

        void SetUp(Guid sessionId, string outputPath, Texture2D thumbnail, Action<ObjectProgressRow> showPreview, Action<ObjectProgressRow> removeRow)
        {
            m_SessionId = sessionId;
            m_ShowPreview = showPreview;
            m_RemoveRow = removeRow;
            m_Thumbnail = thumbnail;
            SetupFileWatcher(outputPath);
            SetupUI(outputPath);
        }

        internal void OnDestroy()
        {
            ObjectCaptureUtils.TryClearPhotogrammetrySessionCallback(m_SessionId, out _);
            Delete();
        }

        internal void OnRequestCompleted(Guid requestId)
        {
            m_RequestsCompleted[requestId] = true;

            foreach (var kvp in m_RequestsCompleted)
            {
                if (!kvp.Value)
                    return;
            }

            SetStatusUI(ProcessStatus.Complete);
        }
    }
}
