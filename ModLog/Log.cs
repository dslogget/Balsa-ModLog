using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Collections;
using UnityEngine;

namespace DarkLog
{
    public class ModLog : UnityEngine.MonoBehaviour
    {
        private static long realtimeEpoch;
        private readonly static ConcurrentQueue<string> errorMessages = new ConcurrentQueue<string>();
        private readonly static ConcurrentQueue<string> warningMessages = new ConcurrentQueue<string>();
        private readonly static ConcurrentQueue<string> infoMessages = new ConcurrentQueue<string>();
        private readonly static Dictionary<string, int> handleLevel = new Dictionary<string, int>();
        public const int MaxLogLevel = sizeof(int) * 8 - 1;
        public const int ErrorLevels = 5;
        public const int WarningLevels = 5;

        static ModLog()
        {
            realtimeEpoch = 0;
            SetHandleLevel("ModLog", new int[] {  
                0, 
                1, 
                2 }, true);
            SetHandleLevelW("ModLog", new int[] {
                0,
                1,
                2 }, true);
            SetHandleLevelE("ModLog", new int[] {
                0,
                1,
                2 }, true);

            Chat.GameChat.OnPlayerMessageReceived.AddListener(HandleMessage);
        }

        private static void HandleMessage( Chat.ChatData chatData )
        {
            bool success = false;
            if ( chatData.player.isLocalPlayer && chatData.isCommand )
            {
                Regex ModLogrx = new Regex(@"^/ModLog", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                if (ModLogrx.IsMatch(chatData.message) )
                {
                    Regex setLevelRx = new Regex(@"^\/ModLog\s+(?<op>s|c)[a-z]*\s+(?<handle>[a-z]+)\s+l[a-z]*\s+(?<type>[iwe])[a-z]*\s+(?<levels>(?:[0-9]+)(?:\s+[0-9]+)*)\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                    Match match = setLevelRx.Match(chatData.message);
                    if (match.Success)
                    {
                        bool set = match.Groups["op"].Value.ToLower()[0] == 's';
                        string handle = match.Groups["handle"].Value.ToLower();
                        string type = match.Groups["type"].Value.ToLower();
                        string[] levelsStr = match.Groups["levels"].Value.Split(' ');
                        int[] levels = new int[levelsStr.Length];
                        for (int i = 0; i < levels.Length; i++)
                        {
                            levels[i] = int.Parse(levelsStr[i]);
                        }
                        switch (type[0]) {
                            case 'i':
                                ModLog.SetHandleLevel(handle, levels, set) ;
                                success = true;
                                break;
                            case 'w':
                                ModLog.SetHandleLevelW(handle, levels, set);
                                success = true;
                                break;
                            case 'e':
                                ModLog.SetHandleLevelE(handle, levels, set);
                                success = true;
                                break;
                            default:
                                break;
                        }
                    } 
                    else
                    {
                        LogThread("ModLog", 4, "TestI");
                        LogThreadW("ModLog", 4, "TestW");
                        LogThreadE("ModLog", 4, "TestE");
                    }
                    if (!success)
                    {
                        Chat.GameChat.PostServerMessage("InvalidCommand, usage: \"/ModLog set/clear <handle> level info/warning/error <level1> <level2> ...\"");
                        LogThread("ModLog", 0, "Invalid Command");
                    }
                }
            }
        }


        public void Awake()
        {
            GameObject.DontDestroyOnLoad(this);
            realtimeEpoch = DateTime.UtcNow.Ticks - (long)(UnityEngine.Time.realtimeSinceStartup * TimeSpan.TicksPerSecond);
            Log("ModLog", 0, "SanityInfo");
            LogW("ModLog", 0, "SanityWarning");
            LogE("ModLog", 0, "SanityError");
        }

        public static int ELevel(int i)
        {
            return i;
        }

        public static int WLevel(int i)
        {
            return i + ErrorLevels;
        }

        public static int ILevel(int i)
        {
            return i + ErrorLevels + WarningLevels;
        }

        private static bool LevelIsActive( string handle, int logLevel )
        {
            return handleLevel.TryGetValue(handle, out int levels) && (levels & (1 << logLevel)) != 0;
        }

        public static void Log(string handle, int logLevel, string message)
        {
            if (LevelIsActive(handle.ToLower(), ILevel(logLevel)))
            {
                    UnityEngine.Debug.Log(PrependText(logLevel, handle, message));
            }
        }
        public static void LogW(string handle, int logLevel, string message)
        {
            if (LevelIsActive(handle.ToLower(), WLevel(logLevel)))
            {
                UnityEngine.Debug.LogWarning(PrependText(logLevel, handle, message));
            }
        }

        public static void LogE(string handle, int logLevel, string message)
        {
            if (LevelIsActive(handle.ToLower(), ELevel(logLevel)))
            {
                UnityEngine.Debug.LogError(PrependText(logLevel, handle, message));
            }
        }

        public static void LogThread(string handle, int logLevel, string message)
        {
            if (LevelIsActive(handle.ToLower(), ILevel(logLevel)))
            {
                    infoMessages.Enqueue(PrependText(logLevel, handle, message));
            }
        }

        public static void LogThreadW(string handle, int logLevel, string message)
        {
            if (LevelIsActive(handle.ToLower(), WLevel(logLevel)))
            {
                warningMessages.Enqueue(PrependText(logLevel, handle, message));
            }
        }
        public static void LogThreadE(string handle, int logLevel, string message)
        {
            if (LevelIsActive(handle.ToLower(), ELevel(logLevel)))
            {
                errorMessages.Enqueue(PrependText(logLevel, handle, message));
            }
        }

        /// <summary>
        /// If using threads, this will flush the message cache to log. Call from Update and FixedUpdate.
        /// </summary>
        public void Update()
        {
            while (errorMessages.TryDequeue(out string message))
            {
                UnityEngine.Debug.LogError(message);
            }

            while (warningMessages.TryDequeue(out string message))
            {
                UnityEngine.Debug.LogWarning(message);
            }

            while (infoMessages.TryDequeue(out string message))
            {
                UnityEngine.Debug.Log(message);
            }
        }

        private static void SetHandleLevel(string handle, int level, bool enable)
        {
            SetHandleLevelGeneric(handle, ILevel(level), enable);
        }
        private static void SetHandleLevelW(string handle, int level, bool enable)
        {
            SetHandleLevelGeneric(handle, WLevel(level), enable);
        }

        private static void SetHandleLevelE(string handle, int level, bool enable)
        {
            SetHandleLevelGeneric(handle, ELevel(level), enable);
        }

        private static void SetHandleLevelGeneric(string handle, int level, bool enable)
        {
            handle = handle.ToLower();
            if ( level < 0 || level > MaxLogLevel)
            {
                throw new ArgumentOutOfRangeException("level", $"level must be between 0 and {MaxLogLevel - WarningLevels - ErrorLevels}");
            }
            if ( !handleLevel.ContainsKey(handle) )
            {
                handleLevel.Add(handle, 0);
            }

            if (enable)
            {
                handleLevel[handle] |= (int)(1 << level);
            }
            else
            {
                handleLevel[handle] &= (int)~(1 << level);
            }
        }


        private static void SetHandleLevel(string handle, int[] levels, bool enable)
        {
            foreach ( int level in levels )
            {
                SetHandleLevelGeneric(handle, ILevel(level), enable);
            }
        }
        private static void SetHandleLevelW(string handle, int[] levels, bool enable)
        {
            foreach (int level in levels)
            {
                SetHandleLevelGeneric(handle, WLevel(level), enable);
            }
        }

        private static void SetHandleLevelE(string handle, int[] levels, bool enable)
        {
            foreach (int level in levels)
            {
                SetHandleLevelGeneric(handle, ELevel(level), enable);
            }
        }

        private static void SetHandleLevelGeneric(string handle, int[] levels, bool enable)
        {
            handle = handle.ToLower();
            foreach (int level in levels ) {
                if (level < 0 || level > MaxLogLevel)
                {
                    throw new ArgumentOutOfRangeException("level", $"level must be between 0 and {MaxLogLevel - WarningLevels - ErrorLevels}");
                }
                if (!handleLevel.ContainsKey(handle))
                {
                    handleLevel.Add(handle, 0);
                }

                if (enable)
                {
                    handleLevel[handle] |= (int)(1 << level);
                }
                else
                {
                    handleLevel[handle] &= (int)~(1 << level);
                }
            }
        }

        private static string PrependText(int level, string handle, string message)
        {
            return $"{GetTime()} [{handle}] {{{level}}} {message}";
        }

        //RealTimeSinceStartup is not thread safe... somehow.
        private static float GetTime()
        {
            return (DateTime.UtcNow.Ticks - realtimeEpoch) / (float)TimeSpan.TicksPerSecond;
        }




    }
}
