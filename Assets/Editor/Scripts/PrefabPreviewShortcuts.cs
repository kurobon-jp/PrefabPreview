using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace PrefabPreview
{
    static class PrefabPreviewShortcuts
    {
        [Shortcut("Prefab Preview/Play/Pause")]
        private static void PlayPause()
        {
            var windows = Resources.FindObjectsOfTypeAll<PrefabPreviewWindow>();
            foreach (var window in windows)
            {
                window.TogglePlay();
            }
        }

        [Shortcut("Prefab Preview/First")]
        private static void First()
        {
            var windows = Resources.FindObjectsOfTypeAll<PrefabPreviewWindow>();
            foreach (var window in windows)
            {
                window.First();
            }
        }

        [Shortcut("Prefab Preview/Prev")]
        private static void Prev()
        {
            var windows = Resources.FindObjectsOfTypeAll<PrefabPreviewWindow>();
            foreach (var window in windows)
            {
                window.Prev();
            }
        }

        [Shortcut("Prefab Preview/Next")]
        private static void Next()
        {
            var windows = Resources.FindObjectsOfTypeAll<PrefabPreviewWindow>();
            foreach (var window in windows)
            {
                window.Next();
            }
        }

        [Shortcut("Prefab Preview/Last")]
        private static void Last()
        {
            var windows = Resources.FindObjectsOfTypeAll<PrefabPreviewWindow>();
            foreach (var window in windows)
            {
                window.Last();
            }
        }
    }
}