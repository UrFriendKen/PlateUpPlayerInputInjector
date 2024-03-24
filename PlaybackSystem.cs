using Controllers;
using Kitchen;
using KitchenMods;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Entities;
using UnityEngine;

namespace KitchenPlayerInputInjector
{
    internal class Frame
    {
        public float Time;
        public InputState State;
    }

    public class PlaybackSystem : GenericSystemBase, IModSystem
    {
        private const string FILENAME = "recording.csv";

        private string FilePath => Path.Combine(Main.FolderPath, FILENAME);

        private struct SPlayback : IComponentData, IModComponent
        {
            public int PlayerID;
            public float Time;
        }

        EntityQuery Players;

        Queue<Frame> Frames = new Queue<Frame>();

        private static int _automatedPlayer = 0;

        private static Frame CurrentFrame;

        protected override void Initialise()
        {
            base.Initialise();
            Players = GetEntityQuery(typeof(CPlayer));
        }

        protected override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F6))
            {
                Main.LogInfo("Playback Key Pressed");
                if (!Has<SPlayback>() && !Players.IsEmpty)
                {
                    Main.LogInfo("Create");
                    Entity playback = EntityManager.CreateEntity(typeof(SPlayback), typeof(CDoNotPersist));
                    Set(playback, new SPlayback()
                    {
                        PlayerID = Players.First<CPlayer>().ID
                    });
                    Reload();
                }
                else
                {
                    Main.LogInfo("Destroy");
                    Clear<SPlayback>();
                }
            }

            if (!Require(out SPlayback sPlayback) || Players.IsEmpty)
            {
                CurrentFrame = null;
                return;
            }

            if (Frames.Count <= 0 || Players.FirstMatchingEntity<CPlayer>((cPlayer) => cPlayer.ID == sPlayback.PlayerID) == default)
            {
                Main.LogInfo("Playback Complete");
                Clear<SPlayback>();
                return;
            }

            if (TryGetNextFrame(sPlayback.Time, out Frame nextFrame))
            {
                CurrentFrame = nextFrame;
            }
            sPlayback.Time += Time.DeltaTime;
            Set(sPlayback);
        }

        private void Reload()
        {
            Vector2 Rotate(Vector2 v, float delta)
            {
                delta = delta / 180f * Mathf.PI;
                return new Vector2(
                    v.x * Mathf.Cos(delta) - v.y * Mathf.Sin(delta),
                    v.x * Mathf.Sin(delta) + v.y * Mathf.Cos(delta)
                );
            }

            Frames.Clear();

            if (!Directory.Exists(Main.FolderPath))
                Directory.CreateDirectory(Main.FolderPath);

            if (File.Exists(FilePath))
            {
                using (StreamReader sr = new StreamReader(FilePath))
                {
                    string l = null;
                    do
                    {
                        l = sr.ReadLine()?.Trim();
                        if (l == null)
                            continue;

                        float frameTime = -1f;
                        InputState state = default;
                        foreach (string token in l.Split(','))
                        {
                            string cmd = token.ToLowerInvariant();
                            string content = cmd.Substring(1);

                            if (cmd.StartsWith("t"))
                            {
                                if (float.TryParse(content, out float time))
                                    frameTime = time;
                                continue;
                            }
                            if (cmd.StartsWith("m"))
                            {
                                if (float.TryParse(content, out float bearing))
                                    state.Movement = Rotate(Vector2.right, bearing);
                                continue;
                            }
                        }
                        if (frameTime >= 0)
                        {
                            Main.LogInfo(state.Movement);
                            Frames.Enqueue(new Frame()
                            {
                                Time = frameTime,
                                State = state
                            });
                        }
                    } while (l != null);
                    
                }
            }
            Main.LogInfo($"{Frames.Count} Loaded");
        }

        private bool TryGetNextFrame(float currentTime, out Frame frame)
        {
            frame = default;
            if (Frames.Count == 0)
            {
                return false;
            }

            do
            {
                frame = Frames.Dequeue();
            } while (Frames.Count > 0 && Frames.Peek().Time <= currentTime);
            return true;
        }

        internal static bool IsAutomatedPlayer(int playerID)
        {
            return playerID != 0 && playerID == _automatedPlayer;
        }

        internal static bool TryGetInputState(out InputState state)
        {
            if (CurrentFrame == null)
            {
                state = default;
                return false;
            }
            state = CurrentFrame.State;
            return true;

        }
    }
}
