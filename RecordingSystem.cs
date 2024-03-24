using Controllers;
using Kitchen;
using KitchenMods;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Entities;
using UnityEngine;

namespace KitchenPlayerInputInjector
{
    public class RecordingSystem : GenericSystemBase, IModSystem
    {
        private struct SRecording : IComponentData, IModComponent
        {
            public int PlayerID;
            public float Time;
        }

        EntityQuery Players;

        Dictionary<int, PlayerData> _playerDatas;
        Dictionary<int, PlayerData> PlayerDatas
        {
            get
            {
                if (_playerDatas == null)
                {
                    FieldInfo f_Players = typeof(BaseInputSource).GetField("Players", BindingFlags.NonPublic | BindingFlags.Instance);
                    _playerDatas = (Dictionary<int, PlayerData>)(f_Players?.GetValue(InputSourceIdentifier.DefaultInputSource));
                }
                return _playerDatas;
            }
        }

        private string FolderPath => Path.Combine(Main.FolderPath, "Recordings");

        private string _recordingFile = null;

        protected override void Initialise()
        {
            base.Initialise();
            Players = GetEntityQuery(typeof(CPlayer));
        }

        protected override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F7))
            {
                Main.LogInfo("Recording Key Pressed!");
                if (!Has<SRecording>() && !Players.IsEmpty)
                {
                    Entity recording = EntityManager.CreateEntity(typeof(SRecording), typeof(CDoNotPersist));
                    Set(recording, new SRecording()
                    {
                        PlayerID = Players.First<CPlayer>().ID
                    });
                    _recordingFile = $"{Random.Range(int.MinValue, int.MaxValue)}.csv";
                    Main.LogInfo($"Recording started: {_recordingFile}");
                }
                else
                {
                    Main.LogInfo("Recording Complete");
                    Clear<SRecording>();
                }
            }

            if (!Require(out SRecording sRecording))
            {
                _recordingFile = null;
                return;
            }

            if (Players.FirstMatchingEntity<CPlayer>((player) => player.ID == sRecording.PlayerID) == default)
            {
                Main.LogInfo("Recording Player Not Found!");
                Clear<SRecording>();
                return;
            }

            if (PlayerDatas == null ||
                !PlayerDatas.TryGetValue(sRecording.PlayerID, out PlayerData data))
            {
                Main.LogInfo("PlayerData not found!");
                Clear<SRecording>();
                return;
            }
            
            if (data.InputState != InputState.Neutral)
            {
                if (!AddFrame(sRecording.Time, data.InputState))
                {
                    Main.LogInfo("Failed to write to file!");
                    Clear<SRecording>();
                    return;
                }
            }

            sRecording.Time += Time.DeltaTime;
            Set(sRecording);
        }

        private bool AddFrame(float time, InputState state)
        {
            if (_recordingFile == null)
                return false;

            float angle = Vector2.Angle(Vector2.right, state.Movement);
            if (state.Movement.y < 0)
            {
                angle = 360 - angle;
            }
            string content = $"T{time:0.######},M{angle}\n";

            string filepath = Path.Combine(FolderPath, _recordingFile);

            try
            {
                File.AppendAllText(filepath, content);
            }
            catch (DirectoryNotFoundException)
            {
                Directory.CreateDirectory(FolderPath);
                File.AppendAllText(filepath, content);
            }
            return true;
        }
    }
}
