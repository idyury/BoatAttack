﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using BoatAttack.UI;
using UnityEngine.UI;
using UnityEngine.Playables;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;
using Cinemachine;

namespace BoatAttack
{
    public class RaceManager : MonoBehaviour
    {
        
        #region Enums
        [Serializable]
        public enum GameType
        {
            Singleplayer = 0,
            LocalMultiplayer = 1,
            Multiplayer = 2,
            Spectator = 3,
            Benchmark = 4
        }

        [Serializable]
        public enum RaceType
        {
            Race,
            PointToPoint,
            TimeTrial
        }

        [Serializable]
        public class Race
        {
            //Race options
            public GameType game;
            public RaceType type;
            public int boatCount = 4; // currently hardcoded to 4

            //Level options
            public string level;
            public int laps = 3;
            public bool reversed;

            //Competitors
            public List<BoatData> boats;
        }
               
        #endregion

        public static RaceManager Instance;
        [NonSerialized] public static bool RaceStarted;
        [NonSerialized] public static bool LevelLoaded;
        [NonSerialized] public static float LevelLoadedTime = 0.0F;
        [NonSerialized] public static Race RaceData;
        public Race demoRaceData = new Race();
        [NonSerialized] public static float RaceTime;
        private readonly Dictionary<int, float> _boatTimes = new Dictionary<int, float>();

        public static Action<bool> raceStarted;

        [Header("Assets")] public AssetReference[] boats;
        public AssetReference raceUiPrefab;
        public AssetReference raceUiTouchPrefab;


