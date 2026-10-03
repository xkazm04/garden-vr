using System.Collections.Generic;

namespace GardenVR.Core
{
    /// <summary>
    /// Append-only migrations for <see cref="TerrariumSave"/>.
    /// v1 is the first shipped document, so the list is empty.
    /// A later change appends one <see cref="MigrationStep"/> and raises <see cref="CurrentSchema"/>.
    /// Shipped steps are never edited, reordered, or removed.
    /// </summary>
    public static class TerrariumSaveMigrations
    {
        public const int CurrentSchema = 1;

        public static readonly IReadOnlyList<MigrationStep> Steps = new MigrationStep[0];
    }
}
