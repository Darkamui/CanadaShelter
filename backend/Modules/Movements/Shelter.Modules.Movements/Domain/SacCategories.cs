namespace Shelter.Modules.Movements.Domain;

/// <summary>
/// Shelter Animals Count (SAC) reporting categories (ADR 0017 amendment 1). Each intake reason and outcome type maps to
/// one, so reports count movements the way the SAC basic data matrix does, whatever the organization calls its reasons.
/// Stable codes, stored on the reference lists. TODO(pilot-review): check the mapping against the pilot's SAC reports.
/// </summary>
internal static class SacCategories
{
    /// <summary>Intake categories.</summary>
    public static class Intake
    {
        public const string Stray = "stray";
        public const string RelinquishedByOwner = "relinquished_by_owner";
        public const string TransferIn = "transfer_in";
        public const string Seized = "seized";
        public const string OtherIntake = "other_intake";

        public static IReadOnlyList<string> All { get; } = [Stray, RelinquishedByOwner, TransferIn, Seized, OtherIntake];
    }

    /// <summary>Outcome categories.</summary>
    public static class Outcome
    {
        public const string Adoption = "adoption";
        public const string ReturnToOwner = "return_to_owner";
        public const string TransferOut = "transfer_out";
        public const string ReturnToField = "return_to_field";
        public const string OtherLiveOutcome = "other_live_outcome";
        public const string DiedInCare = "died_in_care";
        public const string Euthanasia = "euthanasia";

        public static IReadOnlyList<string> All { get; } =
            [Adoption, ReturnToOwner, TransferOut, ReturnToField, OtherLiveOutcome, DiedInCare, Euthanasia];

        /// <summary>The animal goes to a person, who must be recorded on the outcome.</summary>
        public static bool RequiresPerson(string category) => category is Adoption or ReturnToOwner;

        /// <summary>The animal died: it can never be taken in again.</summary>
        public static bool IsTerminal(string category) => category is DiedInCare or Euthanasia;
    }
}
