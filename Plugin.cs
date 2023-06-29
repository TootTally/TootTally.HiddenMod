using BaboonAPI.Hooks.Initializer;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using TootTally.Replays;
using TootTally.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace TootTally.HiddenMod
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("TootTally", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin, ITootTallyModule
    {
        public static Plugin Instance;

        private const string CONFIG_NAME = "HiddenMod.cfg";
        private const string CONFIG_FIELD = "HiddenMod";
        public ConfigEntry<bool> ModuleConfigEnabled { get; set; }
        public bool IsConfigInitialized { get; set; }
        public string Name { get => PluginInfo.PLUGIN_NAME; set => Name = value; }

        public ManualLogSource GetLogger => Logger;

        public void LogInfo(string msg) => Logger.LogInfo(msg);
        public void LogError(string msg) => Logger.LogError(msg);

        private void Awake()
        {
            if (Instance != null) return;
            Instance = this;

            GameInitializationEvent.Register(Info, TryInitialize);
        }

        private void TryInitialize()
        {
            ModuleConfigEnabled = TootTally.Plugin.Instance.Config.Bind("Modules", "HiddenMod", true, "Makes the note disappear as they approach the target.");
            TootTally.Plugin.AddModule(this);
        }

        public void LoadModule()
        {
            Harmony.CreateAndPatchAll(typeof(HiddenNotePatches), PluginInfo.PLUGIN_GUID);
            LogInfo($"Module loaded!");
        }

        public void UnloadModule()
        {
            Harmony.UnpatchID(PluginInfo.PLUGIN_GUID);
            LogInfo($"Module unloaded!");
        }

        public static class HiddenNotePatches
        {
            public static List<GameObject> allnoteList;
            public static List<GameObject> activeNotes;
            public static List<FullNoteComponents> activeNotesComponents;
            public static List<FullNoteComponents> notesToRemove;
            [HarmonyPatch(typeof(GameController), nameof(GameController.Start))]
            [HarmonyPostfix]
            public static void GetAllNotesList(GameController __instance)
            {
                allnoteList = __instance.allnotes;
                activeNotes = new List<GameObject>();
                activeNotesComponents = new List<FullNoteComponents>();
                notesToRemove = new List<FullNoteComponents>();
                Plugin.Instance.LogInfo(__instance.trackmovemult.ToString());
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.Update))]
            [HarmonyPostfix]
            public static void HideNotesPatch(GameController __instance)
            {
                foreach (GameObject note in allnoteList)
                {
                    if (!activeNotes.Contains(note) && note.transform.position.x > -15 && note.transform.position.x <= 3.4f)
                    {
                        activeNotes.Add(note);
                        activeNotesComponents.Add(new FullNoteComponents()
                        {
                            StartPoint = note.transform.Find("StartPoint").GetComponent<Image>(),
                            StartPointColor = note.transform.Find("StartPoint/StartPointColor").GetComponent<Image>(),
                            EndPoint = note.transform.Find("EndPoint").GetComponent<Image>(),
                            EndPointColor = note.transform.Find("EndPoint/EndPointColor").GetComponent<Image>(),
                            OutlineLine = note.transform.Find("OutlineLine").GetComponent<LineRenderer>(),
                            Line = note.transform.Find("Line").GetComponent<LineRenderer>(),
                            note = note,
                            endBuffer = (note.transform.Find("EndPoint").position.x - note.transform.Find("StartPoint").position.x) / 6f / (__instance.trackmovemult / 275f * ReplaySystemManager.gameSpeedMultiplier),
                            lifespan = (note.transform.Find("EndPoint").position.x - note.transform.Find("StartPoint").position.x) / (__instance.trackmovemult / 275f * ReplaySystemManager.gameSpeedMultiplier)
                        });
                    }
                }

                foreach (FullNoteComponents note in activeNotesComponents)
                {
                    var deltaTimeMult = Time.deltaTime * (__instance.trackmovemult / 275f) * ReplaySystemManager.gameSpeedMultiplier;

                    //Fuck 4am coding im tired I wanna sleep
                    note.OutlineLine.startColor = new Color(note.OutlineLine.startColor.r, note.OutlineLine.startColor.g, note.OutlineLine.startColor.b, note.OutlineLine.startColor.a - deltaTimeMult);
                    note.Line.startColor = new Color(note.Line.startColor.r, note.Line.startColor.g, note.Line.startColor.b, note.Line.startColor.a - deltaTimeMult);
                    note.StartPoint.color = new Color(note.StartPoint.color.r, note.StartPoint.color.g, note.StartPoint.color.b, note.StartPoint.color.a - deltaTimeMult);
                    note.StartPointColor.color = new Color(note.StartPointColor.color.r, note.StartPointColor.color.g, note.StartPointColor.color.b, note.StartPointColor.color.a - deltaTimeMult);
                    if (note.endBuffer <= 0)
                    {
                        note.Line.endColor = new Color(note.Line.endColor.r, note.Line.endColor.g, note.Line.endColor.b, note.Line.endColor.a - deltaTimeMult);
                        note.OutlineLine.endColor = new Color(note.OutlineLine.endColor.r, note.OutlineLine.endColor.g, note.OutlineLine.endColor.b, note.OutlineLine.endColor.a - deltaTimeMult);
                        note.EndPoint.color = new Color(note.EndPoint.color.r, note.EndPoint.color.g, note.EndPoint.color.b, note.EndPoint.color.a - deltaTimeMult);
                        note.EndPointColor.color = new Color(note.EndPointColor.color.r, note.EndPointColor.color.g, note.EndPointColor.color.b, note.EndPointColor.color.a - deltaTimeMult);
                    }
                    else
                        note.endBuffer -= deltaTimeMult;
                    note.lifespan -= deltaTimeMult;

                    if (note.lifespan <= 0)
                        notesToRemove.Add(note);

                }

                // :SkullEmoji:
                if (notesToRemove.Count > 0)
                {
                    notesToRemove.ForEach(n => activeNotes.Remove(n.note));
                    activeNotesComponents.RemoveAll(notesToRemove.Contains);
                    notesToRemove.Clear();
                }
            }

            public class FullNoteComponents
            {
                public Image StartPoint, EndPoint;
                public Image StartPointColor, EndPointColor;
                public LineRenderer OutlineLine, Line;
                public GameObject note;
                public float endBuffer, lifespan;

            }
        }

    }
}