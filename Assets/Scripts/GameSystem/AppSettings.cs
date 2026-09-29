using System;
using System.Collections;
using GameplayIngredients;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering.Universal;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
// ReSharper disable InconsistentNaming

namespace BoatAttack
{
    [ManagerDefaultPrefab(nameof(AppSettings))]
    public class AppSettings : Manager
    {
        public enum RenderRes
        {
            _Native,
            _1440p,
            _1080p,
            _720p
        }

        public enum Framerate
        {
            _30,
            _60,
            _120
        }

        public enum SpeedFormat
        {
            _Kph,
            _Mph
        }

        public static AppSettings Instance;
        private GameObject loadingScreenObject;
        public static Camera MainCamera;
        [Header("Resolution Settings")]
        public RenderRes maxRenderSize = RenderRes._720p;
        [Range(0f, 1f)]
        public float axisBias = 0.5f;
        public float minScale = 0.5f;
        public Framerate targetFramerate = Framerate._30;
        private float currentDynamicScale = 1.0f;
        private float maxScale = 1.0f;
        public SpeedFormat speedFormat = SpeedFormat._Mph;

        [Header("Asset References")]
        public AssetReference loadingScreen;
        public AssetReference volumeManager;
        [Header("Prefabs")]
        public GameObject consoleCanvas;
        public static GameObject ConsoleCanvas;

        public static bool HQmode = false;

        private int lastWindowHeight = 0;

        // Use this for initialization
        private void Awake()
        {
            if(Debug.isDebugBuild)
                Debug.Log("AppManager initializing");
            Initialize();
            CmdArgs();

            if (AppSettings.HQmode) {
                Physics.defaultSolverIterations = 18;
                Physics.defaultSolverVelocityIterations = 4;
            } else {
                Physics.defaultSolverIterations = 6;
                Physics.defaultSolverVelocityIterations = 1;
            }

            SceneManager.sceneLoaded += LevelWasLoaded;

            // Disable internal VSync (Android enforces its own hardware VSync)
            QualitySettings.vSyncCount = 0;
#if UNITY_ANDROID
        Screen.SetResolution(1920, 1080, true);
#elif UNITY_STANDALONE_WIN
            //Screen.SetResolution(1920, 1080, FullScreenMode.ExclusiveFullScreen);
            //Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
#endif
        }

        private void Initialize()
        {
            Instance = this;
            ConsoleCanvas = Instantiate(consoleCanvas);
            DontDestroyOnLoad(ConsoleCanvas);
            MainCamera = Camera.main;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= LevelWasLoaded;
        }

        private static void LevelWasLoaded(Scene scene, LoadSceneMode mode)
        {
            CleanupCameras();
#if STATIC_EVERYTHING
            Utility.StaticObjects();
#endif
            Instance.Invoke(nameof(CleanupLoadingScreen), 0.5f);
        }

        private static void CleanupCameras()
        {
            foreach (var c in GameObject.FindGameObjectsWithTag("MainCamera"))
            {
                if (MainCamera != null && c != MainCamera.gameObject)
                {
                    Destroy(c);
                }
                else
                {
                    MainCamera = c.GetComponent<Camera>();
                }
            }
        }

        private void CleanupLoadingScreen()
        {
            if(loadingScreenObject) loadingScreen?.ReleaseInstance(loadingScreenObject);
        }

        private void Update()
        {
#if !UNITY_EDITOR
            Utility.CheckQualityLevel(); //TODO - hoping to remove one day when we have a quality level callback
#endif

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Application.Quit();
            }

            // Only recalculate if the player resizes the window
            //if (Screen.height != lastWindowHeight)
            //{
            //    lastWindowHeight = Screen.height;
            //    EnforceFixedResolution();
            //}
        }
        private void EnforceFixedResolution()
        {
            if (UniversalRenderPipeline.asset == null) return;

            // Calculate the ratio needed to make the render buffer exactly 1080p.
            // Example: If window is 4K (2160p), scale becomes 1080 / 2160 = 0.5f.
            float targetHeight = 1080f;
            float scaleFactor = targetHeight / (float)Screen.height;

            // Apply scale directly to the pipeline asset
            UniversalRenderPipeline.asset.renderScale = scaleFactor;
        }

        public void ToggleSRPBatcher(bool enabled)
        {
            UniversalRenderPipeline.asset.useSRPBatcher = enabled;
        }

        public static void LoadScene(int buildIndex, LoadSceneMode mode = LoadSceneMode.Single)
        {
            LoadScene(SceneUtility.GetScenePathByBuildIndex(buildIndex), mode);
        }

