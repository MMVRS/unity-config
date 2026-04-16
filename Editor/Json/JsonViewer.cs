#if UNITY_EDITOR

using System;
using Build1.UnityEGUI;
using Build1.UnityEGUI.Components.Title;
using Build1.UnityEGUI.Json;
using Build1.UnityEGUI.Window;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;

namespace Build1.UnityConfig.Editor.Json
{
    internal sealed class JsonViewer : EGUIWindow
    {
        // For debugging.
        // Json = "{" +
        //        "\"support_email\":\"email@gmail.com\"," +
        //        "\"support_email_subject\":\"Support Request #{0}\"," +
        //        "\"support_email_body\":\"Hey developers,\n\n\"," +
        //        "\"privacy_policy_url\":\"https://google.com\"," +
        //        "\"terms_of_use_url\":\"https://google.com\"," +
        //        "\"collection\":[0,1,2,3,4,5,6,1,2,3,4,5,6,1,2,3,4,5,6,1,2,3,4,5,6,1,2,3,4,5,6,1,2,3,4,5,6,1,2,3,4,5,6,1,2,3,4,5,6,1,2,3,4,5,6,1,2,3,4,5,6,1,2,3,4,5,6,1,2,3,4,5,6,1,2,3,4,5,6]," +
        //        "\"multiple_children\":" +
        //        "{" +
        //        "\"name\":\"value\"," +
        //        "\"int\":10" +
        //        "}" +
        //        "}";

        private string _title;
        private string _json;

        [SerializeField] private TreeViewState _jsonTreeViewState;
        [NonSerialized]  private JsonTreeView  _jsonTree;

        /*
         * Public.
         */

        public JsonViewer Title(string title)
        {
            _title = title;
            return this;
        }

        public JsonViewer Json(string json)
        {
            _json = json;
            return this;
        }

        /*
         * Protected.
         */

        protected override void OnInitialize()
        {
            _jsonTreeViewState ??= new TreeViewState();
            _jsonTree ??= new JsonTreeView(_json, _jsonTreeViewState);
        }

        protected override void OnFocusLost()
        {
            Close();
        }

        protected override void OnEGUI()
        {
            var json = _json.Replace("\n", "");

            EGUI.Horizontally(() =>
            {
                EGUI.Title(_title ?? "Json Viewer", TitleType.H3);
                EGUI.Space();
                EGUI.Label("* click anywhere outside to close", EGUI.FontStyle(FontStyle.Italic));
            });
            EGUI.Space(5);

            var titleRect = EGUI.GetLastRect();

            EGUI.Space();

            EGUI.Horizontally(() =>
            {
                EGUI.Title("Json:", TitleType.H3, EGUI.OffsetX(5));
                EGUI.Space();
                EGUI.Label("Symbols: ", EGUI.FontStyle(FontStyle.Bold));
                EGUI.Label($"{json.Length}");
            });

            EGUI.Space(3);

            var jsonRect = EGUI.GetLastRect();

            EGUI.TextArea(_json, 150);
            EGUI.Space(1);
            EGUI.Horizontally(() =>
            {
                EGUI.Button("Expand All", EGUI.Size(120, EGUI.ButtonHeight01)).OnClick(_jsonTree.ExpandAll);
                EGUI.Button("Collapse All", EGUI.Size(120, EGUI.ButtonHeight01)).OnClick(_jsonTree.CollapseAll);
                EGUI.Space();
                EGUI.Button("Copy to Clipboard", EGUI.Size(200, EGUI.ButtonHeight01)).OnClick(EGUI.CopyToClipboard, _json);
                EGUI.Button("Close", EGUI.Size(100, EGUI.ButtonHeight01)).OnClick(Close);
            });

            const int x = 0;
            var y = titleRect.y + titleRect.height;
            var width = position.width - Padding * 2 - x;
            var height = jsonRect.y - titleRect.y - titleRect.height - Padding * 2;

            _jsonTree.OnEGUI(x, y, width, height);
        }

        /*
         * Static.
         */

        public static void Open(string title, string json)
        {
            EGUI.Window<JsonViewer>("Json Viewer", true)
                .Size(900, 950)
                .Get()
                .Title(title)
                .Json(json);
        }
    }
}

#endif
