using System;
using System.Collections.Generic;

namespace JollyLlama.SkillTreeSystem
{
    /// <summary>
    /// A single cost line the skill tree asks the wallet to check, spend or refund.
    /// ResourceId matches ResourceDefinitionSO.resourceId on the node's cost list,
    /// so an adapter only needs to map that string to its own currency type.
    /// </summary>
    public readonly struct SkillTreeCost
    {
        public readonly string ResourceId;
        public readonly int    Amount;

        public SkillTreeCost(string resourceId, int amount)
        {
            ResourceId = resourceId;
            Amount     = amount;
        }

        public override string ToString() => $"{Amount} {ResourceId}";
    }

    /// <summary>
    /// The only thing the skill tree knows about the game's economy.
    ///
    /// SkillTreeManager never talks to ResourceManager (or any other concrete economy)
    /// directly. It goes through this interface, so a game can plug in its own
    /// currency system with a small adapter component.
    ///
    /// The bundled ResourceManager implements it for the demo scene.
    ///
    /// How the manager finds a wallet (first match wins):
    ///   1. SkillTreeManager.SetWallet(wallet) called from code (DI, bootstrap, etc.)
    ///   2. The 'walletComponent' field on SkillTreeManager in the Inspector
    ///   3. The first active MonoBehaviour in the scene that implements this interface
    /// </summary>
    public interface ISkillTreeWallet
    {
        /// <summary>Current balance for a resource. Return 0 for unknown ids.</summary>
        int GetBalance(string resourceId);

        /// <summary>True if every cost in the list can be paid.</summary>
        bool CanAfford(IReadOnlyList<SkillTreeCost> costs);

        /// <summary>
        /// Spend all costs as one transaction. Must be all-or-nothing:
        /// if any cost cannot be paid, nothing is deducted and false is returned.
        /// </summary>
        bool TrySpend(IReadOnlyList<SkillTreeCost> costs);

        /// <summary>Give back costs when a node rank is refunded.</summary>
        void Refund(IReadOnlyList<SkillTreeCost> costs);

        /// <summary>
        /// Raised whenever a balance changes (resourceId, newBalance), so the skill tree
        /// UI can refresh affordability. Raise it for changes made by the game too,
        /// not just for spends made by the skill tree.
        /// </summary>
        event Action<string, int> BalanceChanged;
    }
}
