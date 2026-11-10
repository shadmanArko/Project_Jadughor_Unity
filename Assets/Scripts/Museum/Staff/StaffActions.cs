using System;
using ProjectMuseum.Data;

namespace ProjectMuseum.Staff
{
    /// <summary>Staff &amp; cleanliness events (same static-Action style as BuilderActions / MuseumActions).</summary>
    public static class StaffActions
    {
        /// <summary>Litter appeared on a tile (raised by MuseumDataModel.AddTrash).</summary>
        public static Action<TrashData> OnTrashAdded;

        /// <summary>Litter was swept, covered by an object, or otherwise removed.</summary>
        public static Action<TrashData> OnTrashRemoved;

        public static Action<StaffAgent> OnStaffHired;
        public static Action<StaffAgent> OnStaffFired;

        /// <summary>A janitor finished cleaning an exhibit (hook for future exhibit dirtiness).</summary>
        public static Action<string> OnExhibitCleaned;
    }

    public enum StaffRole
    {
        Janitor,
        // Guard, Guide, Technician … add roles here, then a StaffAgent subclass per role.
    }
}
