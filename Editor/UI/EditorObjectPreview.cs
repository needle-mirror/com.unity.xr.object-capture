using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace UnityEditor.XR.ObjectCapture
{
    class EditorObjectPreview
    {
        const string k_PreviewCaptureLabel = "Preview Capture";
        const string k_RestoreToPreviewBtnLabel = "Restore preview";

        const string k_PreviewCameraName = "PreviewCamera";
        const float k_CameraMoveSpeed = 80f;
        const float k_PrevObjRotSpeed = 1000;

        static readonly BoxBoundsHandle k_BoundsHandle = new BoxBoundsHandle();

        static readonly Rect k_TopFaceRectArea = new Rect(5, 3, 40, 19);
        static readonly Rect k_FrontFaceRectArea = new Rect(5, 23, 20, 24);
        static readonly Rect k_SideFaceRectArea = new Rect(26, 23, 19, 24);

        static readonly Quaternion k_PrevCamTopRot = Quaternion.Euler(90, 0, 0);
        static readonly Quaternion k_PrevCamSideRot = Quaternion.Euler(0, 270, 0);
        static readonly Quaternion k_PrevCamFrontRot = Quaternion.Euler(0, 180, 0);

        static readonly Vector3 k_InitialCameraPos = new Vector3(3, 2, 5);

        static readonly Color k_BoundsColor = new Color(1, 1, 1, 0.75f);

        static Scene s_PreviewScene;

        static ObjectProgressRow s_CurrentPreviewedRow;
        static GameObject s_CurrInstancedPrevGO;

        static Texture2D s_FaceWidgetTex;
        static Texture2D s_FaceWidgetFrontTex;
        static Texture2D s_FaceWidgetSideTex;
        static Texture2D s_FaceWidgetTopTex;
        static Texture2D s_CurrentLoadedFaceWidgetTex;

        Camera m_PreviewCam;
        bool m_RotateAroundObj;
        float m_EditorDeltaTime;
        double m_LastTimeSinceStartup;

        Camera PreviewCam
        {
            get
            {
                if (m_PreviewCam == null)
                {
                    var cam = GameObject.Find(k_PreviewCameraName);

                    var previewCameraGO = cam == null ? new GameObject(k_PreviewCameraName) : cam;
                    previewCameraGO.transform.position = k_InitialCameraPos;
                    previewCameraGO.hideFlags = HideFlags.HideAndDontSave;
                    m_PreviewCam = previewCameraGO.AddComponent<Camera>();
                    m_PreviewCam.nearClipPlane = 0.01f;
                    m_PreviewCam.clearFlags = CameraClearFlags.SolidColor;
                    m_PreviewCam.backgroundColor = Color.gray;
                    m_PreviewCam.orthographic = true;
                    m_PreviewCam.enabled = false;
                }

                return m_PreviewCam;
            }
        }

        internal static bool SettingObjToManipulateTransform { get; set; }

        internal static GameObject CurrentInstancedPrevGO => s_CurrInstancedPrevGO;
        internal static ObjectProgressRow CurrentPreviewedProgressRow => s_CurrentPreviewedRow;

        internal void SetRowToPreview(ObjectProgressRow objProgressRow)
        {
            if (objProgressRow.GeneratedPrefab == null)
                return;

            if (s_CurrInstancedPrevGO != null)
                Object.DestroyImmediate(s_CurrInstancedPrevGO);

            s_CurrentPreviewedRow = objProgressRow;
            s_CurrInstancedPrevGO = Object.Instantiate(s_CurrentPreviewedRow.GeneratedPrefab);
            s_CurrInstancedPrevGO.hideFlags = HideFlags.HideAndDontSave;
            s_CurrInstancedPrevGO.transform.position = Vector3.zero;
            s_CurrInstancedPrevGO.transform.rotation = Quaternion.identity;

            if (!s_PreviewScene.isLoaded) // The created scene gets unloaded when this window is maximized and minimized.
                InitializePreviewScene();

            SceneManager.MoveGameObjectToScene(s_CurrInstancedPrevGO, s_PreviewScene);

            objProgressRow.InstancedGeneratedPrefabID = s_CurrInstancedPrevGO.GetInstanceID();

            Selection.activeObject = s_CurrentPreviewedRow.GeneratedPrefab;

            FocusPreviewCamOnGameObject(s_CurrInstancedPrevGO);

            if (!objProgressRow.InitialBoundsSet)
            {
                objProgressRow.ObjBounds = CalculateBounds(s_CurrInstancedPrevGO);
                objProgressRow.InitialBoundsSet = true;
            }
        }

        void InitializePreviewScene()
        {
            s_PreviewScene = EditorSceneManager.NewPreviewScene();
            PreviewCam.scene = s_PreviewScene;

            SceneManager.MoveGameObjectToScene(PreviewCam.gameObject, s_PreviewScene);

            var previewSceneLightGO = new GameObject("Directional Light");
            previewSceneLightGO.transform.rotation = k_PrevCamTopRot;
            var sceneLight = previewSceneLightGO.AddComponent<Light>();
            sceneLight.color = Color.white;
            sceneLight.intensity = 0.65f;
            sceneLight.type = LightType.Directional;
            SceneManager.MoveGameObjectToScene(previewSceneLightGO, s_PreviewScene);
        }
        
        internal void Init()
        {
            InitializePreviewScene();

            SettingObjToManipulateTransform = false;

            k_BoundsHandle.wireframeColor = k_BoundsColor;

            s_FaceWidgetFrontTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.unity.xr.object-capture/Editor/UI/Resources/Images/PreviewSides/FaceWidget_Front.png");
            s_FaceWidgetSideTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.unity.xr.object-capture/Editor/UI/Resources/Images/PreviewSides/FaceWidget_Side.png");
            s_FaceWidgetTopTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.unity.xr.object-capture/Editor/UI/Resources/Images/PreviewSides/FaceWidget_Top.png");
            s_FaceWidgetTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.unity.xr.object-capture/Editor/UI/Resources/Images/PreviewSides/FaceWidget.png");

            s_CurrentLoadedFaceWidgetTex = s_FaceWidgetTex;
        }

        internal void DrawGUI(int top, int previewWidth, int previewHeight)
        {
            if (s_CurrentPreviewedRow == null || s_CurrInstancedPrevGO == null)
            {
                EditorGUI.DrawRect(new Rect(0, 0, previewWidth, previewHeight), Color.gray);
                return;
            }

            CalculateEditorDeltaTime();
            ProcessInput();

            const int offsetTopPreview = 20;
#if UNITY_2020
            var mouseOffset = new Vector2(0, -offsetTopPreview - top);
#else
            const int footerOffset = 30;
            Vector2 mouseOffset = new Vector2(0, footerOffset);
#endif
            Handles.SetCamera(PreviewCam);
            Handles.DrawCamera(new Rect(0, top + offsetTopPreview, previewWidth, previewHeight), PreviewCam);

            Event.current.mousePosition -= mouseOffset;
            if (SettingObjToManipulateTransform)
            {
                var rotation = CurrentPreviewedProgressRow.ObjectTransformPivot.rotation;
                var position = CurrentPreviewedProgressRow.ObjectTransformPivot.position;

                Handles.TransformHandle(ref position, ref rotation);
                CurrentPreviewedProgressRow.ObjectTransformPivot.rotation = rotation;
                CurrentPreviewedProgressRow.ObjectTransformPivot.position = position;
            }

            if (CurrentPreviewedProgressRow.ShowingPreview)
            {
                var objToManipulateBounds = s_CurrentPreviewedRow.ObjBounds;
                DrawBoundsHandle(ref objToManipulateBounds);
                s_CurrentPreviewedRow.ObjBounds = objToManipulateBounds;
            }

            Event.current.mousePosition += mouseOffset;

            Handles.BeginGUI();
            DrawOrientationUI(previewWidth);
            ShowFooterPreviewInfo(previewWidth, previewHeight);
            Handles.EndGUI();

        }

        static void ShowFooterPreviewInfo(int previewWidth, int previewHeight)
        {
            if (s_CurrentPreviewedRow == null || s_CurrInstancedPrevGO == null)
                return;
            const int offsetWidth = 105;
            const int offsetHeight = 22;
            using (new GUILayout.AreaScope(new Rect(previewWidth - offsetWidth, previewHeight - offsetHeight, offsetWidth, offsetHeight)))
            {
                if (CurrentPreviewedProgressRow.ShowingPreview)
                    GUILayout.Label(k_PreviewCaptureLabel);
                else
                {
                    if (GUILayout.Button(k_RestoreToPreviewBtnLabel))
                    {
                        CurrentPreviewedProgressRow.RestorePreviewPrefab();
                        ObjectCaptureWindow.ShowPreviewForCapturedObject(CurrentPreviewedProgressRow);
                    }
                }
            }
        }

        internal void ResetView()
        {
            if (s_CurrInstancedPrevGO == null)
                return;

            s_CurrInstancedPrevGO.transform.rotation = Quaternion.identity;
            PreviewCam.transform.position = k_InitialCameraPos;
            FocusPreviewCamOnGameObject(s_CurrInstancedPrevGO);

            s_CurrentPreviewedRow.ObjBounds = CalculateBounds(s_CurrInstancedPrevGO);
            s_CurrentPreviewedRow.ObjectTransformPivot.rotation = Quaternion.identity;
            s_CurrentPreviewedRow.ObjectTransformPivot.position = Vector3.zero;
        }

        static void DrawBoundsHandle(ref Bounds bounds)
        {
            k_BoundsHandle.center = bounds.center;
            k_BoundsHandle.size = bounds.size;

            k_BoundsHandle.DrawHandle();

            var newBounds = new Bounds();
            newBounds.center = k_BoundsHandle.center;
            newBounds.size = k_BoundsHandle.size;
            bounds = newBounds;
        }

        void ProcessInput()
        {
            var currentEvt = Event.current;

            if (currentEvt.isMouse && currentEvt.button == 1)
            {
                m_RotateAroundObj = true;
            }

            if (currentEvt.type == EventType.MouseUp)
            {
                m_RotateAroundObj = false;
            }

            if (currentEvt.type == EventType.ScrollWheel)
            {
                var scrollDelta = currentEvt.delta.y;
                PreviewCam.orthographicSize += m_EditorDeltaTime * scrollDelta * k_CameraMoveSpeed;
            }

            if (currentEvt.type == EventType.MouseDrag)
            {
                if (s_CurrentPreviewedRow != null && s_CurrInstancedPrevGO != null)
                {
                    if (m_RotateAroundObj)
                    {
                        var pivot = s_CurrInstancedPrevGO.transform.position;
                        var prevCamTransform = PreviewCam.transform;
                        prevCamTransform.RotateAround(pivot, prevCamTransform.up, currentEvt.delta.x * m_EditorDeltaTime * k_PrevObjRotSpeed * EditorGUIUtility.pixelsPerPoint);
                        prevCamTransform.RotateAround(pivot, prevCamTransform.right, currentEvt.delta.y * m_EditorDeltaTime * k_PrevObjRotSpeed * EditorGUIUtility.pixelsPerPoint);
                    }
                }
            }
        }

        void DrawOrientationUI(int previewWidth)
        {
            if (s_CurrentPreviewedRow == null || s_CurrInstancedPrevGO == null)
                return;

            const int offsetWidth = 60;
            const int rectPosY = 10;
            const int rectSize = 50;
            using (new GUILayout.AreaScope(new Rect(previewWidth - offsetWidth, rectPosY, rectSize, rectSize)))
            {
                GUI.DrawTexture(new Rect(0, 0, 50, 50), s_CurrentLoadedFaceWidgetTex);

                var distanceFromCamera = 5;
                var currEvt = Event.current;
                if (Event.current.type == EventType.MouseDown)
                {
                    var previewCamTransform = PreviewCam.transform;
                    if (k_TopFaceRectArea.Contains(currEvt.mousePosition))
                    {
                        s_CurrentLoadedFaceWidgetTex = s_FaceWidgetTopTex;
                        var pos = s_CurrInstancedPrevGO.transform.position + new Vector3(0, distanceFromCamera, 0);
                        previewCamTransform.position = pos;
                        FocusPreviewCamOnGameObject(s_CurrInstancedPrevGO);

                        previewCamTransform.position = pos;
                        previewCamTransform.rotation = k_PrevCamTopRot;
                    }

                    if (k_SideFaceRectArea.Contains(currEvt.mousePosition))
                    {
                        s_CurrentLoadedFaceWidgetTex = s_FaceWidgetSideTex;
                        var pos = s_CurrInstancedPrevGO.transform.position + new Vector3(distanceFromCamera, 0, 0);
                        previewCamTransform.position = pos;
                        FocusPreviewCamOnGameObject(s_CurrInstancedPrevGO);

                        previewCamTransform.position = pos;
                        previewCamTransform.rotation = k_PrevCamSideRot;
                    }

                    if (k_FrontFaceRectArea.Contains(currEvt.mousePosition))
                    {
                        s_CurrentLoadedFaceWidgetTex = s_FaceWidgetFrontTex;
                        var pos = s_CurrInstancedPrevGO.transform.position + new Vector3(0, 0, distanceFromCamera);
                        previewCamTransform.position = pos;
                        FocusPreviewCamOnGameObject(s_CurrInstancedPrevGO);

                        previewCamTransform.position = pos;
                        previewCamTransform.rotation = k_PrevCamFrontRot;
                    }
                }

                if (PreviewCam.transform.rotation != k_PrevCamTopRot && PreviewCam.transform.rotation != k_PrevCamSideRot &&
                    PreviewCam.transform.rotation != k_PrevCamFrontRot)
                    s_CurrentLoadedFaceWidgetTex = s_FaceWidgetTex;
            }
        }

        void CalculateEditorDeltaTime()
        {
            if (m_LastTimeSinceStartup == 0f)
                m_LastTimeSinceStartup = EditorApplication.timeSinceStartup;

            m_EditorDeltaTime = (float) (EditorApplication.timeSinceStartup - m_LastTimeSinceStartup);
            m_LastTimeSinceStartup = EditorApplication.timeSinceStartup;
        }

        internal static Bounds CalculateBounds(GameObject go)
        {
            var bounds = new Bounds(go.transform.position, Vector3.zero);
            var renderers = go.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);

            return bounds;
        }

        void FocusPreviewCamOnGameObject(GameObject gameObject)
        {
            var calculatedBounds = CalculateBounds(gameObject);
            var max = calculatedBounds.size;

            var radius = max.magnitude * 0.5f;
            var horizontalFOV = 2f * Mathf.Atan(Mathf.Tan(PreviewCam.fieldOfView * Mathf.Deg2Rad * 0.5f) * PreviewCam.aspect) * Mathf.Rad2Deg;
            var fov = Mathf.Min(PreviewCam.fieldOfView, horizontalFOV) * 0.5f;
            var dist = radius / Mathf.Sin(fov * Mathf.Deg2Rad * 0.5f);

            var previewCamTransform = PreviewCam.transform;
            var camLocalPos = previewCamTransform.localPosition;
            camLocalPos.z = dist;
            previewCamTransform.localPosition = camLocalPos;

            if (PreviewCam.orthographic)
                PreviewCam.orthographicSize = radius;

            PreviewCam.transform.LookAt(calculatedBounds.center);
        }

        internal static void OnDestroy()
        {
            EditorSceneManager.ClosePreviewScene(s_PreviewScene);
            var cam = GameObject.Find(k_PreviewCameraName);
            if (cam != null)
                Object.DestroyImmediate(cam);

            if (s_CurrInstancedPrevGO != null)
                Object.DestroyImmediate(s_CurrInstancedPrevGO);
        }
    }
}
