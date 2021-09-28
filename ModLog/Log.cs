using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using System.Collections;
using UnityEngine;

namespace DLog
{
    internal class ModLogHandler : MonoBehaviour
    {
        // For keeping track of when the message was generated
        private static long realtimeEpoch;

        // Queues for dealing with threads
        private readonly static ConcurrentQueue<string> errorMessages = new ConcurrentQueue<string>();
        private readonly static ConcurrentQueue<string> warningMessages = new ConcurrentQueue<string>();
        private readonly static ConcurrentQueue<string> infoMessages = new ConcurrentQueue<string>();

        // Container to keep track of handles' levels
        private readonly static Dictionary<string, int> handleLevel = new Dictionary<string, int>();

        // Useful constants
        public const int MaxLogLevel = sizeof(int) * 8 - 1;
        public const int ErrorLevels = 5;
        public const int WarningLevels = 5;

        static readonly ModLogger logger = new ModLogger("ModLog");

        static ModLogHandler()
        {
            realtimeEpoch = 0;
            Chat.GameChat.OnPlayerMessageReceived.AddListener(HandleMessage);
            logger.SetLevel(new int[] { 0, 1, 2 });
            logger.SetLevelW(new int[] { 0, 1, 2 });
            logger.SetLevelE(new int[] { 0, 1, 2 });
        }

        private static void HandleMessage( Chat.ChatData chatData )
        {
            if ( chatData.player.isLocalPlayer && chatData.isCommand )
            {
                string[] message = chatData.message.Substring(1).ToLower().Split(' ');
                if (message[0] == "modlog")
                {
                    logger.Log($"Modlog Command: {message[1]}", 3);
                    // /modlog set levels <handle> info/warning/error <levels>
                    if (message.Length >= 6 && "levels".Contains(message[2]))
                    {
                        if ("set".Contains(message[1]))
                        {
                            CmdSetHandleLevel(message, true);
                        }
                        else if ("clear".Contains(message[1]))
                        {
                            CmdSetHandleLevel(message, false);
                        }
                        else
                        {
                            Chat.GameChat.PostServerMessage("Usage: \"/modlog set levels <handle> info/warning/error <levels>\"");
                            logger.LogE("Usage: \"/modlog set levels <handle> info/warning/error <levels>\"", 3);
                        }
                    }
                    // /modlog log <handle> info/warning/error <level> <message>
                    else if (message.Length >= 6 && "log".Contains(message[1]))
                    {
                        string toLog = "";
                        for ( int i = 5; i < message.Length; i++)
                        {
                            toLog += " ";
                            toLog += message[i];
                        }
                        toLog = toLog.Trim();

                        int level;
                        if ("info".Contains(message[3]) && int.TryParse(message[4], out level))
                        {

                            logger.Log(toLog, level, message[2]);
                        }
                        else if ("warning".Contains(message[3]) && int.TryParse(message[4], out level))
                        {
                            logger.LogW(toLog, level, message[2]);
                        }
                        else if ("error".Contains(message[3]) && int.TryParse(message[4], out level))
                        {
                            logger.LogE(toLog, level, message[2]);
                        }
                        else
                        {
                            Chat.GameChat.PostServerMessage("Usage: \"/modlog log <handle> info/warning/error <level> <message>\"");
                            logger.LogE("Usage: \"/modlog log <handle> info/warning/error <level> <message>\"", 3);
                        }
                    }
                    else
                    {
                        logger.LogE("Command not recognised", 3);
                    }
                }
            }
        }

        private static void CmdSetHandleLevel( string[] message, bool enable )
        {
            string handle = message[3];
            if (message.Length > 4 && "levels".Contains(message[2]))
            {
                for (int i = 0; i < message.Length - 5; i++)
                {
                    if ("info".Contains(message[4]))
                    {
                        SetHandleLevel(handle, int.Parse(message[5 + i]), enable);
                    }
                    else if ("warning".Contains(message[4]))
                    {
                        SetHandleLevelW(handle, int.Parse(message[5 + i]), enable);
                    }
                    else if ("error".Contains(message[4]))
                    {
                        SetHandleLevelE(handle, int.Parse(message[5 + i]), enable);
                    }
                    else
                    {
                        Chat.GameChat.PostServerMessage("Usage: \"/modlog set levels <handle> info/warning/error <levels>\"");
                        logger.LogE("Usage: \"/modlog set levels <handle> info/warning/error <levels>\"", 3);
                    }
                }
            }
            else
            {
                Chat.GameChat.PostServerMessage("Usage: \"/modlog set levels <handle> info/warning/error <levels>\"");
                logger.LogE("Usage: \"/modlog set levels <handle> info/warning/error <levels>\"", 3);
            }
        }


        public void Awake()
        {
            DontDestroyOnLoad(this);
            realtimeEpoch = DateTime.UtcNow.Ticks - (long)(Time.realtimeSinceStartup * TimeSpan.TicksPerSecond);
            logger.Log("SanityInfo");
            logger.LogW("SanityWarning");
            logger.LogE("SanityError");
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
                    Debug.Log(PrependText(logLevel, handle, message, "I"));
            }
        }
        public static void LogW(string handle, int logLevel, string message)
        {
            if (LevelIsActive(handle.ToLower(), WLevel(logLevel)))
            {
                Debug.LogWarning(PrependText(logLevel, handle, message, "W"));
            }
        }

