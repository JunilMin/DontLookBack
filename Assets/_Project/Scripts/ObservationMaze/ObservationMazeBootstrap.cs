using UnityEngine;

namespace DontLookBack.ObservationMaze
{
    public static class ObservationMazeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreatePrototype()
        {
            if (Object.FindFirstObjectByType<ObservationMazeGame>() != null)
            {
                return;
            }

            var root = new GameObject("Observation Maze Prototype");
            root.AddComponent<ObservationMazeGame>();
        }
    }
}
