#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class MusePSonaMenu
{
    const string RelExport = "../../../导出";
    const string PrefAuto = "MusePSona.AutoImportOnPlay";
    const string AutoMenu = "MusePSona/进入 Play 时自动导入最新 MDM";

    static MusePSonaMenu()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    static bool AutoImport
    {
        get => EditorPrefs.GetBool(PrefAuto, true);
        set => EditorPrefs.SetBool(PrefAuto, value);
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
            EditorApplication.delayCall += ForceGameViewDisplay1;
        if (!AutoImport) return;
        if (state == PlayModeStateChange.ExitingEditMode)
            SyncNow();
        else if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += SyncNow;
    }

    static void SyncNow()
    {
        if (TryCopyLatest(silent: true, out var src, out var dest, out _))
            Debug.Log("MusePSona 已同步谱面:\n" + dest + "\n来源:\n" + src);
    }

    static string ExportDir()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, RelExport));
    }

    static string SongDest()
    {
        var destDir = Path.Combine(Application.dataPath, "StreamingAssets", "Charts");
        Directory.CreateDirectory(destDir);
        return Path.Combine(destDir, "song.mdm");
    }

    static string PendingDest() => SongDest() + ".pending";

    static IEnumerable<string> ExportSearchDirs()
    {
        yield return ExportDir();
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MuseDashChart", "export");
    }

    static string FindNewestMdm()
    {
        string newest = null;
        var newestWrite = DateTime.MinValue;
        foreach (var export in ExportSearchDirs())
        {
            if (!Directory.Exists(export)) continue;
            foreach (var f in Directory.GetFiles(export, "*.mdm", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(f);
                if (name.StartsWith("pack-", StringComparison.OrdinalIgnoreCase))
                    continue;
                var w = File.GetLastWriteTimeUtc(f);
                if (w > newestWrite)
                {
                    newestWrite = w;
                    newest = f;
                }
            }
        }
        return newest;
    }

    static bool TryReplace(string src, string dest)
    {
        for (var i = 0; i < 6; i++)
        {
            try
            {
                File.Copy(src, dest, true);
                return true;
            }
            catch
            {
                System.Threading.Thread.Sleep(50 * (i + 1));
            }
        }

        File.Copy(src, PendingDest(), true);
        return false;
    }

    static bool TryCopyLatest(bool silent, out string src, out string dest, out bool skipped)
    {
        skipped = false;
        dest = SongDest();
        var pending = PendingDest();
        src = File.Exists(pending) ? pending : FindNewestMdm();
        if (src == null)
        {
            if (!silent)
                EditorUtility.DisplayDialog("MusePSona", "还没有 .mdm。先在编辑器里导出。", "OK");
            return false;
        }

        try
        {
            if (!TryReplace(src, dest))
            {
                if (!silent)
                    EditorUtility.DisplayDialog("MusePSona",
                        "song.mdm 仍被占用，已排队。停掉 Play 后会自动导入。\n" + dest, "OK");
                else
                    Debug.LogWarning("MusePSona song.mdm 占用中，已写入 pending，停 Play 后同步。");
                return false;
            }

            if (File.Exists(pending)
                && string.Equals(src, pending, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(pending); }
                catch { /* next sync retries */ }
            }

            if (!silent)
                AssetDatabase.Refresh();
            return true;
        }
        catch (Exception ex)
        {
            if (!silent)
                EditorUtility.DisplayDialog("MusePSona", "无法写入 song.mdm：\n" + dest + "\n" + ex.Message, "OK");
            else
                Debug.LogWarning("MusePSona 未能同步 song.mdm: " + ex.Message);
            return false;
        }
    }

    static void CopyToPlay(string src)
    {
        var dest = SongDest();
        if (!TryReplace(src, dest))
        {
            EditorUtility.DisplayDialog("MusePSona",
                "song.mdm 被占用，已排队。停掉 Play 后会自动导入。\n来源:\n" + src, "OK");
            return;
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("MusePSona", "已导入:\n" + dest + "\n来源:\n" + src, "OK");
    }

    [MenuItem("MusePSona/从导出复制最新 MDM")]
    static void CopyLatest()
    {
        if (!TryCopyLatest(silent: false, out var src, out var dest, out _))
            return;
        EditorUtility.DisplayDialog("MusePSona", "已导入:\n" + dest + "\n来源:\n" + src, "OK");
    }

    [MenuItem(AutoMenu)]
    static void ToggleAuto()
    {
        AutoImport = !AutoImport;
    }

    [MenuItem(AutoMenu, true)]
    static bool ToggleAutoValidate()
    {
        Menu.SetChecked(AutoMenu, AutoImport);
        return true;
    }

    [MenuItem("MusePSona/从导出选择 MDM")]
    static void CopyPicked()
    {
        var export = ExportDir();
        if (!Directory.Exists(export)) Directory.CreateDirectory(export);
        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MuseDashChart", "export");
        var start = Directory.Exists(local) ? local : export;
        var picked = EditorUtility.OpenFilePanel("选择要导入的 MDM", start, "mdm");
        if (string.IsNullOrEmpty(picked)) return;
        CopyToPlay(picked);
    }

    [MenuItem("MusePSona/难度/map1")]
    static void Diff1() => PlayDirector.RequestMap(1);

    [MenuItem("MusePSona/难度/map2")]
    static void Diff2() => PlayDirector.RequestMap(2);

    [MenuItem("MusePSona/难度/map3")]
    static void Diff3() => PlayDirector.RequestMap(3);

    [MenuItem("MusePSona/难度/map4")]
    static void Diff4() => PlayDirector.RequestMap(4);

    [MenuItem("MusePSona/难度/map5")]
    static void Diff5() => PlayDirector.RequestMap(5);

    [MenuItem("MusePSona/难度/下一张")]
    static void DiffNext() => PlayDirector.RequestCycle(1);

    [MenuItem("MusePSona/难度/上一张")]
    static void DiffPrev() => PlayDirector.RequestCycle(-1);

    [MenuItem("MusePSona/修复 Play 场景脚本")]
    static void MenuRepair() => RepairPlayDirector(stripMissing: true);

    static void RepairPlayDirector() => RepairPlayDirector(stripMissing: false);

    static void RepairPlayDirector(bool stripMissing)
    {
        if (EditorApplication.isPlaying) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return;
        var dirty = 0;
        if (stripMissing)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                {
                    var n = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(tr.gameObject);
                    if (n > 0) dirty += n;
                }
            }
        }
        // 场景里不要序列化 PlayDirector：域重载时脚本尚未绑上会打 Unknown。
        // 进 Play 由 PlayDirector.AutoMount 挂组件。
        var go = GameObject.Find("PlayDirector");
        if (go == null)
        {
            go = new GameObject("PlayDirector");
            dirty++;
        }
        else
        {
            var existing = go.GetComponents<MonoBehaviour>();
            for (var i = existing.Length - 1; i >= 0; i--)
            {
                if (existing[i] is PlayDirector)
                {
                    UnityEngine.Object.DestroyImmediate(existing[i]);
                    dirty++;
                }
            }
        }
        if (dirty > 0)
            EditorSceneManager.MarkSceneDirty(scene);
    }

    [MenuItem("MusePSona/Game 视图切到 Display 1")]
    static void MenuForceDisplay() => ForceGameViewDisplay1();

    static void ForceGameViewDisplay1()
    {
        var t = Type.GetType("UnityEditor.GameView,UnityEditor");
        if (t == null) return;
        var flags = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic;
        foreach (var w in Resources.FindObjectsOfTypeAll(t))
        {
            var set = t.GetMethod("SetTargetDisplay", flags);
            if (set != null)
            {
                set.Invoke(w, new object[] { 0 });
                continue;
            }
            var f = t.GetField("m_TargetDisplay", flags);
            if (f != null) f.SetValue(w, 0);
        }
    }

    [MenuItem("MusePSona/打开导出目录")]
    static void OpenExport()
    {
        var p = ExportDir();
        if (!Directory.Exists(p)) Directory.CreateDirectory(p);
        EditorUtility.RevealInFinder(p);
    }

    [MenuItem("MusePSona/打开自制谱目录")]
    static void OpenCharts()
    {
        var p = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../自制谱"));
        if (Directory.Exists(p)) EditorUtility.RevealInFinder(p);
    }

    [MenuItem("MusePSona/删除当前 song.mdm")]
    static void DeleteSong()
    {
        var dest = SongDest();
        if (!File.Exists(dest))
        {
            EditorUtility.DisplayDialog("MusePSona", "没有 song.mdm", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("MusePSona", "删除 StreamingAssets/Charts/song.mdm？", "删除", "取消"))
            return;
        File.Delete(dest);
        var meta = dest + ".meta";
        if (File.Exists(meta)) File.Delete(meta);
        var pending = PendingDest();
        if (File.Exists(pending)) File.Delete(pending);
        AssetDatabase.Refresh();
    }
}
#endif
