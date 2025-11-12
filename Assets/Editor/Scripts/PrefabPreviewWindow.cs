#if UNITY_EDITOR
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
        private FloatSlider _volumeSlider;
        private VisualElement _prefabIcon;
        private Label _prefabName;
        private DropdownField _animClips;
        private TimeSlider _timeSlider;
        private FloatSlider _speedSlider;
        private ToolbarToggle _previewToggle;
        private Button _playButton;
        private StyleBackground _playImage;
        private StyleBackground _pauseImage;

        private PlayModeStateChange _playModeState;
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
        private bool _animationPreview;

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
                    SetAnimationPreview(_selectedClipIndex > 0);
                    ClearParticles();
                }
                else
                {
                    SetAnimationPreview(false);
                    ReloadPrefab();
                }

                ResetPlayback();
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
            }
        }

        public void CreateGUI()
        {
            _prefabContentsRoot = null;
            var visualTree = Resources.Load<VisualTreeAsset>("PrefabPreviewWindow");
            var root = visualTree.Instantiate();
            rootVisualElement.Add(root);
            _container = rootVisualElement.Q<VisualElement>("container");
            _prefabIcon = rootVisualElement.Q<VisualElement>("prefab_icon");
            _prefabName = rootVisualElement.Q<Label>("prefab_name");
            _previewToggle = rootVisualElement.Q<ToolbarToggle>("preview_toggle");
            _previewToggle.RegisterValueChangedCallback(OnPreviewChanged);
            _volumeSlider = rootVisualElement.Q<FloatSlider>("audio_volume");
            _volumeSlider.OnValueChanged += SetAudioVolume;
            _timeSlider = rootVisualElement.Q<TimeSlider>("playback_time");
            _timeSlider.OnValueChanged += f => { IsPlaying = false; };
            _timeSlider.OnMaxChanged += f => { _duration = f; };
            _speedSlider = rootVisualElement.Q<FloatSlider>("playback_speed");
            _animClips = rootVisualElement.Q<DropdownField>("clips");
            _animClips.RegisterValueChangedCallback(OnClipChanged);
            _playButton = rootVisualElement.Q<Button>("play_pause");
            _playButton.clicked += TogglePlay;

            var firstFrame = rootVisualElement.Q<Button>("first_frame");
            firstFrame.clicked += () => Seek(0f);
            var prevFrame = rootVisualElement.Q<Button>("prev_frame");
            prevFrame.clicked += () => Seek(_playbackTime - 1f / _frameRate);
            var nextButton = rootVisualElement.Q<Button>("next_frame");
            nextButton.clicked += () => Seek(_playbackTime + 1f / _frameRate);
            var lastFrame = rootVisualElement.Q<Button>("last_frame");
            lastFrame.clicked += () => Seek(_duration);

            rootVisualElement.SetEnabled(false);
            _timeSlider.Max = 1f;
            _playImage = new StyleBackground(Resources.Load<Texture2D>("Images/Play"));
            _pauseImage = new StyleBackground(Resources.Load<Texture2D>("Images/Pause"));
            OnPrefabStageChanged(null);
        }

        private void Seek(float time)
        {
            IsPlaying = false;
            SetPlaybackTime(time);
        }

        private void OnPreviewChanged(ChangeEvent<bool> evt)
        {
            IsPreviewing = evt.newValue;
            _container.SetEnabled(IsPreviewing);
        }

        private void ResetPlayback()
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
            ResetPlayback();
            SetAnimationPreview(false);
            _selectedClipIndex = Array.IndexOf(_clipNames, evt.newValue);
            if (_selectedClipIndex > 0 && _clips is { Length: > 0 })
            {
                _duration = _clips[_selectedClipIndex - 1].length;
                SetAnimationPreview(IsPreviewing);
            }
            else
            {
                _selectedClipIndex = 0;
                _duration = 1f;
            }

            _timeSlider.Max = _duration;
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
            _playModeState = stateChange;
            OnPrefabStageChanged(null);
        }

        private void OnPrefabStageClosed(PrefabStage prefabStage)
        {
            OnPrefabStageChanged(prefabStage);
        }

        private void OnPrefabStageChanged(PrefabStage _)
        {
            if (EditorApplication.isPlaying)
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
            if (EditorApplication.isPlaying || _speedSlider == null || !IsPreviewing) return;
            var playbackTime = _playbackTime;
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
            playbackTime = (float)Math.Clamp(playbackTime + deltaTime, 0f, _duration);
            if (playbackTime >= _duration)
            {
                IsPlaying = false;
                playbackTime = 0f;
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
                _prefabName.text = _playModeState == PlayModeStateChange.EnteredPlayMode
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

            ResetPlayback();
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
                _clips = controller.animationClips;
                if (_clips is { Length: > 0 })
                {
                    _clipNames = new string[_clips.Length + 1];
                    _clipNames[0] = "None";
                    for (var i = 0; i < _clips.Length; i++)
                    {
                        _clipNames[i + 1] = $"{_clips[i].name} ({_clips[i].length:0.00}Sec)";
                    }
                }
            }
            else
            {
                _frameRate = 60f;
            }

            _animClips.choices = _clipNames.ToList();
            if (_selectedClipIndex == 0 && _clipNames.Length > 1)
            {
                _selectedClipIndex = 1;
            }

            _animClips.index = _selectedClipIndex = Mathf.Clamp(_selectedClipIndex, 0, _clipNames.Length - 1);
            SetAudioVolume(_volumeSlider.Value);
        }

        private void UpdatePreview(float time, float deltaTime = 0f)
        {
            UpdateAnimator(time);
            UpdateParticles(time, deltaTime);
            UpdateAudioSources();
        }

        private void UpdateAnimator(float time)
        {
            if (_animator == null || _clips is not { Length: > 0 } || _selectedClipIndex == 0 ||
                !AnimationMode.InAnimationMode()) return;
            var clip = _clips[_selectedClipIndex - 1];
            if (clip == null) return;
            AnimationMode.SampleAnimationClip(_animator.gameObject, clip, time);
        }

        private void UpdateParticles(float time, float deltaTime)
        {
            if (_rootParticles is not { Count: > 0 }) return;
            foreach (var particle in _rootParticles)
            {
                if (particle == null) continue;
                if (!particle.gameObject.activeInHierarchy) continue;
                if (deltaTime != 0)
                {
                    particle.Simulate(deltaTime, withChildren: false, restart: false);
                }
                else
                {
                    particle.Simulate(time, withChildren: false, restart: true);
                }
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

        private void SetAudioVolume(float volume)
        {
            foreach (var audio in _audioSources)
            {
                if (audio == null || audio.clip == null) continue;
                audio.volume = volume;
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
                if (particle == null) continue;
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.useAutoRandomSeed = false;
            }
        }

        private void SetAnimationPreview(bool animationPreview)
        {
            if (_animationPreview == animationPreview) return;
            _animationPreview = animationPreview;
            if (_animationPreview)
            {
                AnimationMode.StartAnimationMode();
            }
            else
            {
                AnimationMode.StopAnimationMode();
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
#endif