#if UNITY_EDITOR

using System.Globalization;
using Build1.UnityConfig.Editor.Config;
using Build1.UnityEGUI;
using Build1.UnityEGUI.Window;
using UnityEditor;
using UnityEngine;

namespace Build1.UnityConfig.Editor.Metadata
{
    public sealed class MetadataWindow : EGUIWindow
    {
        private ConfigNodeMetadata _data;
        private ConfigEditorModel  _model;

        /*
         * Protected.
         */
        
        protected override void OnInitialize()
        {
            if (_data == null || _model == null)
                Close();
        }
        
        protected override void OnFocusLost()
        {
            Close();
        }
        
        protected override void OnEGUI()
        {
            var dto = _data;

            EGUI.Enabled(false, () =>
            {
                EGUI.Horizontally(() =>
                {
                    EGUI.Property(dto, dto.AppVersion, nameof(dto.AppVersion));
                    
                    EGUI.Enabled(dto.AppVersion != Application.version, () =>
                    {
                        EGUI.Button("Update").OnClick(dto.Update);
                    });
                });
            });
            
            EGUI.Property(dto, dto.Note, nameof(dto.Note));

            EGUI.Enabled(false, () =>
            {
                EGUI.Property(dto, dto.LastChangedBy, nameof(dto.LastChangedBy));

                EGUI.Horizontally(() =>
                {
                    EGUI.Label("Last Changed On:", EGUI.Width(EGUI.PropertyLabelWidth));
                    EGUI.TextField(dto.LastChangeDate.ToString(CultureInfo.InvariantCulture));
                });
            });

            EGUI.Space(20);

            EGUI.Label("A/B Testing", EGUI.FontStyle(FontStyle.Bold));
            EGUI.Property(dto, dto.AbTestEnabled, nameof(dto.AbTestEnabled));
            EGUI.Enabled(dto.AbTestEnabled, () =>
            {
                EGUI.Property(dto, dto.AbTestName, nameof(dto.AbTestName));
                EGUI.Property(dto, dto.AbTestGroup, nameof(dto.AbTestGroup));
            });
        }

        /*
         * Private.
         */

        private MetadataWindow SetData(ConfigNodeMetadata data, ConfigEditorModel model)
        {
            _data = data;
            _model = model;
            return this;
        }

        /*
         * Static.
         */

        internal static void Open(ConfigNode node, ConfigEditorModel model)
        {
            if (node.Metadata == null)
                node.UpdateMetadata();
            
            EGUI.Window<MetadataWindow>("Metadata", true)
                .Size(640, 480)
                .Get()
                .SetData(node.Metadata, model);
        }
    }
}

#endif