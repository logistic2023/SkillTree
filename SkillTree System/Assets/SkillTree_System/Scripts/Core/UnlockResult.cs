using System.Collections.Generic;

namespace JollyLlama.SkillTreeSystem
{
    /// <summary>
    /// Returned by SkillTreeManager.TryUnlock and TryRefund.
    /// On success, Costs holds the list of resources that were spent (unlock) or returned (refund).
    /// </summary>
    public readonly struct UnlockResult
    {
        public readonly bool IsSuccess;
        public readonly string Message;

        /// <summary>
        /// Resources spent on a successful unlock, or resources returned on a successful refund.
        /// Each entry is (ResourceDefinitionSO, amount) — amount is always positive.
        /// </summary>
        public readonly IReadOnlyList<(ResourceDefinitionSO Resource, int Amount)> Costs;

        // ── Private ctor ──────────────────────────────────────────────────────────

        private UnlockResult(bool success, string message,
            IReadOnlyList<(ResourceDefinitionSO, int)> costs)
        {
            IsSuccess = success;
            Message   = message;
            Costs     = costs ?? System.Array.Empty<(ResourceDefinitionSO, int)>();
        }

        // ── Factory ───────────────────────────────────────────────────────────────

        public static UnlockResult Success(IReadOnlyList<(ResourceDefinitionSO, int)> costs)
            => new(true, string.Empty, costs);

        public static UnlockResult Fail(string reason)
            => new(false, reason, null);

        // ── Convenience ───────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the amount spent/refunded for a specific resource, or 0 if not in the list.
        /// </summary>
        public int GetAmount(ResourceDefinitionSO resource)
        {
            if (resource == null) return 0;
            foreach (var (res, amt) in Costs)
                if (res == resource) return amt;
            return 0;
        }
    }
}