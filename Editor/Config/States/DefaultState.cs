#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using Build1.UnityEGUI;
using Build1.UnityEGUI.Components.Label;
using Build1.UnityEGUI.Components.Title;
using UnityEditor;
using UnityEngine;

namespace Build1.UnityConfig.Editor.Config.States
{
    internal sealed class DefaultState : ConfigEditorState
    {
        private static Vector2 _scrollPosition      = new(0, 1);
        private static int     _selectedConfigIndex = -1;

        private static string _configName          = string.Empty;
        private static int    _configCopyFromIndex;
        private static bool   _configNameInvalid;
        private static bool   _configAlreadyExists;

        public DefaultState(ConfigEditorModel model) : base(model)
        {
        }

        public override void Draw()
        {
            var configs = model.Configs;
            var settings = model.Settings;
            
            var currentConfigSourceIndex = configs.IndexOf(model.Settings.Source);

            EGUI.Scroll(ref _scrollPosition, () =>
            {
                EGUI.Title("Source", TitleType.H3, EGUI.OffsetX(5));
                EGUI.Label("Selected config (except Firebase) will be included and used in the build.");
                EGUI.Space(3);

                EGUI.DropDown(configs, currentConfigSourceIndex, EGUI.DropDownHeight01, newIndex =>
                {
                    settings.SetSource(configs[newIndex]);
                });
                EGUI.Space(18);

                EGUI.Checkbox("Reset Source to Firebase when building", settings.ResetSourceForPlatformBuilds, resetSelected =>
                {
                    settings.SetResetForPlatformBuilds(resetSelected);
                });

                EGUI.Space(5);

                var fallbackAvailable = settings.Source != ConfigSettings.SourceFirebase || settings.ResetSourceForPlatformBuilds; 
                EGUI.Enabled(fallbackAvailable && !Application.isPlaying && !model.InProgress, () =>
                {
                    EGUI.Checkbox("Fallback enabled", settings.FallbackEnabled, "Fallback is the version of the config included in the build and will be used if config loading failed or loading time exceeds the timeout.", value =>
                    {
                        settings.SetFallbackEnabled(value);
                    });
                });

                if (!fallbackAvailable)
                {
                    EGUI.Space(5);
                    EGUI.MessageBox("Config fallback unavailable for embed configs sources.", MessageType.Info);    
                }
                
                if (settings.FallbackEnabled)
                {
                    EGUI.Space(5);
                    EGUI.Checkbox("Cache enabled", settings.CacheEnabled, "When config is loaded from remote it's saved locally for further usage instead of the fallback version.", value =>
                    {
                        settings.SetCacheEnabled(value);
                    });

                    EGUI.Space(5);
                    
                    EGUI.Enabled(settings.CacheEnabled, () =>
                    {
                        EGUI.Checkbox("Fast loading", settings.FastLoadingEnabled, "Config loads fast by using the last saved locally config or the fallback one. Remote config loads in background and saved once it's loaded.", value =>
                        {
                            settings.SetFastLoadingEnabled(value);
                        });

                        if (settings.FastLoadingEnabled)
                        {
                            EGUI.Space(10);
                            EGUI.MessageBox("Fast loading will affect Remote Config A/B tests as the previous (or the fallback) version of the config will be served. Users will get the fallback config in the first session and relevant config in the second session.", MessageType.Warning);
                        }
                    });
                    
                    EGUI.Space(10);
                    
                    var fallbackConfigs = configs.Where(c => c != ConfigSettings.SourceFirebase).ToList();
                    var fallbackConfigSourceIndex = fallbackConfigs.IndexOf(model.Settings.FallbackSource);
                    if (fallbackConfigSourceIndex == -1)
                    {
                        fallbackConfigSourceIndex = 0;
                        settings.SetFallbackSource(fallbackConfigs[fallbackConfigSourceIndex]);
                    }
                    
                    EGUI.Label("Fallback Source", EGUI.FontStyle(FontStyle.Bold));
                    EGUI.DropDown(fallbackConfigs, fallbackConfigSourceIndex, EGUI.DropDownHeight01, newIndex =>
                    {
                        settings.SetFallbackSource(fallbackConfigs[newIndex]);
                    });
                    
                    if (!settings.CacheEnabled || !settings.FastLoadingEnabled)
                    {
                        EGUI.Space(20);
                        EGUI.Label("Fallback Timeout Ms", EGUI.FontStyle(FontStyle.Bold));
                        EGUI.Int(settings.FallbackTimeout, EGUI.Height(EGUI.ButtonHeight02), EGUI.TextAnchor(TextAnchor.MiddleLeft)).OnChange(timeout => { settings.SetFallbackTimeout(timeout); });
                    }
                    else
                    {
                        EGUI.Space(10);        
                    }
                }
                
                EGUI.Space(20);
                
                EGUI.Title("Mode", TitleType.H3, EGUI.OffsetX(5));
                EGUI.Label("The mode configures parsing and editor tools behavior.");

                var modes = new List<string> { ConfigMode.Default.ToString(), ConfigMode.Decomposed.ToString() };
                var index = modes.LastIndexOf(settings.Mode.ToString());
                EGUI.DropDown(modes, index, EGUI.DropDownHeight01, newIndex =>
                {
                    settings.SetMode((ConfigMode)Enum.Parse(typeof(ConfigMode), modes[newIndex], true));
                });
                
                EGUI.Space(15);
                
                switch (settings.Mode)
                {
                    case ConfigMode.Default:
                        EGUI.MessageBox("The Default mode uses a single Firebase Remote Config parameter for config reading and parsing.", MessageType.Info);
                        EGUI.Space(9);
                        EGUI.Label("Parameter Name", EGUI.FontStyle(FontStyle.Bold));
                        EGUI.Label("Name of the parameter that must be used as source for config.");
                        EGUI.TextField(settings.ParameterName, EGUI.ButtonHeight02, TextAnchor.MiddleLeft, valueNew =>
                        {
                            settings.SetParameterName(valueNew);
                        });
                        break;
                    
                    case ConfigMode.Decomposed:
                        EGUI.MessageBox("In Decomposed mode config is constructed from many fields using json properties naming for linking.", MessageType.Info);
                        break;
                    
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                
                EGUI.Space(20);
                
                EGUI.Title("A/B Testing", TitleType.H3, EGUI.OffsetX(5));
                EGUI.Label("A/B tests related settings.");

                var baselineSources = configs.Where(c => c != ConfigSettings.SourceFirebase).ToList();
                var currentBaselineSourceIndex = baselineSources.IndexOf(model.Settings.BaselineSource);
                
                EGUI.DropDown(baselineSources, currentBaselineSourceIndex, EGUI.DropDownHeight01, newIndex =>
                {
                    settings.SetBaselineSource(baselineSources[newIndex]);
                });
                
                model.TrySaveSettings();

                EGUI.Space(30);

                configs = configs.Concat(new[] { "New..." }).ToList();

                EGUI.Title("Configs", TitleType.H3, EGUI.OffsetX(5));
                EGUI.Space(5);

                EGUI.Label("Select config to view / edit.");
                EGUI.Space(5);

                EGUI.MessageBox("Firebase config can't be edited from Unity Editor but can be used for another config creation.", MessageType.Info);

                EGUI.SelectionGrid(configs, ref _selectedConfigIndex, 160, 3, 10);

                if (_selectedConfigIndex == configs.Count - 1)
                {
                    OnAddConfig();
                    return;
                }

                if (_selectedConfigIndex <= -1)
                    return;

                var configName = configs[_selectedConfigIndex];
                model.SelectConfig(configName);

                _selectedConfigIndex = -1;
            });
        }

        public override void Reset() { }

        private void OnAddConfig()
        {
            EGUI.Space(30);

            EGUI.Title("New Config", TitleType.H3, EGUI.OffsetX(5));
            EGUI.Space(5);

            var add = false;
            var controlName = string.Empty;
            var configs = model.Configs.Concat(new[] { "None" }).ToList();

            EGUI.Horizontally(() =>
            {
                EGUI.Label("Name:", EGUI.Size(80, EGUI.DropDownHeight02), EGUI.TextAnchor(TextAnchor.MiddleLeft));
                EGUI.TextField(_configName, EGUI.DropDownHeight02, TextAnchor.MiddleLeft, out controlName, configName =>
                {
                    _configName = configName;
                    _configNameInvalid = false;
                    _configAlreadyExists = false;
                });
            });

            EGUI.Horizontally(() =>
            {
                EGUI.Label("Copy from:", EGUI.Size(80, EGUI.DropDownHeight02), EGUI.TextAnchor(TextAnchor.MiddleLeft));
                EGUI.DropDown(configs, ref _configCopyFromIndex, EGUI.DropDownHeight02);
            });

            string configSourceName = null;
            if (_configCopyFromIndex < configs.Count - 1)
                configSourceName = configs[_configCopyFromIndex];

            EGUI.Focus(controlName);

            EGUI.Horizontally(() =>
            {
                if (_configNameInvalid)
                {
                    EGUI.Space(8);

                    EGUI.Label("Invalid name. Can't be empty of whitespace. No special symbols or spaces allowed.",
                               LabelType.Error,
                               EGUI.Height(EGUI.ButtonHeight01), EGUI.TextAnchor(TextAnchor.MiddleCenter), EGUI.StretchedWidth());

                    EGUI.Space(8);
                }
                else if (_configAlreadyExists)
                {
                    EGUI.Space(8);

                    EGUI.Label($"Config with name '{_configName}' already exist.",
                               LabelType.Error,
                               EGUI.Height(EGUI.ButtonHeight01), EGUI.TextAnchor(TextAnchor.MiddleCenter), EGUI.StretchedWidth());

                    EGUI.Space(8);
                }
                else
                {
                    EGUI.Space();
                }

                EGUI.Button("Create", EGUI.Size(120, EGUI.ButtonHeight01)).Clicked(out add);
            });

            if (!add)
                return;

            _configNameInvalid = !model.CheckConfigNameValid(_configName);
            if (_configNameInvalid)
                return;

            _configAlreadyExists = model.CheckConfigExists(_configName);
            if (_configAlreadyExists)
                return;

            model.AddConfig(_configName, configSourceName);

            _configName = string.Empty;
            _selectedConfigIndex = -1;
            _configCopyFromIndex = 0;
        }
    }
}

#endif