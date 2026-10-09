using UnityEditor;
using UnityEngine;
using Varneon.VUdon.Editors.Editor;

namespace Varneon.VUdon.Playerlist.Editor
{
    [CustomEditor(typeof(Playerlist))]
    public class PlayerlistEditor : InspectorBase
    {
        [SerializeField]
        private Texture2D headerIcon;

        private bool showWindowSettings;

        private Transform playerlistTransform;

        private RectTransform canvasRectTransform;

        private float windowWidth, windowHeight, windowScale;

        private static readonly GUIContent WindowFoldoutContent = new GUIContent("Window", "Adjust console window");

        protected override string PersistenceKey => "Varneon/VUdon/Playerlist/Editor/Foldout";

        protected override int CustomPersistentBoolCount => 1;

        protected override InspectorHeader Header => new InspectorHeaderBuilder()
            .WithTitle("VUdon - Playerlist")
            .WithDescription("UI for displaying information about players in the instance")
            .WithURL("GitHub", "https://github.com/Varneon")
            .WithIcon(headerIcon)
            .Build();

        protected override void OnEnable()
        {
            base.OnEnable();

            Playerlist playerlist = (Playerlist)target;

            playerlistTransform = playerlist.transform;

            canvasRectTransform = playerlist.windowRoot;

            if (canvasRectTransform)
            {
                Vector2 windowResolution = canvasRectTransform.sizeDelta;

                windowWidth = windowResolution.x;

                windowHeight = windowResolution.y;

                windowScale = canvasRectTransform.localScale.x;

                showWindowSettings = customPersistentBools[0];
            }
        }

        protected override void OnDestroy()
        {
            customPersistentBools[0] = showWindowSettings;

            base.OnDestroy();
        }

        protected override void OnPreDrawFields()
        {
            if(canvasRectTransform == null)
            {
                EditorGUILayout.HelpBox("Window settings can't be accessed because windowRoot hasn't been assigned to the Playerlist component!", MessageType.Error);

                return;
            }

            Vector2 canvasScale = canvasRectTransform.localScale;

            if(canvasScale.x != canvasScale.y)
            {
                EditorGUILayout.HelpBox("Canvas scale's X and Y axis doesn't match. Please do not directly manipulate the RectTransform component of the window, use the Window settings panel below instead.", MessageType.Error);
            }

            if(playerlistTransform.lossyScale != Vector3.one)
            {
                EditorGUILayout.HelpBox("Please do not modify the scale of the Playerlist's Transform, use the Window settings below instead.", MessageType.Error);
            }

            if (showWindowSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showWindowSettings, WindowFoldoutContent))
            {
                GUI.color = Color.black;

                using (EditorGUILayout.VerticalScope verticalScope = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUI.Box(verticalScope.rect, string.Empty);

                    GUI.color = Color.white;

                    using (new EditorGUI.IndentLevelScope(1))
                    {
                        using (EditorGUI.ChangeCheckScope changeScope = new EditorGUI.ChangeCheckScope())
                        {
                            windowWidth = EditorGUILayout.Slider("Width", windowWidth, 750f, 1500f);

                            windowHeight = EditorGUILayout.Slider("Height", windowHeight, 500f, 1500f);

                            windowScale = Mathf.Clamp(EditorGUILayout.FloatField("Scale", windowScale), 0.0001f, 10f);

                            if (changeScope.changed)
                            {
                                Undo.RecordObject(canvasRectTransform, "Adjust UdonConsole Window");

                                canvasRectTransform.sizeDelta = new Vector2(windowWidth, windowHeight);

                                canvasRectTransform.localScale = Vector3.one * windowScale;
                            }
                        }
                    }
                }
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }
}
