using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace StealthCastle.Isometric3D.EditorTools
{
    /// <summary>
    /// Запись игрового процесса в Play mode через Unity Recorder: MP4 и, по желанию, PNG-кадры.
    /// Источник — главная камера (рендер в текстуру), поэтому запись работает и когда редактор не в фокусе.
    /// HUD (Screen Space Overlay) в запись не попадает: в URP захват UI с камеры не поддерживается.
    /// Файлы — в папке Recordings/ в корне проекта (вне Assets).
    /// Меню: StealthCastle → 3D → Start / Stop Recording
    /// </summary>
    public static class GameplayRecorder3D
    {
        private const string OutputFolder = "Recordings";

        private static RecorderController controller;

        public static bool IsRecording => controller != null && controller.IsRecording();

        [MenuItem("StealthCastle/3D/Start Recording")]
        public static void StartFromMenu() => Start("gameplay", pngFrames: false);

        [MenuItem("StealthCastle/3D/Stop Recording")]
        public static void StopFromMenu() => Stop();

        /// <param name="takeName">Имя дубля: Recordings/{takeName}.mp4 и Recordings/{takeName}_frames/</param>
        /// <param name="pngFrames">Дополнительно писать каждый кадр в PNG (для покадрового разбора)</param>
        public static string Start(string takeName, bool pngFrames, int width = 1280, int height = 720, float frameRate = 60f)
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[GameplayRecorder3D] Recording works only in Play mode");
                return null;
            }
            Stop();

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputFolder));
            Directory.CreateDirectory(root);

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.SetRecordModeToManual();
            settings.FrameRatePlayback = FrameRatePlayback.Constant;
            settings.FrameRate = frameRate;
            settings.CapFrameRate = true;

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = takeName;
            movie.Enabled = true;
            movie.ImageInputSettings = MainCameraInput(width, height);
            movie.OutputFile = Path.Combine(root, takeName);
            settings.AddRecorderSettings(movie);

            if (pngFrames)
            {
                var frames = ScriptableObject.CreateInstance<ImageRecorderSettings>();
                frames.name = takeName + "_frames";
                frames.Enabled = true;
                frames.OutputFormat = ImageRecorderSettings.ImageRecorderOutputFormat.PNG;
                frames.imageInputSettings = MainCameraInput(width, height); // Recorder требует одно разрешение у всех дублей
                // Path.Combine в Mono отвергает «<Frame>» из шаблона Recorder — склеиваем вручную
                frames.OutputFile = $"{root}/{takeName}_frames/frame_{DefaultWildcard.Frame}";
                settings.AddRecorderSettings(frames);
            }

            controller = new RecorderController(settings);
            controller.PrepareRecording();
            controller.StartRecording();
            Debug.Log($"[GameplayRecorder3D] Recording to {root}/{takeName}.mp4");
            return root;
        }

        public static void Stop()
        {
            if (controller == null) return;
            if (controller.IsRecording())
            {
                controller.StopRecording();
                Debug.Log("[GameplayRecorder3D] Recording stopped");
            }
            controller = null;
        }

        private static CameraInputSettings MainCameraInput(int width, int height) => new CameraInputSettings
        {
            Source = ImageSource.MainCamera,
            OutputWidth = width,
            OutputHeight = height, // UI с камеры в URP не захватывается — HUD на записи нет
        };
    }
}
