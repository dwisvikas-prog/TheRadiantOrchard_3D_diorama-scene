using RadiantOrchard.Core;
using UnityEditor;
using UnityEngine;

namespace RadiantOrchard.Editor
{
    /// <summary>
    /// Editor shortcuts for the local save (ENGINEERING_GUIDE 9.2) until the in-game dev panel exists.
    /// Reset deletes save.json, save.bak and save.tmp, so the next Play starts from GameState.CreateNew.
    /// The dev time offset file is left alone.
    /// </summary>
    public static class DevSaveMenu
    {
        [MenuItem("Radiant Orchard/Dev/Reset Save")]
        public static void ResetSave()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                // The running game would write its state straight back on quit.
                Debug.LogError("DevSaveMenu: stop Play mode first; nothing was deleted.");
                return;
            }
            if (!EditorUtility.DisplayDialog("Reset save", "Delete the local save (save.json, save.bak) and start fresh next Play?", "Reset", "Cancel"))
                return;

            new LocalFileStore(Application.persistentDataPath).Delete();
            Debug.Log($"DevSaveMenu: save deleted in {Application.persistentDataPath}.");
        }

        [MenuItem("Radiant Orchard/Dev/Show Save Folder")]
        public static void ShowSaveFolder() => EditorUtility.RevealInFinder(Application.persistentDataPath + "/" + LocalFileStore.MainFileName);
    }
}
