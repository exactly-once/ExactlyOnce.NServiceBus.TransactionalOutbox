namespace ExactlyOnce.NServiceBus.AtomicConsumeDispatchOutbox.Sql
{
    using global::NServiceBus.Extensibility;
    using HarmonyLib;
    using System.Formats.Asn1;
    using System.Reflection;

    public class OutboxPatcher
    {
        // make sure DoPatching() is called at start either by
        // the mod loader or by your injector

        public static void EnableOutboxForSendsAtomicWithReceive()
        {
            var harmony = new Harmony("ExactlyOnce.NServiceBus.AtomicConsumeDispatchOutbox.Sql");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(global::NServiceBus.Features.Outbox))]
    [HarmonyPatch("Setup")]
    class OutboxFeatureSuppressSetup
    {
        static bool Prefix()
        {
            return false;
        }
    }

    [HarmonyPatch()]
    class OutboxSuppressSetAsDispatch
    {
        public static MethodBase TargetMethod()
        {
            var type = Type.GetType("OutboxPersister, NServiceBus.Persistence.Sql", true);
            var setAsDispatchedMethod =
                type.GetMethod("SetAsDispatched", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            return setAsDispatchedMethod;
        }

        static bool Prefix(ref Task __result)
        {
            __result = Task.CompletedTask;
            return false;
        }
    }
}
