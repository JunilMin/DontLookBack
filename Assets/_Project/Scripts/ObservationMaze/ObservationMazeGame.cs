using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace DontLookBack.ObservationMaze
{
    public sealed class ObservationMazeGame : MonoBehaviour
    {
        private const int MazeWidth = 31;
        private const int MazeHeight = 31;
        private const float CellSize = 2.35f;
        private const float WallThickness = 0.16f;
        private const int MazeSeed = 170319;
        private readonly List<Clue> clues = new();
        private readonly Dictionary<MazeCell, GameObject> chalkMarks = new();
        private readonly List<Anomaly> anomalies = new();
        private readonly List<Light2D> globalLights = new();
        private MazeLayout layout;
        private ObservationMazePlayer player;
        private Camera gameCamera;
        private Light2D flashlight;
        private Material litMaterial;
        private Texture2D pixel;
        private MazeCell powerCell;
        private GameObject powerConsole;
        private string message;
        private float messageUntil;
        private bool powerOn;
        private bool completed;
        private bool exitLockedMessageShown;
        private int foundClues;

        private sealed class Clue
        {
            public MazeCell Cell;
            public string Text;
            public GameObject Marker;
            public bool Found;
        }

        private sealed class Anomaly
        {
            public GameObject Marker;
            public MazeCell First;
            public MazeCell Second;
            public bool AtFirst = true;
            public float HiddenTime;
        }

        private void Awake()
        {
            pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            if (shader != null) litMaterial = new Material(shader);

            layout = new MazeLayout(MazeWidth, MazeHeight, MazeSeed);
            BuildCameraAndDarkness();
            BuildMaze();
            BuildPowerConsole();
            BuildPlayer();
            BuildClues();
            BuildAnomalies();
            ShowMessage("주 전원이 끊어졌다. 손전등을 이용해 가까운 전력 제어실을 찾아라.", 7f);
        }

        private void BuildCameraAndDarkness()
        {
            gameCamera = Camera.main;
            if (gameCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                gameCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            gameCamera.orthographic = true;
            gameCamera.orthographicSize = 3.8f;
            gameCamera.backgroundColor = new Color(0.002f, 0.004f, 0.006f);

            foreach (var global in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            {
                if (global.lightType == Light2D.LightType.Global)
                {
                    globalLights.Add(global);
                }
            }

            if (globalLights.Count == 0)
            {
                var globalObject = new GameObject("Facility Global Light");
                var global = globalObject.AddComponent<Light2D>();
                global.lightType = Light2D.LightType.Global;
                globalLights.Add(global);
            }

            SetFacilityLights(false);
        }

        private void BuildMaze()
        {
            var mazeRoot = new GameObject("Large Fixed Maze");
            var totalSize = new Vector2(MazeWidth * CellSize, MazeHeight * CellSize);
            CreateSprite(mazeRoot.transform, "Facility Floor", MazeToWorld(new MazeCell((MazeWidth - 1) / 2, (MazeHeight - 1) / 2)),
                totalSize, new Color(0.09f, 0.105f, 0.11f), -5, false);

            for (var x = 0; x < MazeWidth; x++)
            {
                for (var y = 0; y < MazeHeight; y++)
                {
                    var cell = new MazeCell(x, y);
                    var center = MazeToWorld(cell);
                    if (!layout.CanMove(cell, 0))
                        CreateWall(mazeRoot.transform, center + Vector2.up * CellSize * 0.5f, new Vector2(CellSize + WallThickness, WallThickness));
                    if (!layout.CanMove(cell, 1))
                        CreateWall(mazeRoot.transform, center + Vector2.right * CellSize * 0.5f, new Vector2(WallThickness, CellSize + WallThickness));
                    if (y == 0 && !layout.CanMove(cell, 2))
                        CreateWall(mazeRoot.transform, center + Vector2.down * CellSize * 0.5f, new Vector2(CellSize + WallThickness, WallThickness));
                    if (x == 0 && !layout.CanMove(cell, 3))
                        CreateWall(mazeRoot.transform, center + Vector2.left * CellSize * 0.5f, new Vector2(WallThickness, CellSize + WallThickness));
                }
            }

            CreateSprite(null, "Central Observation Room", MazeToWorld(layout.Start), Vector2.one * 1.3f,
                new Color(0.18f, 0.42f, 0.46f), -1, false);
            CreateSprite(null, "Exterior Exit", MazeToWorld(layout.Exit), Vector2.one * 1.05f,
                new Color(0.25f, 0.82f, 0.55f), 1, false);
        }

        private void BuildPowerConsole()
        {
            var pathIndex = Mathf.Clamp(12, 2, layout.Solution.Count - 2);
            powerCell = layout.Solution[pathIndex];
            powerConsole = CreateSprite(null, "Emergency Power Console", MazeToWorld(powerCell),
                new Vector2(0.62f, 0.62f), new Color(0.95f, 0.38f, 0.12f), 3, false);
        }

        private void BuildPlayer()
        {
            var playerObject = CreateSprite(null, "Observer", MazeToWorld(layout.Start), new Vector2(0.55f, 0.45f),
                new Color(0.82f, 0.92f, 0.82f), 8, false);
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CircleCollider2D>();
            player = playerObject.AddComponent<ObservationMazePlayer>();
            CreateSprite(playerObject.transform, "Facing Direction", new Vector2(0.45f, 0f), new Vector2(0.65f, 0.09f),
                new Color(1f, 0.88f, 0.38f), 9, false);

            var lightObject = new GameObject("Narrow Flashlight");
            lightObject.transform.SetParent(playerObject.transform, false);
            flashlight = lightObject.AddComponent<Light2D>();
            flashlight.lightType = Light2D.LightType.Point;
            flashlight.intensity = 1.15f;
            flashlight.color = new Color(1f, 0.88f, 0.62f);
            flashlight.pointLightInnerRadius = 0.35f;
            flashlight.pointLightOuterRadius = 7.2f;
            flashlight.pointLightInnerAngle = 22f;
            flashlight.pointLightOuterAngle = 58f;
            flashlight.falloffIntensity = 0.72f;
        }

        private void BuildClues()
        {
            var path = layout.Solution;
            var clueCount = 9;
            for (var index = 1; index <= clueCount; index++)
            {
                var pathIndex = Mathf.Clamp(index * path.Count / (clueCount + 1), 1, path.Count - 2);
                var cell = path[pathIndex];
                var next = path[Mathf.Min(pathIndex + 1, path.Count - 1)];
                var direction = DirectionName(next.X - cell.X, next.Y - cell.Y);
                var zone = ZoneName(cell);
                var text = index switch
                {
                    1 => $"시설 기록: 출구는 중앙이 아니라 외벽에 있다. {direction} 통로를 확인하라.",
                    3 => $"배관 표식: 번호가 커지는 방향이 외벽이다. 다음 흐름은 {direction}.",
                    5 => $"근무자 메모: {zone}의 바람은 출구 환기구에서 들어온다. {direction}으로 향한다.",
                    7 => $"찢어진 대피도: 녹색 외벽등까지 {direction}.",
                    9 => $"차가운 바람이 강해진다. 출구는 이제 {direction} 방향에서 가깝다.",
                    _ => $"{zone} 표지판: 외곽 순찰로는 {direction} 방향."
                };

                var marker = CreateSprite(null, $"Clue {index:00}", MazeToWorld(cell), Vector2.one * 0.38f,
                    new Color(0.94f, 0.66f, 0.18f), 2, false);
                clues.Add(new Clue { Cell = cell, Text = text, Marker = marker });
            }
        }

        private void BuildAnomalies()
        {
            var path = layout.Solution;
            var indices = new[] { path.Count / 4, path.Count / 2, path.Count * 3 / 4 };
            for (var i = 0; i < indices.Length; i++)
            {
                var first = path[Mathf.Clamp(indices[i], 2, path.Count - 3)];
                var second = path[Mathf.Clamp(indices[i] + 2, 2, path.Count - 3)];
                var marker = CreateSprite(null, $"Unobserved Room Marker {i + 1}", MazeToWorld(first),
                    new Vector2(0.55f, 0.55f), new Color(0.66f, 0.22f, 0.3f), 1, false);
                anomalies.Add(new Anomaly { Marker = marker, First = first, Second = second });
            }
        }

        private void Update()
        {
            if (player == null || completed) return;
            var playerPosition = (Vector2)player.transform.position;
            gameCamera.transform.position = new Vector3(playerPosition.x, playerPosition.y, -10f);

            UpdateClues(playerPosition);
            UpdateAnomalies(playerPosition);
            HandleInput(playerPosition);

            if (Vector2.Distance(playerPosition, MazeToWorld(layout.Exit)) < 0.75f)
            {
                if (!powerOn)
                {
                    if (!exitLockedMessageShown)
                    {
                        exitLockedMessageShown = true;
                        ShowMessage("출구 잠금장치에 전력이 없다. 먼저 주 전원을 복구해야 한다.", 5f);
                    }
                    return;
                }

                completed = true;
                player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                ShowMessage($"탈출 성공 — {foundClues}/{clues.Count}개의 단서로 외벽에 도달했다.", 999f);
            }
        }

        private void UpdateClues(Vector2 playerPosition)
        {
            foreach (var clue in clues)
            {
                if (clue.Found || Vector2.Distance(playerPosition, clue.Marker.transform.position) > 0.72f) continue;
                clue.Found = true;
                foundClues++;
                clue.Marker.GetComponent<SpriteRenderer>().color = new Color(0.35f, 0.5f, 0.38f);
                ShowMessage(clue.Text, 7f);
            }
        }

        private void UpdateAnomalies(Vector2 playerPosition)
        {
            foreach (var anomaly in anomalies)
            {
                var toMarker = (Vector2)anomaly.Marker.transform.position - playerPosition;
                var observed = toMarker.magnitude < 7.2f && Vector2.Angle(player.LookDirection, toMarker) < 30f;
                if (observed)
                {
                    anomaly.HiddenTime = 0f;
                    anomaly.Marker.GetComponent<SpriteRenderer>().color = new Color(0.85f, 0.36f, 0.38f);
                    continue;
                }

                anomaly.HiddenTime += Time.deltaTime;
                if (anomaly.HiddenTime < 2.2f) continue;
                anomaly.AtFirst = !anomaly.AtFirst;
                anomaly.Marker.transform.position = MazeToWorld(anomaly.AtFirst ? anomaly.First : anomaly.Second);
                anomaly.HiddenTime = 0f;
                ShowMessage("보지 않던 공간에서 금속 마찰음이 들렸다.", 2.8f);
            }
        }

        private void HandleInput(Vector2 playerPosition)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (!powerOn && keyboard.eKey.wasPressedThisFrame &&
                Vector2.Distance(playerPosition, powerConsole.transform.position) < 0.9f)
            {
                powerOn = true;
                SetFacilityLights(true);
                powerConsole.GetComponent<SpriteRenderer>().color = new Color(0.25f, 0.9f, 0.48f);
                ShowMessage("주 전력 복구 완료. 시설 전체 조명이 켜졌다. 이제 외벽 출구를 찾아라.", 7f);
                return;
            }

            if (!keyboard.qKey.wasPressedThisFrame) return;
            var cell = WorldToMaze(playerPosition);
            if (!layout.IsInside(cell)) return;

            if (chalkMarks.TryGetValue(cell, out var existing))
            {
                Destroy(existing);
                chalkMarks.Remove(cell);
                ShowMessage("이 위치의 분필 표시를 지웠다.");
            }
            else
            {
                var mark = CreateSprite(null, "Chalk Mark", MazeToWorld(cell) + new Vector2(0f, -0.55f),
                    new Vector2(0.34f, 0.09f), new Color(0.72f, 0.86f, 0.9f), 3, false);
                chalkMarks[cell] = mark;
                ShowMessage($"분필 표시를 남겼다. 현재 구역: {ZoneName(cell)}");
            }
        }

        private void OnGUI()
        {
            if (player == null) return;
            var scale = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            var width = Screen.width / scale;
            var height = Screen.height / scale;
            var cell = WorldToMaze(player.transform.position);

            GUI.color = new Color(0f, 0f, 0f, 0.82f);
            GUI.Box(new Rect(18f, 18f, 420f, 118f), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(34f, 30f, 390f, 24f), $"구역: {ZoneName(cell)} · 발견한 단서 {foundClues}/{clues.Count}");
            GUI.color = powerOn ? new Color(0.65f, 1f, 0.72f) : new Color(1f, 0.62f, 0.32f);
            GUI.Label(new Rect(34f, 55f, 390f, 24f),
                powerOn ? "목표: 시설 외벽의 녹색 출구를 찾기" : "현재 퀘스트: 전력 제어실에서 주 전원 복구 [E]");
            GUI.color = Color.white;
            GUI.Label(new Rect(34f, 80f, 390f, 44f), "이동 WASD/방향키 · 시선 마우스 · 상호작용 E\n분필 Q · 집중해서 듣기 F");

            if (player.IsListening)
            {
                GUI.color = new Color(0.58f, 0.84f, 1f);
                GUI.Label(new Rect(width * 0.5f - 220f, height - 80f, 440f, 28f), AmbientHint(cell));
            }

            if (Time.time < messageUntil)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.88f);
                GUI.Box(new Rect(width * 0.5f - 310f, height - 142f, 620f, 54f), GUIContent.none);
                GUI.color = completed ? new Color(0.62f, 1f, 0.76f) : Color.white;
                GUI.Label(new Rect(width * 0.5f - 288f, height - 126f, 576f, 34f), message);
            }
        }

        private string AmbientHint(MazeCell cell)
        {
            if (!powerOn)
            {
                var powerDx = powerCell.X - cell.X;
                var powerDy = powerCell.Y - cell.Y;
                var powerDistance = Mathf.Abs(powerDx) + Mathf.Abs(powerDy);
                return powerDistance < 4
                    ? "집중: 가까운 곳에서 비상 전력 장치의 짧은 신호음"
                    : "집중: 어둠 속에서 희미한 전기 신호음";
            }

            var dx = layout.Exit.X - cell.X;
            var dy = layout.Exit.Y - cell.Y;
            var horizontal = dx == 0 ? "" : dx > 0 ? "오른쪽" : "왼쪽";
            var vertical = dy == 0 ? "" : dy > 0 ? "위쪽" : "아래쪽";
            var direction = string.IsNullOrEmpty(horizontal) ? vertical :
                string.IsNullOrEmpty(vertical) ? horizontal : $"{horizontal}과 {vertical} 사이";
            var distance = Mathf.Abs(dx) + Mathf.Abs(dy);
            return distance < 8
                ? $"집중: {direction}에서 강한 바람과 느슨한 철문 소리"
                : $"집중: {direction}에서 아주 약한 공기 흐름";
        }

        private string ZoneName(MazeCell cell)
        {
            var horizontal = cell.X < MazeWidth / 3 ? "서쪽" : cell.X >= MazeWidth * 2 / 3 ? "동쪽" : "중앙";
            var vertical = cell.Y < MazeHeight / 3 ? "수몰 구역" : cell.Y >= MazeHeight * 2 / 3 ? "감시 구역" : "연구 구역";
            return $"{horizontal} {vertical}";
        }

        private static string DirectionName(int dx, int dy)
        {
            if (dx > 0) return "동쪽";
            if (dx < 0) return "서쪽";
            if (dy > 0) return "북쪽";
            return "남쪽";
        }

        private Vector2 MazeToWorld(MazeCell cell) =>
            new((cell.X - MazeWidth / 2) * CellSize, (cell.Y - MazeHeight / 2) * CellSize);

        private MazeCell WorldToMaze(Vector2 world) =>
            new(Mathf.RoundToInt(world.x / CellSize) + MazeWidth / 2,
                Mathf.RoundToInt(world.y / CellSize) + MazeHeight / 2);

        private void ShowMessage(string value, float duration = 3.4f)
        {
            message = value;
            messageUntil = Time.time + duration;
        }

        private void SetFacilityLights(bool enabled)
        {
            foreach (var global in globalLights)
            {
                global.intensity = enabled ? 0.78f : 0.018f;
                global.color = enabled ? new Color(0.88f, 0.94f, 1f) : new Color(0.14f, 0.2f, 0.25f);
            }

            if (flashlight != null)
            {
                flashlight.intensity = enabled ? 0.38f : 1.15f;
            }
        }

        private void CreateWall(Transform parent, Vector2 position, Vector2 size)
        {
            CreateSprite(parent, "Maze Wall", position, size, new Color(0.18f, 0.22f, 0.23f), 0, true);
        }

        private GameObject CreateSprite(Transform parent, string objectName, Vector2 position, Vector2 size,
            Color color, int order, bool collider)
        {
            var gameObject = new GameObject(objectName);
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
                gameObject.transform.localPosition = position;
            }
            else
            {
                gameObject.transform.position = position;
            }

            gameObject.transform.localScale = size;
            var renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = Sprite.Create(pixel, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            renderer.color = color;
            renderer.sortingOrder = order;
            if (litMaterial != null) renderer.sharedMaterial = litMaterial;
            if (collider) gameObject.AddComponent<BoxCollider2D>();
            return gameObject;
        }
    }
}
