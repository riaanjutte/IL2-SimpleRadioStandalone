using System;
using System.Collections.Generic;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Settings
{
    public static class InputPollingCycleCache
    {
        public static TState GetOrPoll<TState>(Guid instanceGuid, Dictionary<Guid, object> deviceStateCache, Func<TState> pollState)
        {
            object cachedState;
            if (deviceStateCache != null &&
                deviceStateCache.TryGetValue(instanceGuid, out cachedState) &&
                cachedState is TState)
            {
                return (TState)cachedState;
            }

            var state = pollState();
            if (deviceStateCache != null)
            {
                deviceStateCache[instanceGuid] = state;
            }

            return state;
        }

        public static bool ShouldAttemptRecoveryScan(bool hasBoundDevice)
        {
            return !hasBoundDevice;
        }
    }
}
