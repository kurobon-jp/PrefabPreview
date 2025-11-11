using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrefabPreview
{
    public class PrefabPreviewWindow : EditorWindow
    {
        private VisualElement _container;
        private VisualElement _animatorContainer;
        private VisualElement _durationContainer;
        private VisualElement _prefabIcon;
        private Label _prefabName;
        private DropdownField _animClips;
        private FloatSlider _durationSlider;
        private TimeSlider _timeSlider;
        private FloatSlider _speedSlider;
        private ToolbarToggle _previewToggle;
        private Button _playButton;
        private StyleBackground _playImage;
        private StyleBackground _pauseImage;

        private bool _isPlaying;
        private float _duration;
        private float _frameRate;
        private float _playbackTime;
        private float _playbackSpeed;
        private double _lastEditorTime;
        private int _selectedClipIndex;

        private GameObject _prefabContentsRoot;
        private Animator _animator;
        private ParticleSystem[] _particles;
        private List<ParticleSystem> _rootParticles = new();
        private AudioSource[] _audioSources;
        private AnimationClip[] _clips;
        private string[] _clipNames;
        private string[] _stateNames;

        private static bool _isPreviewing;

        private bool IsPreviewing
        {
            get => _isPreviewing;
            set
            {
                if (_isPreviewing == value) return;
                _isPreviewing = value;
                if (_isPreviewing)
                {
                    EditorApplication.delayCall += AnimationMode.StartAnimationMode;
                    ClearParticles();
                }
                else
                {
                    EditorApplication.delayCall += AnimationMode.StopAnimationMode;
                    ReloadPrefab();
                }

                OnReset();
            }
        }

        private bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                _isPlaying = value;
                if (_playButton == null) return;
                _playButton.style.backgroundImage = _isPlaying ? _pauseImage : _playImage;
                if (!value || _playbackTime < _duration) return;
                _playbackTime = 0f;
                ClearParticles();
            }
        }

        public void CreateGUI()
        {
            _prefabContentsRoot = null;
            var visualTree = Resources.Load<VisualTreeAsset>("PrefabPreviewWindow");
            var ui = visualTree.Instantiate();
            rootVisualElement.Add(ui);
            _container = rootVisualElement.Q<VisualElement>("container");
            _animatorContainer = rootVisualElement.Q<VisualElement>("animator_container");
            _durationContainer = rootVisualElement.Q<VisualElement>("duration_container");
            _prefabIcon = rootVisualElement.Q<VisualElement>("prefab_icon");
            _prefabName = rootVisualElement.Q<Label>("prefab_name");
            _previewToggle = rootVisualElement.Q<ToolbarToggle>("preview_toggle");
            _previewToggle.RegisterValueChangedCallback(OnPreviewChanged);
            _timeSlider = rootVisualElement.Q<TimeSlider>("playback_time");
            _timeSlider.OnValueChanged += f => { IsPlaying = false; };
            _speedSlider = rootVisualElement.Q<FloatSlider>("playback_speed");
            _durationSlider = rootVisualElement.Q<FloatSlider>("playback_duration");
            _durationSlider.OnValueChanged += f =>
            {
                _duration = f;
                _timeSlider.Max = _duration;
            };
            _animClips = rootVisualElement.Q<DropdownField>("clips");
            _animClips.RegisterValueChangedCallback(OnClipChanged);
            _playButton = rootVisualElement.Q<Button>("play_pause");
            _playButton.clicked += TogglePlay;

            var firstFrame = rootVisualElement.Q<Button>("first_frame");
            firstFrame.clicked += () => Jump(0f);
            var prevFrame = rootVisualElement.Q<Button>("prev_frame");
            prevFrame.clicked += () => Jump(_playbackTime - 1f / _frameRate);
            var nextButton = rootVisualElement.Q<Button>("next_frame");
            nextButton.clicked += () => Jump(_playbackTime + 1f / _frameRate);
            var lastFrame = rootVisualElement.Q<Button>("last_frame");
            lastFrame.clicked += () => Jump(_duration);

            rootVisualElement.SetEnabled(false);
            _playImage = new StyleBackground(Resources.Load<Texture2D>("Images/Play"));
            _pauseImage = new StyleBackground(Resources.Load<Texture2D>("Images/Pause"));
            OnPrefabStageChanged(null);
        }

        private void Jump(float time)
        {
            IsPlaying = false;
            SetPlaybackTime(time);
        }

        private void OnPreviewChanged(ChangeEvent<bool> evt)
        {
            IsPreviewing = evt.newValue;
            _container.SetEnabled(IsPreviewing);
        }

        private void OnReset()
        {
            IsPlaying = false;
            SetPlaybackTime(0f);
        }

        private void SetPlaybackTime(float playbackTime)
        {
            _playbackTime = Mathf.Clamp(playbackTime, 0f, _duration);
            if (_timeSlider != null)
            {
                _timeSlider.Value = playbackTime;
            }
        }

        private void OnClipChanged(ChangeEvent<string> evt)
        {
            OnReset();
            _selectedClipIndex = Array.IndexOf(_clipNames, evt.newValue);
        }

        private void TogglePlay()
        {
            IsPlaying = !IsPlaying;
            _lastEditorTime = EditorApplication.timeSinceStartup;
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            PrefabStage.prefabStageOpened += OnPrefabStageChanged;
            PrefabStage.prefabStageClosing += OnPrefabStageClosed;
            ResetPreview();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            PrefabStage.prefabStageOpened -= OnPrefabStageChanged;
            PrefabStage.prefabStageClosing -= OnPrefabStageClosed;
            ResetPreview();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange stateChange)
        {
            IsPreviewing = false;
            OnPrefabStageChanged(null);
        }

        private void OnPrefabStageClosed(PrefabStage prefabStage)
        {
            OnPrefabStageChanged(prefabStage);
        }

        private void OnPrefabStageChanged(PrefabStage _)
        {
            if (Application.isPlaying)
            {
                _prefabContentsRoot = null;
                rootVisualElement.SetEnabled(false);
                ResetPreview();
                return;
            }

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (_prefabContentsRoot == prefabStage?.prefabContentsRoot) return;
            _prefabContentsRoot = prefabStage?.prefabContentsRoot;
            rootVisualElement.SetEnabled(_prefabContentsRoot != null);
            ResetPreview();
            SetupPrefab();
        }

        private void OnEditorUpdate()
        {
            if (Application.isPlaying || _speedSlider == null || !IsPreviewing) return;
            float playbackTime;
            if (!IsPlaying)
            {
                playbackTime = _timeSlider.Value;
                SetPlaybackTime(playbackTime);
                UpdatePreview(playbackTime);
                return;
            }

            var timeSinceStartup = EditorApplication.timeSinceStartup;
            var deltaTime = (timeSinceStartup - _lastEditorTime) * _speedSlider.Value;
            _lastEditorTime = timeSinceStartup;
            playbackTime = _playbackTime + Mathf.Clamp((float)deltaTime, 0, _duration);
            if (playbackTime >= _duration)
            {
                IsPlaying = false;
            }

            SetPlaybackTime(playbackTime);
            UpdatePreview(playbackTime, (float)deltaTime);
            SceneView.RepaintAll();
        }

        private void ResetPreview()
        {
            if (_prefabIcon != null)
            {
                _prefabIcon.style.backgroundImage = null;
            }

            if (_prefabName != null)
            {
                _prefabName.text = Application.isPlaying
                    ? "Preview disabled during editor playback."
                    : "Enter prefab edit mode.";
            }

            if (_animClips != null)
            {
                _animClips.choices = new List<string>();
                _animClips.index = 0;
            }

            if (_previewToggle != null)
            {
                _previewToggle.value = false;
            }

            if (_timeSlider != null)
            {
                _timeSlider.Value = 0;
            }

            if (_speedSlider != null)
            {
                _speedSlider.Value = 1f;
            }

            OnReset();
            IsPreviewing = false;
        }

        private void SetupPrefab()
        {
            if (_prefabContentsRoot == null || _prefabIcon == null) return;
            var root = _prefabContentsRoot;
            _prefabIcon.style.backgroundImage = new StyleBackground(AssetPreview.GetMiniThumbnail(root));
            _prefabName.text = root.name;
            _animator = root.GetComponentInChildren<Animator>();
            _particles = root.GetComponentsInChildren<ParticleSystem>(true);

            var subParticles = _particles.SelectMany(x =>
                    Enumerable.Range(0, x.subEmitters.subEmittersCount)
                        .Select(i => x.subEmitters.GetSubEmitterSystem(i)))
                .ToList();
            _rootParticles = _particles.Where(x => !subParticles.Contains(x)).ToList();

            _audioSources = root.GetComponentsInChildren<AudioSource>(true);
            _clips = null;
            _clipNames = Array.Empty<string>();
            if (_animator != null && _animator.runtimeAnimatorController is AnimatorController controller)
            {
                var states = controller.layers[0].stateMachine.states;
                _stateNames = states.Select(x => x.state.name).ToArray();
                _clips = _animator.runtimeAnimatorController.animationClips;
                if (_clips is { Length: > 0 })
                {
                    _clipNames = new string[_clips.Length];
                    for (var i = 0; i < _clips.Length; i++)
                    {
                        _clipNames[i] = $"{_clips[i].name} ({_clips[i].length:0.00}Sec)";
                    }
                }

                _animatorContainer.style.display = DisplayStyle.Flex;
                _durationContainer.style.display = DisplayStyle.None;
            }
            else
            {
                _animatorContainer.style.display = DisplayStyle.None;
                _durationContainer.style.display = DisplayStyle.Flex;
                _timeSlider.Max = _durationSlider.Value = _duration = Mathf.Max(_duration, 1f);
                _frameRate = 60f;
            }

            _animClips.choices = _clipNames.ToList();
            _animClips.index = _selectedClipIndex = Mathf.Clamp(_selectedClipIndex, 0, _clipNames.Length - 1);
        }

        private void UpdatePreview(float time, float deltaTime = 0f)
        {
            UpdateAnimator(time);
            UpdateParticles(time, deltaTime);
            UpdateAudioSources();
        }

        private void UpdateAnimator(float time)
        {
            if (_animator == null || _clips is not { Length: > 0 } || _stateNames is not { Length: > 0 } ||
                !AnimationMode.InAnimationMode()) return;
            var clip = _clips[_selectedClipIndex];
            if (clip == null) return;
            _duration = clip.length;
            _timeSlider.Max = _duration;
            _frameRate = clip.frameRate;
            AnimationMode.SampleAnimationClip(_animator.gameObject, clip, time);
        }

        private void UpdateParticles(float time, float deltaTime)
        {
            if (_rootParticles is not { Count: > 0 }) return;
            foreach (var ps in _rootParticles)
            {
                if (ps == null) continue;
                if (!ps.gameObject.activeInHierarchy) continue;
                if (deltaTime != 0)
                {
                    time = deltaTime;
                }
                else
                {
                    ps.Simulate(0f, withChildren: true, restart: true);
                    ps.Play(true);
                }

                ps.Simulate(time, withChildren: true, restart: false);
            }
        }

        private void UpdateAudioSources()
        {
            if (_audioSources is not { Length: > 0 } || !_isPlaying) return;
            foreach (var audio in _audioSources)
            {
                if (audio == null || audio.clip == null || !audio.enabled ||
                    !audio.gameObject.activeInHierarchy) continue;
                if (!audio.isPlaying)
                {
                    audio.Play();
                }
            }
        }

        private void ClearParticles()
        {
            if (_rootParticles.Count > 0)
            {
                SetLockedParticleSystem(_rootParticles[0]);
            }

            foreach (var particle in _particles)
            {
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.useAutoRandomSeed = false;
                particle.Play(false);
            }
        }

        private static void SetLockedParticleSystem(ParticleSystem particle)
        {
            var particleSystemEditorUtils =
                typeof(Editor).Assembly.GetType("UnityEditor.ParticleSystemEditorUtils");
            var lockedParticleSystem = particleSystemEditorUtils?.GetProperty("lockedParticleSystem",
                BindingFlags.Static | BindingFlags.NonPublic);
            lockedParticleSystem?.SetValue(null, particle);
        }

        private static void ReloadPrefab()
        {
            SetLockedParticleSystem(null);
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null) return;
            var method = stage.GetType().GetMethod("ReloadStage", BindingFlags.NonPublic | BindingFlags.Instance);
            method?.Invoke(stage, null);
        }

        [MenuItem("Window/Prefab Preview")]
        private static void ShowWindow()
        {
            GetWindow<PrefabPreviewWindow>("Prefab Preview", true);
        }

        private class PrefabModificationProcessor : AssetModificationProcessor
        {
            static string[] OnWillSaveAssets(string[] paths)
            {
                if (!_isPreviewing || paths.Length == 0) return paths;
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage == null) return paths;
                foreach (var path in paths)
                {
                    if (path != prefabStage.assetPath) continue;
                    Debug.LogError("You cannot save during preview.");
                    return Array.Empty<string>();
                }

                return paths;
            }
        }
    }
}