using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DontLookBack.ObservationMaze
{
    public sealed class ObservationMazeGame : MonoBehaviour
    {
        private const float RoomWidth = 11f;
        private const float RoomHeight = 7f;
        private const float DoorShiftDelay = 1.35f;
        private readonly List<Room> rooms = new();
        private readonly List<Door> doors = new();
        private ObservationMazePlayer player;
        private Camera gameCamera;
        private Room currentRoom;
        private float unobservedTime;
        private float messageUntil;
        private string message = "마우스로 문을 바라보면 연결이 고정됩니다.";
        private bool generatorOn;
        private bool completed;
        private Texture2D pixel;

        private sealed class Room
        {
            public int Id;
            public string Name;
            public string Sound;
            public Vector2 Center;
            public Color Color;
            public bool Chalk;
        }

        private sealed class Door
        {
            public GameObject Object;
            public Room Owner;
            public DoorLinkCycle Links;
            public Vector2 Position;
            public bool Observed;
        }

        private void Awake()
        {
            pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
            BuildCamera();
            BuildFacility();
            BuildPlayer();
            EnterRoom(rooms[0], Vector2.zero);
        }

        private void BuildCamera()
        {
            gameCamera = Camera.main;
            if (gameCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                gameCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            gameCamera.orthographic = true;
            gameCamera.orthographicSize = 5.2f;
            gameCamera.backgroundColor = new Color(0.015f, 0.02f, 0.025f);
            gameCamera.transform.position = new Vector3(0f, 0f, -10f);
        }

        private void BuildFacility()
        {
            rooms.Add(NewRoom(0, "안전실", "낡은 환풍기 소리", new Vector2(0f, 0f), new Color(0.12f, 0.18f, 0.2f)));
            rooms.Add(NewRoom(1, "사무동", "형광등의 낮은 웅웅거림", new Vector2(18f, 0f), new Color(0.2f, 0.18f, 0.14f)));
            rooms.Add(NewRoom(2, "발전실", "무거운 기계 진동", new Vector2(36f, 0f), new Color(0.22f, 0.13f, 0.08f)));
            rooms.Add(NewRoom(3, "침수실", "오른쪽에서 흐르는 물소리", new Vector2(54f, 0f), new Color(0.06f, 0.15f, 0.22f)));
            rooms.Add(NewRoom(4, "출구 관찰실", "문틈의 차가운 바람", new Vector2(72f, 0f), new Color(0.12f, 0.1f, 0.17f)));

            AddDoor(rooms[0], 1, 3);
            AddDoor(rooms[1], 2, 3, 0);
            AddDoor(rooms[2], 4, 1);
            AddDoor(rooms[3], 1, 2);
            AddDoor(rooms[4], 0);
        }

        private Room NewRoom(int id, string name, string sound, Vector2 center, Color color)
        {
            var room = new Room { Id = id, Name = name, Sound = sound, Center = center, Color = color };
            var root = new GameObject($"Room {id} - {name}");
            root.transform.position = center;

            CreateSprite(root.transform, "Floor", Vector2.zero, new Vector2(RoomWidth, RoomHeight), color, -3);
            CreateWall(root.transform, new Vector2(0f, RoomHeight * 0.5f), new Vector2(RoomWidth, 0.25f));
            CreateWall(root.transform, new Vector2(0f, -RoomHeight * 0.5f), new Vector2(RoomWidth, 0.25f));
            CreateWall(root.transform, new Vector2(-RoomWidth * 0.5f, 0f), new Vector2(0.25f, RoomHeight));
            CreateWall(root.transform, new Vector2(RoomWidth * 0.5f, 1.9f), new Vector2(0.25f, 3.2f));
            CreateWall(root.transform, new Vector2(RoomWidth * 0.5f, -1.9f), new Vector2(0.25f, 3.2f));

            var sign = CreateText(root.transform, name, new Vector2(0f, 2.65f), 0.42f, Color.white);
            sign.name = "Room Identity";
            if (id == 2) CreateText(root.transform, "[E] 발전기 가동", new Vector2(0f, 0f), 0.34f, new Color(1f, 0.65f, 0.2f));
            if (id == 4) CreateText(root.transform, "[E] 시설 탈출", new Vector2(0f, 0f), 0.34f, new Color(0.7f, 0.9f, 1f));
            return room;
        }

        private void AddDoor(Room owner, params int[] destinations)
        {
            var doorObject = CreateSprite(null, $"Unstable Door - {owner.Name}", owner.Center + new Vector2(RoomWidth * 0.5f, 0f), new Vector2(0.45f, 1.65f), new Color(0.65f, 0.78f, 0.8f), 2);
            doors.Add(new Door
            {
                Object = doorObject,
                Owner = owner,
                Links = new DoorLinkCycle(destinations),
                Position = doorObject.transform.position
            });
        }

        private void BuildPlayer()
        {
            var playerObject = CreateSprite(null, "Observer", Vector2.zero, new Vector2(0.7f, 0.55f), new Color(0.82f, 0.9f, 0.78f), 5);
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CircleCollider2D>();
            player = playerObject.AddComponent<ObservationMazePlayer>();
            CreateSprite(playerObject.transform, "Flashlight Direction", new Vector2(0.55f, 0f), new Vector2(0.8f, 0.12f), new Color(1f, 0.9f, 0.45f), 4);
        }

        private void Update()
        {
            if (player == null || currentRoom == null || completed)
            {
                return;
            }

            UpdateObservation();
            UpdateRoomRules();
            HandleInput();
            gameCamera.transform.position = new Vector3(currentRoom.Center.x, currentRoom.Center.y, -10f);
        }

        private void UpdateObservation()
        {
            var door = GetCurrentDoor();
            if (door == null) return;

            var toDoor = door.Position - (Vector2)player.transform.position;
            var insideCone = toDoor.magnitude < 8f && Vector2.Angle(player.LookDirection, toDoor) <= 38f;
            var rayStart = (Vector2)player.transform.position + player.LookDirection * 0.45f;
            door.Observed = insideCone && !Physics2D.Linecast(rayStart, door.Position, LayerMask.GetMask("Default"));
            var renderer = door.Object.GetComponent<SpriteRenderer>();
            renderer.color = door.Observed
                ? new Color(0.95f, 0.88f, 0.45f)
                : new Color(0.38f, 0.48f, 0.52f);
        }

        private void UpdateRoomRules()
        {
            var door = GetCurrentDoor();
            if (door == null || door.Observed)
            {
                unobservedTime = 0f;
                return;
            }

            unobservedTime += Time.deltaTime;
            if (unobservedTime >= DoorShiftDelay)
            {
                door.Links.TryShift(false);
                unobservedTime = 0f;
                ShowMessage("벽 속에서 무언가 움직였다.");
            }
        }

        private void HandleInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.eKey.wasPressedThisFrame)
            {
                if (currentRoom.Id == 2 && Vector2.Distance(player.transform.position, currentRoom.Center) < 2f)
                {
                    generatorOn = true;
                    ShowMessage("발전기가 가동됐다. 출구 관찰실에서 바람 소리를 찾아라.");
                    return;
                }

                if (currentRoom.Id == 4 && Vector2.Distance(player.transform.position, currentRoom.Center) < 2f)
                {
                    if (generatorOn)
                    {
                        completed = true;
                        player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                        ShowMessage("탈출 성공 — 관찰된 현실이 당신을 놓아주었다.", 999f);
                    }
                    else
                    {
                        ShowMessage("엘리베이터에 전력이 없다.");
                    }
                    return;
                }

                var door = GetCurrentDoor();
                if (door != null && Vector2.Distance(player.transform.position, door.Position) < 1.4f)
                {
                    EnterRoom(rooms[door.Links.Current], new Vector2(-RoomWidth * 0.5f + 1f, 0f));
                }
            }

            if (keyboard.qKey.wasPressedThisFrame)
            {
                currentRoom.Chalk = !currentRoom.Chalk;
                ShowMessage(currentRoom.Chalk ? $"분필 별표를 {currentRoom.Name}에 남겼다." : "분필 표시를 지웠다.");
            }
        }

        private void EnterRoom(Room room, Vector2 localPosition)
        {
            currentRoom = room;
            player.transform.position = room.Center + localPosition;
            unobservedTime = 0f;
            ShowMessage($"{room.Name} — {room.Sound}");
        }

        private Door GetCurrentDoor() => doors.Find(door => door.Owner == currentRoom);

        private void ShowMessage(string value, float duration = 3.2f)
        {
            message = value;
            messageUntil = Time.time + duration;
        }

        private void OnGUI()
        {
            if (currentRoom == null || player == null) return;

            var scale = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            var width = Screen.width / scale;
            var height = Screen.height / scale;

            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.Box(new Rect(18f, 18f, 410f, 116f), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(34f, 30f, 380f, 24f), $"현재 방: {currentRoom.Name} {(currentRoom.Chalk ? "★" : "")}");
            GUI.Label(new Rect(34f, 54f, 380f, 24f), generatorOn ? "목표: 출구 관찰실로 이동" : "목표: 발전실을 찾아 전원 가동");
            GUI.Label(new Rect(34f, 78f, 380f, 44f), "이동 WASD/방향키 · 시선 마우스 · 문/장치 E\n분필 Q · 집중해서 듣기 F(이동 느려짐)");

            var door = GetCurrentDoor();
            if (door != null)
            {
                GUI.color = door.Observed ? new Color(1f, 0.9f, 0.4f) : new Color(0.7f, 0.8f, 0.85f);
                GUI.Label(new Rect(width - 260f, 28f, 240f, 28f), door.Observed ? "문 연결: 관찰로 고정됨" : "문 연결: 변화 가능");
            }

            if (player.IsListening)
            {
                GUI.color = new Color(0.6f, 0.85f, 1f);
                GUI.Label(new Rect(width * 0.5f - 150f, height - 80f, 300f, 30f), $"집중: {currentRoom.Sound}");
            }

            if (Time.time < messageUntil)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.84f);
                GUI.Box(new Rect(width * 0.5f - 270f, height - 130f, 540f, 48f), GUIContent.none);
                GUI.color = completed ? new Color(0.7f, 1f, 0.8f) : Color.white;
                GUI.Label(new Rect(width * 0.5f - 250f, height - 116f, 500f, 28f), message);
            }
        }

        private GameObject CreateWall(Transform parent, Vector2 localPosition, Vector2 size)
        {
            var wall = CreateSprite(parent, "Wall", localPosition, size, new Color(0.035f, 0.045f, 0.055f), 1);
            wall.AddComponent<BoxCollider2D>();
            return wall;
        }

        private GameObject CreateSprite(Transform parent, string objectName, Vector2 position, Vector2 size, Color color, int order)
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
            return gameObject;
        }

        private static GameObject CreateText(Transform parent, string value, Vector2 localPosition, float size, Color color)
        {
            var textObject = new GameObject("Label");
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = localPosition;
            var mesh = textObject.AddComponent<TextMesh>();
            mesh.text = value;
            mesh.fontSize = 48;
            mesh.characterSize = size / 10f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            mesh.GetComponent<MeshRenderer>().sortingOrder = 3;
            return textObject;
        }
    }
}