        int frameCount = 0;
        int frameCountTotal = 0;
        double dt = 0.0;
        double dtTotal = 0.0;
        double updateDt = 0.5;
        float lastUpdateTime = 0.0F;

        
        public static void BoatFinished(int player)
        {
            switch (RaceData.game)
            {
                case GameType.Singleplayer:
                    if (player == 0)
                    {
                        var raceUi = RaceData.boats[0].Boat.RaceUi;
                        raceUi.MatchEnd();
                        ReplayCamera.Instance.EnableSpectatorMode();
                    }
                    break;
                case GameType.LocalMultiplayer:
                    break;
                case GameType.Multiplayer:
                    break;
                case GameType.Spectator:
                    break;
                case GameType.Benchmark:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        private void Awake()
        {
            if(Debug.isDebugBuild)
                Debug.Log("RaceManager Loaded");
            Instance = this;
        }

        private void Reset()
        {
            RaceStarted = false;
            LevelLoaded = false;
            RaceData.boats.Clear();
            RaceTime = 0f;
            _boatTimes.Clear();
            raceStarted = null;
        }

        public static void Setup(Scene scene, LoadSceneMode mode)
        {
            Instance.StartCoroutine(SetupRace());
        }

        public static IEnumerator SetupRace()
        {
            Random.InitState(0);
            if(RaceData == null) RaceData = Instance.demoRaceData; // make sure we have the data, otherwise default to demo data
            while (WaypointGroup.Instance == null) // TODO need to re-write whole game loading/race setup logic as it is dirty
            {
                yield return null;
            }
            WaypointGroup.Instance.Setup(RaceData.reversed); // setup waypoints
            yield return Instance.StartCoroutine(CreateBoats()); // spawn boats;

            bool hqmode = false;
            string[] args = System.Environment.GetCommandLineArgs();
            foreach (string arg in args) {
                if (arg.Equals("--hq")) {
                    hqmode = true;
                }
            }

            switch (RaceData.game)
            {
                case GameType.Singleplayer:
                    yield return Instance.StartCoroutine(CreatePlayerUi(0));
                    if (AppSettings.HQmode) {
                        SetupMultiCamera(0, 0, 0, 2, 2);
                        SetupMultiCamera(1, 1, 0, 2, 2);
                        SetupMultiCamera(2, 0, 1, 2, 2);
                        SetupMultiCamera(3, 1, 1, 2, 2);
                    } else {
                        SetupCamera(0); // setup camera for player 1
                    }
                    break;
                case GameType.LocalMultiplayer:
                    break;
                case GameType.Multiplayer:
                    break;
                case GameType.Spectator:
                    ReplayCamera.Instance.EnableSpectatorMode();
                    break;
                case GameType.Benchmark:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            Instance.StartCoroutine(BeginRace());
        }
        
        public static void SetGameType(GameType gameType)
        {
            RaceData = new Race {game = gameType,
                boats = new List<BoatData>(),
                boatCount = 4,
                laps = 3,
                type = RaceType.Race
            };

            Debug.Log($"Game type set to:{RaceData.game}");
            switch (RaceData.game)
            {
                case GameType.Singleplayer:
                    var b = new BoatData();
                    b.human = true; // single player is human
                    RaceData.boats.Add(b); // add player boat
                    GenerateRandomBoats(RaceData.boatCount - 1); // add random AI
                    break;
                case GameType.Spectator:
                    GenerateRandomBoats(RaceData.boatCount);
                    break;
                case GameType.LocalMultiplayer:
                    Debug.LogError("Not Implemented");
                    break;
                case GameType.Multiplayer:
                    Debug.LogError("Not Implemented");
                    break;
                case GameType.Benchmark:
                    Debug.LogError("Not Implemented");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static void SetLevel(int levelIndex)
        {
            RaceData.level = ConstantData.GetLevelName(levelIndex);
            Debug.Log($"Level set to:{levelIndex} with path:{RaceData.level}");
        }

        /// <summary>
        /// Triggered to begin the race
        /// </summary>
        /// <returns></returns>
        private static IEnumerator BeginRace()
        {
            LevelLoaded = true;
            LevelLoadedTime = Time.realtimeSinceStartup;
            Debug.Log("Level loaded");

            var introCams = GameObject.FindWithTag("introCameras");
            introCams.TryGetComponent<PlayableDirector>(out var introDirector);

            if (introDirector)
            {
                while (introDirector.state == PlayState.Playing)
                {
                    yield return null;
                }
                introCams.SetActive(false);
            }

            yield return new WaitForSeconds(3f); // countdown 3..2..1..
            
            RaceStarted = true;
            raceStarted?.Invoke(RaceStarted);
            
            SceneManager.sceneLoaded -= Setup;
        }

        /// <summary>
        /// Triggered when the race has finished
        /// </summary>
        private static void EndRace()
        {
            RaceStarted = false;
            switch (RaceData.game)
            {
                case GameType.Spectator:
                    UnloadRace();
                    break;
                case GameType.Singleplayer:
                    SetupCamera(0, true);
                    ReplayCamera.RaceDone = true;
                    break;
                case GameType.LocalMultiplayer:
                    break;
                case GameType.Multiplayer:
                    break;
                case GameType.Benchmark:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        private void UpdateFPS() {
            float timeNow = Time.realtimeSinceStartup;

            if (lastUpdateTime == 0.0F) {
                lastUpdateTime = LevelLoadedTime;
            }

            frameCount++;
            frameCountTotal++;
            dt = timeNow - lastUpdateTime;
            if (dt < updateDt) {
                return;
            }

            var fps = frameCount / dt;
            frameCount = 0;
            lastUpdateTime = timeNow;

            dtTotal = timeNow - LevelLoadedTime;
            var fpsTotal = frameCountTotal / dtTotal;

            var text = $"FPS: {fps.ToString("0.0")}. Ave: {fpsTotal.ToString("0.0")}. Frames: {frameCountTotal}. Time: {dtTotal.ToString("0.0")}";
            Debug.Log(text);
            TextMeshProUGUI fpsText = GameObject.Find("FPSCounter").GetComponent<TextMeshProUGUI>();
            if (fpsText) {
                fpsText.text = text;
            }
        }

        private void LateUpdate() {
            if (LevelLoaded) {
                UpdateFPS();
            }

            if (!RaceStarted) {
                return;
            }

            int finished = RaceData.boatCount;
            for (var i = 0; i < RaceData.boats.Count; i++)
            {
                var boat = RaceData.boats[i].Boat;
                if (boat.MatchComplete)
                {
                    _boatTimes[i] = Mathf.Infinity; // completed the race so no need to update
                    --finished;
                }
                else
                {
                    _boatTimes[i] = boat.LapPercentage + boat.LapCount;
                }
            }
            if(RaceStarted && finished == 0)
                EndRace();

            var mySortedList = _boatTimes.OrderBy(d => d.Value).ToList();
            var place = RaceData.boatCount;
            foreach (var boat in mySortedList.Select(index => RaceData.boats[index.Key].Boat).Where(boat => !boat.MatchComplete))
            {
                boat.Place = place;
                place--;
            }

            RaceTime += Time.deltaTime;
        }
        
        #region Utilities

        public static void LoadGame()
        {
            AppSettings.LoadScene(RaceData.level);
            SceneManager.sceneLoaded += Setup;
        }

        public static void UnloadRace()
        {
            Debug.LogWarning("Unloading Race");
            if(Instance.raceUiPrefab != null && Instance.raceUiPrefab.IsValid())
            {
                Instance.raceUiPrefab.ReleaseAsset();
            }

            Instance.Reset();
            AppSettings.LoadScene(0, LoadSceneMode.Single);
        }
        
        public static void SetHull(int player, int hull) => RaceData.boats[player].boatPrefab = Instance.boats[hull];
        
        private static IEnumerator CreateBoats()
        {
            for (int i = 0; i < RaceData.boats.Count; i++)
            {
                var boat = RaceData.boats[i]; // boat to setup

                // Load prefab
                var startingPosition = WaypointGroup.Instance.StartingPositions[i];
                AsyncOperationHandle<GameObject> boatLoading = Addressables.InstantiateAsync(boat.boatPrefab, startingPosition.GetColumn(3),
                    Quaternion.LookRotation(startingPosition.GetColumn(2)));

                yield return boatLoading; // wait for boat asset to load

                boatLoading.Result.name = boat.boatName; // set the name of the boat
                boatLoading.Result.TryGetComponent<Boat>(out var boatController);
                boat.SetController(boatLoading.Result, boatController);
                boatController.Setup(i + 1, boat.human, boat.livery);
                Instance._boatTimes.Add(i, 0f);
            }

        }
        
        private static void GenerateRandomBoats(int count, bool ai = true)
        {
            for (var i = 0; i < count; i++)
            {
                var boat = new BoatData();
                boat.boatName = ConstantData.AiNames[Random.Range(0, ConstantData.AiNames.Length)];
                BoatLivery livery = new BoatLivery
                {
                    primaryColor = ConstantData.GetRandomPaletteColor,
                    trimColor = ConstantData.GetRandomPaletteColor
                };
                boat.livery = livery;
                boat.boatPrefab = Instance.boats[Random.Range(0, Instance.boats.Length)];

                if (ai)
                    boat.human = false;

                RaceData.boats.Add(boat);
            }
        }

        private static IEnumerator CreatePlayerUi(int player)
        {
            var touch = Input.touchSupported && Input.multiTouchEnabled &&
                        (Application.platform == RuntimePlatform.Android ||
                         Application.platform == RuntimePlatform.IPhonePlayer);
            var uiAsset = touch ? Instance.raceUiTouchPrefab : Instance.raceUiPrefab;
            var uiLoading = uiAsset.InstantiateAsync();
            yield return uiLoading;
            if (uiLoading.Result.TryGetComponent(out RaceUI uiComponent))
            {
                var boatData = RaceData.boats[player];
                boatData.Boat.RaceUi = uiComponent;
                uiComponent.Setup(player);
            }
        }

        private static void SetupCamera(int player, bool remove = false)
        {
            // Setup race camera
            if(remove)
                AppSettings.MainCamera.cullingMask &= ~(1 << LayerMask.NameToLayer($"Player{player + 1}")); // TODO - this needs more work for when adding splitscreen.
            else
                AppSettings.MainCamera.cullingMask |= 1 << LayerMask.NameToLayer($"Player{player + 1}"); // TODO - this needs more work for when adding splitscreen.
        }

        private static void SetupMultiCamera(int player, int row, int col, int rowCnt, int colCnt)
        {
            float w = 1.0f / colCnt;
            float h = 1.0f / rowCnt;
            Camera camera = null;
            if (player == 0) {
                camera = AppSettings.MainCamera;
            } else {
                GameObject cameraObject = new GameObject("Extra Camera");
                camera = cameraObject.AddComponent<Camera>();
                camera.CopyFrom(AppSettings.MainCamera);

                CinemachineBrain brain = cameraObject.AddComponent<CinemachineBrain>();
                brain.m_UpdateMethod = CinemachineBrain.UpdateMethod.LateUpdate;
                brain.m_BlendUpdateMethod = CinemachineBrain.BrainUpdateMethod.LateUpdate;
                brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0f);
            }
            camera.rect = new Rect(w * col, h * row, w, h);
            camera.cullingMask |= 1 << LayerMask.NameToLayer($"Player{player + 1}");
            camera.allowDynamicResolution = false;
            camera.allowHDR = true;
            camera.enabled = true;
        }
        
        public static int GetLapCount()
        {
            if (RaceData != null && RaceData.type == RaceType.Race)
            {
                return RaceData.laps;
            }
            return -1;
        }
        
        #endregion
    }
}