        public static void LogE(string handle, int logLevel, string message)
        {
            if (LevelIsActive(handle.ToLower(), ELevel(logLevel)))
            {
                Debug.LogError(PrependText(logLevel, handle, message, "E"));
            }
        }

        public static void LogThread(string handle, int logLevel, string message)
        {
            if (LevelIsActive(handle.ToLower(), ILevel(logLevel)))
            {
                    infoMessages.Enqueue(PrependText(logLevel, handle, message, "I"));
            }
        }

        public static void LogThreadW(string handle, int logLevel, string message)
        {
            if (LevelIsActive(handle.ToLower(), WLevel(logLevel)))
            {
                warningMessages.Enqueue(PrependText(logLevel, handle, message, "W"));
            }
        }
        public static void LogThreadE(string handle, int logLevel, string message)
        {
            if (LevelIsActive(handle.ToLower(), ELevel(logLevel)))
            {
                errorMessages.Enqueue(PrependText(logLevel, handle, message, "E"));
            }
        }

        /// <summary>
        /// If using threads, this will flush the message cache to log. Call from Update and FixedUpdate.
        /// </summary>
        public void Update()
        {
            while (errorMessages.TryDequeue(out string message))
            {
                Debug.LogError(message);
            }

            while (warningMessages.TryDequeue(out string message))
            {
                Debug.LogWarning(message);
            }

            while (infoMessages.TryDequeue(out string message))
            {
                Debug.Log(message);
            }
        }

        public static void SetHandleLevel(string handle, int level, bool enable)
        {
            SetHandleLevelGeneric(handle, ILevel(level), enable);
        }
        public static void SetHandleLevelW(string handle, int level, bool enable)
        {
            SetHandleLevelGeneric(handle, WLevel(level), enable);
        }

        public static void SetHandleLevelE(string handle, int level, bool enable)
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


        public static void SetHandleLevel(string handle, int[] levels, bool enable)
        {
            foreach ( int level in levels )
            {
                SetHandleLevelGeneric(handle, ILevel(level), enable);
            }
        }
        public static void SetHandleLevelW(string handle, int[] levels, bool enable)
        {
            foreach (int level in levels)
            {
                SetHandleLevelGeneric(handle, WLevel(level), enable);
            }
        }

        public static void SetHandleLevelE(string handle, int[] levels, bool enable)
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

        private static string PrependText(int level, string handle, string message, string levelIdentifier = "")
        {
            return $"{GetTime()} [{handle}] {{{levelIdentifier} {level}}} {message}";
        }

        //RealTimeSinceStartup is not thread safe... somehow.
        private static float GetTime()
        {
            return (DateTime.UtcNow.Ticks - realtimeEpoch) / (float)TimeSpan.TicksPerSecond;
        }




    }


    public class ModLogger
    {
        public string defaultHandleName = "";

        public ModLogger(object callingObj)
        {
            this.defaultHandleName = callingObj.GetType().Name;
        }
        public ModLogger(string defaultHandleName)
        {
            this.defaultHandleName = defaultHandleName;
        }

        public void Log(string message, int level, string handle)
        {
            ModLogHandler.LogThread(handle, level, message);
        }

        public void Log(string message, int level = 0)
        {
            ModLogHandler.LogThread(defaultHandleName, level, message);
        }

        public void LogW(string message, int level, string handle)
        {
            ModLogHandler.LogThreadW(handle, level, message);
        }

        public void LogW(string message, int level = 0)
        {
            ModLogHandler.LogThreadW(defaultHandleName, level, message);
        }

        public void LogE(string message, int level, string handle)
        {
            ModLogHandler.LogThreadE(handle, level, message);
        }

        public void LogE(string message, int level = 0)
        {
            ModLogHandler.LogThreadE(defaultHandleName, level, message);
        }

        public void SetLevel(int[] level, bool enable = true)
        {
            ModLogHandler.SetHandleLevel(defaultHandleName, level, enable);
        }

        public void SetLevel(int level, bool enable = true)
        {
            ModLogHandler.SetHandleLevel(defaultHandleName, level, enable);
        }

        public void SetLevel(int[] level, bool enable, string handle)
        {
            ModLogHandler.SetHandleLevel(handle, level, enable);
        }

        public void SetLevel(int level, bool enable, string handle)
        {
            ModLogHandler.SetHandleLevel(handle, level, enable);
        }

        public void SetLevelW(int[] level, bool enable = true)
        {
            ModLogHandler.SetHandleLevelW(defaultHandleName, level, enable);
        }

        public void SetLevelW(int level, bool enable = true)
        {
            ModLogHandler.SetHandleLevelW(defaultHandleName, level, enable);
        }

        public void SetLevelW(int[] level, bool enable, string handle)
        {
            ModLogHandler.SetHandleLevelW(handle, level, enable);
        }

        public void SetLevelW(int level, bool enable, string handle)
        {
            ModLogHandler.SetHandleLevelW(handle, level, enable);
        }

        public void SetLevelE(int[] level, bool enable = true)
        {
            ModLogHandler.SetHandleLevelE(defaultHandleName, level, enable);
        }

        public void SetLevelE(int level, bool enable = true)
        {
            ModLogHandler.SetHandleLevelE(defaultHandleName, level, enable);
        }

        public void SetLevelE(int[] level, bool enable, string handle)
        {
            ModLogHandler.SetHandleLevelE(handle, level, enable);
        }

        public void SetLevelE(int level, bool enable, string handle)
        {
            ModLogHandler.SetHandleLevelE(handle, level, enable);
        }






    }
}
