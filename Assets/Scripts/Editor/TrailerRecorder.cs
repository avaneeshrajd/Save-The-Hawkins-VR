using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: Save The Hawkins > Record 1-Minute Trailer.
/// Opens Hawkin City, plays the TrailerDirector shot list and records
/// ~/Desktop/SaveTheHawkins_Trailer.mp4 (1920x1080, 30 fps, H.264, with game audio).
/// </summary>
[InitializeOnLoad]
public static class TrailerRecorder
{
    // If the flag file exists when scripts reload, record automatically (used for hands-off renders).
    const string RecordKey = "STH_TrailerRecord";

    // Statics are wiped by the domain reload on entering Play Mode, so state lives in
    // SessionState and the Play Mode listener is re-attached on every reload.
    static TrailerRecorder()
    {
        EditorApplication.playModeStateChanged -= OnPlayMode;
        EditorApplication.playModeStateChanged += OnPlayMode;
        if (EditorApplication.isPlaying && SessionState.GetBool(TrailerDirector.SessionKey, false))
            EditorApplication.delayCall += () => OnPlayMode(PlayModeStateChange.EnteredPlayMode);
        if (!System.IO.File.Exists(AutoFlag)) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !System.IO.File.Exists(AutoFlag)) return;
            System.IO.File.Delete(AutoFlag);
            Begin(true, true);
        };
    }

    const string ScenePath = "Assets/Scenes/Home.unity"; // trailer starts in the Home menu
    const string AutoFlag = "Library/STH_AutoRecordTrailer";
    const float Length = 61f;
    static RecorderController controller;
    static bool record { get => SessionState.GetBool(RecordKey, false); set => SessionState.SetBool(RecordKey, value); }
    // Saved to the user's Desktop: ~/Desktop/SaveTheHawkins_Trailer.mp4
    static string OutputBase => System.IO.Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory), "SaveTheHawkins_Trailer");

    [MenuItem("Save The Hawkins/Record 1-Minute Trailer")]
    static void RecordTrailer() => Begin(true, false);

    [MenuItem("Save The Hawkins/Preview Trailer (no recording)")]
    static void PreviewTrailer() => Begin(false, false);

    static void Begin(bool rec, bool auto)
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play Mode first."); return; }
        if (auto)
        {
            // Never discard the user's unsaved work in a hands-off run.
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError("Trailer auto-record skipped: save your open scene first, then use Save The Hawkins > Record 1-Minute Trailer.");
                    return;
                }
        }
        else if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureTag(TrailerDirector.CameraTag);
        EditorSceneManager.OpenScene(ScenePath);
        record = rec;
        SessionState.SetBool(TrailerDirector.SessionKey, true);
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(TrailerDirector.SessionKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode && record)
        {
            if (controller != null) return;
            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "Trailer";
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High
            };
            movie.ImageInputSettings = new CameraInputSettings
            {
                Source = ImageSource.TaggedCamera,
                CameraTag = TrailerDirector.CameraTag,
                OutputWidth = 1920,
                OutputHeight = 1080,
            };
            movie.AudioInputSettings.PreserveAudio = true;
            // OutputFile setter fails on absolute paths here, so record inside the project
            // (Recordings/) and copy to the Desktop when finished.
            movie.FileNameGenerator.Root = OutputPath.Root.Project;
            movie.FileNameGenerator.Leaf = "Recordings";
            movie.FileNameGenerator.FileName = "SaveTheHawkins_Trailer";

            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToTimeInterval(0f, Length);
            settings.FrameRatePlayback = FrameRatePlayback.Constant;
            settings.FrameRate = 30f;
            settings.CapFrameRate = true;

            controller = new RecorderController(settings);
            controller.PrepareRecording();
            controller.StartRecording();
            EditorApplication.update += WatchRecording;
            Debug.Log("Recording trailer... (61 s)");
        }
        else if (state == PlayModeStateChange.EnteredPlayMode && !record)
        {
            EditorApplication.update -= WatchPreview;
            EditorApplication.update += WatchPreview;
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            SessionState.SetBool(TrailerDirector.SessionKey, false);
            EditorApplication.update -= WatchRecording;
            EditorApplication.update -= WatchPreview;
            record = false;
            if (controller != null && controller.IsRecording()) controller.StopRecording();
            controller = null;
        }
    }

    static void WatchRecording()
    {
        if (controller != null && !controller.IsRecording())
        {
            EditorApplication.update -= WatchRecording;
            string src = System.IO.Path.GetFullPath("Recordings/SaveTheHawkins_Trailer.mp4");
            if (System.IO.File.Exists(src))
            {
                System.IO.File.Copy(src, OutputBase + ".mp4", true);
                Debug.Log("Trailer saved to: " + OutputBase + ".mp4");
            }
            else Debug.LogError("Trailer: recording file not found at " + src);
            EditorApplication.ExitPlaymode();
        }
    }

    static void WatchPreview()
    {
        if (Time.timeSinceLevelLoad > Length) EditorApplication.ExitPlaymode();
    }

    static void EnsureTag(string tag)
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tags = tagManager.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++)
            if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        tagManager.ApplyModifiedProperties();
    }
}
