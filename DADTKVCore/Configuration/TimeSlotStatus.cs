namespace Dadtkv;

/// <summary>
///     Status of a time slot.
/// </summary>
public class TimeSlotStatus : IComparable<TimeSlotStatus>
{
    public TimeSlotStatus(int timeSlot, string status)
    {
        TimeSlot = timeSlot;
        Status = status;
    }

    public int TimeSlot { get; set; }
    public string Status { get; set; }

    public int CompareTo(TimeSlotStatus? other)
    {
        return TimeSlot.CompareTo(other?.TimeSlot);
    }
}