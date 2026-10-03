#if UNITY_EDITOR
using MLG.SimpleSceneTool.CheckpointSystem;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Registers each recently-opened Scene Set as its own native menu entry under
/// Tools/SimpleScene/Recent Scene Sets - the same way Unity's own File > Open Recent Scene
/// works. Selecting an entry opens that Scene Set directly; no intermediate window or popup.
///
/// This relies on UnityEditor.Menu.AddMenuItem/RemoveMenuItem, which is how Unity itself
/// implements dynamic menus like its own recent-files list, but is an INTERNAL API reached here
/// via reflection - it is not part of the public, supported Editor scripting surface, and could
/// change or disappear in a future Unity version. Every call is wrapped so a failure degrades
/// silently (the submenu just won't populate, logged once) rather than breaking anything else -
/// SceneSetLoaderWindow's own embedded Recent section uses only public APIs and keeps working
/// regardless of whether this does.
/// </summary>
internal static class SceneSetRecentMenuIntegration
{
    private const string MenuRoot = "Tools/SimpleScene/Recent Scene Sets/";
    private const int BasePriority = 1000;

    private static readonly List<string> registeredPaths = new List<string>();
    private static bool reflectionAvailable = true;
    private static bool warnedOnce;

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        SceneSetEditorLoader.OnRecentChanged += Rebuild;
        Rebuild();
    }

    private static void Rebuild()
    {
        if (!reflectionAvailable)
        {
            return;
        }

        try
        {
            foreach (string path in registeredPaths)
            {
                RemoveMenuItemInternal(path);
            }
            registeredPaths.Clear();

            List<SceneSet> recent = SceneSetEditorLoader.GetRecent();
            if (recent.Count == 0)
            {
                AddItem("(no recent Scene Sets)", 0, () => { }, () => false);
                return;
            }

            int priority = 0;
            foreach (SceneSet sceneSet in recent)
            {
                if (sceneSet == null)
                {
                    continue;
                }
                SceneSet captured = sceneSet;
                AddItem(SanitizeForMenu(captured.DisplayName), priority, () => SceneSetEditorLoader.Open(captured), null);
                priority++;
            }

            AddItem("Clear Recent", priority + 100, SceneSetEditorLoader.ClearRecent, null);
        }
        catch (Exception exception)
        {
            reflectionAvailable = false;
            if (!warnedOnce)
            {
                warnedOnce = true;
                Debug.LogWarning(
                    "SimpleScene: couldn't build the native Recent Scene Sets menu on this Unity " +
                    "version (an internal API it relies on may have changed). The Scene Set " +
                    "Loader window's own Recent section is unaffected and still works normally.\n" +
                    exception);
            }
        }
    }

    private static void AddItem(string label, int priority, Action execute, Func<bool> validate)
    {
        string path = MenuRoot + label;
        AddMenuItemInternal(path, string.Empty, false, BasePriority + priority, execute, validate);
        registeredPaths.Add(path);
    }

    private static string SanitizeForMenu(string label)
    {
        // A literal "/" in the label would create an unwanted extra level of submenu nesting.
        return string.IsNullOrEmpty(label) ? "(unnamed)" : label.Replace("/", "-");
    }

    private static void AddMenuItemInternal(string name, string shortcut, bool isChecked, int priority, Action execute, Func<bool> validate)
    {
        typeof(Menu).GetMethod("AddMenuItem", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { name, shortcut, isChecked, priority, execute, validate });
    }

    private static void RemoveMenuItemInternal(string name)
    {
        typeof(Menu).GetMethod("RemoveMenuItem", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { name });
    }
}
#endif