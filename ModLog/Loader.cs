using System;
using UnityEngine;
using BalsaCore;

namespace DarkLog
{
    [BalsaAddon]
    public class Loader
    {
        private static bool loaded = false;
        private static GameObject go;
        private static MonoBehaviour mod;

        [BalsaAddonInit]
        public static void BalsaInit()
        {

            if (!loaded)
            {
                loaded = true;
                go = new GameObject();
            }
            mod = go.AddComponent<ModLog>();
        }

        [BalsaAddonInit(invokeTime = AddonInvokeTime.Flight)]
        public static void BalsaInitFlight()
        {

        }

        [BalsaAddonFinalize(invokeTime = AddonInvokeTime.Flight)]
        public static void BalsaFinalizeFlight()
        {

        }
        //Game exit
        [BalsaAddonFinalize]
        public static void BalsaFinalize()
        {
            go.DestroyGameObject();
        }

    }
}
