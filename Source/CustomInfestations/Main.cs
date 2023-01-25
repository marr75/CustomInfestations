using System.Reflection;
using HarmonyLib;
using Verse;

namespace CustomInfestations
{
    [StaticConstructorOnStartup]
    class Main
    {
        static Main()
        {
            var harmony = new Harmony("com.github.harmony.rimworld.whitespacekilla.custominfestations");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