        public static void LoadScene(string scenePath, LoadSceneMode mode = LoadSceneMode.Single)
        {
            Application.backgroundLoadingPriority = ThreadPriority.Low;
            switch (mode)
            {
                case LoadSceneMode.Single:
                    Instance.StartCoroutine(LoadSceneInternal(scenePath));
                    break;
                case LoadSceneMode.Additive:
                    SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        private static IEnumerator LoadSceneInternal(string scenePath)
        {
            var loadingScreenLoading = Instance.loadingScreen.InstantiateAsync();
            yield return loadingScreenLoading;
            Instance.loadingScreenObject = loadingScreenLoading.Result;
            Instance.loadingScreenObject.SendMessage("SetLoad", 0.0001f);
            DontDestroyOnLoad(Instance.loadingScreenObject);

            var buildIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);
            if(Debug.isDebugBuild)
                Debug.Log($"loading scene {scenePath} at build index {buildIndex}");

            // get current scene and set a loading scene as active
            var currentScene = SceneManager.GetActiveScene();
            var loadingScene = SceneManager.CreateScene("Loading");
            SceneManager.SetActiveScene(loadingScene);

            // unload last scene
            var unload = SceneManager.UnloadSceneAsync(currentScene, UnloadSceneOptions.None);
            while (!unload.isDone)
            {
                Instance.loadingScreenObject.SendMessage("SetLoad", unload.progress * 0.5f);
                yield return null;
            }

            // clean up
            var clean = Resources.UnloadUnusedAssets();
            while (!clean.isDone) { yield return null; }

            // load new scene
            var load = new AsyncOperation();
#if UNITY_EDITOR
            if (buildIndex == -1)
            {
                load = EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath,
                    new LoadSceneParameters(LoadSceneMode.Single));
            }
            else
            {
                load = SceneManager.LoadSceneAsync(buildIndex);
            }
#else
            load = SceneManager.LoadSceneAsync(scenePath);
#endif
            while (!load.isDone)
            {
                Instance.loadingScreenObject.SendMessage("SetLoad", load.progress * 0.5f + 0.5f);
                yield return null;
            }
        }

        private static IEnumerator LoadPrefab<T>(AssetReference assetRef, AsyncOperationHandle assetLoading, Transform parent = null)
        {
            if (typeof(T) == typeof(GameObject))
            {
                assetLoading = assetRef.InstantiateAsync(parent);
            }
            else
            {
                assetLoading = assetRef.LoadAssetAsync<T>();
            }
            yield return assetLoading;
        }

        public static void ExitGame(string s = "null")
        {
            if(s != "null")
                Debug.LogError(s);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }

        private static void CmdArgs()
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Length <= 0) return;
            foreach (var argRaw in args)
            {
                if(string.IsNullOrEmpty(argRaw) || argRaw[0] != '-') continue;
                var arg = argRaw.Split(':');

                switch (arg[0])
                {
                    case "-quality":
                        HQmode = arg[1].Equals("hq");
                        break;
                    case "-loadlevel":
                        LoadScene(arg[1]);
                        break;
                    case "-benchmarkFlythrough":
                        LoadScene("benchmark_island-flythrough");
                        break;
                }
            }
        }
    }

    public static class ConstantData
    {
        private static readonly string[] Levels =
        {
            "Island",
        };

        public static string GetLevelName(int level)
        {
            return $"scenes/_levels/level_{Levels[level]}";
        }

        public static readonly string[] AiNames =
        {
            "Felipe",
            "Andre",
            "Elvar",
            "Jonas",
            "Erika",
            "Tim",
            "Florin",
            "Andy",
            "Hakeem",
            "Sophia",
            "Martin",
        };

        public static readonly int[] Laps =
        {
            1,
            3,
            6,
            9
        };

        public static int SeedNow
        {
            get
            {
                DateTime dt = DateTime.Now;
                return dt.Year + dt.Month + dt.Day + dt.Hour + dt.Minute + dt.Second;
            }
        }

        public static Color[] ColorPalette;
        private static Texture2D _colorPaletteRaw;

        public static Color GetPaletteColor(int index)
        {
            GenerateColors();
            return ColorPalette[index];
        }

        public static Color GetRandomPaletteColor
        {
            get
            {
                GenerateColors();
                return ColorPalette[Random.Range(0, ColorPalette.Length)];
            }
        }

        private static void GenerateColors()
        {
            if (ColorPalette != null && ColorPalette.Length != 0) return;

            if (_colorPaletteRaw == null)
                _colorPaletteRaw = Resources.Load<Texture2D>("textures/colorSwatch");

            ColorPalette = _colorPaletteRaw.GetPixels();
            Debug.Log($"Found {ColorPalette.Length} colors.");
        }

    }
}
