using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;

namespace Dadtkv;

/// <summary>
///     Configuration of a server process.
/// </summary>
public class ServerProcessConfiguration : SystemConfiguration
{
    private readonly ILogger<ServerProcessConfiguration> _logger =
        DadtkvLogger.Factory.CreateLogger<ServerProcessConfiguration>();

    public readonly Timer TimeSlotTimer;

    public readonly ServerProcessInfo ProcessInfo;

    public int CurrentTimeSlot = 0;

    public ServerProcessConfiguration(SystemConfiguration systemConfiguration, string serverId) : base(
        systemConfiguration)
    {
        ProcessInfo = ServerProcesses.Find(info => info.Id.Equals(serverId))!;
        TimeSlotTimer = new Timer(TimeSlotDuration);

        var first = true;
        TimeSlotTimer.Elapsed += (_, _) =>
        {
            // Needed because the timer is started before the first time has elapsed
            if (first)
            {
                first = false;
                return;
            }


            var newTimeslot = CurrentTimeSlot + 1;
            _logger.LogDebug($"Starting time slot {newTimeslot}");
            // Check if process is crashed in the current time slot
            if (ProcessInfo.TimeSlotStatusList[_timeSlotCursor].Status == "C")
            {
                _logger.LogDebug("Crashing the process");
                Environment.Exit(1);
            }

            if (_timeSlotCursor + 1 < TimeSlotSuspicionsList.Count &&
                newTimeslot >= TimeSlotSuspicionsList[_timeSlotCursor + 1].TimeSlot)
                Interlocked.Increment(ref _timeSlotCursor);

            //TODO, is there a problem if _timeslotCursor and CurrentTimeSlot are not exchanged at exactly the same time?
            Interlocked.Increment(ref CurrentTimeSlot);

            _logger.LogDebug($"At time slot {CurrentTimeSlot}, the following suspicions are active:");
            foreach (var suspicion in CurrentSuspicions)
                _logger.LogDebug($"- {suspicion.Suspect} suspects {suspicion.Suspected}");
        };
    }

    public List<ServerProcessInfo> OtherServerProcesses =>
        ServerProcesses.Where(info => !info.Id.Equals(ProcessInfo.Id)).ToList();

    public List<ServerProcessInfo> OtherTransactionManagers =>
        TransactionManagers.Where(info => !info.Id.Equals(ProcessInfo.Id)).ToList();

    // TODO: Arranjar melhores nomes para isto das suspeitas?
    /// <summary>
    ///     Servers that are suspected by this server.
    /// </summary>
    protected HashSet<string> MyCurrentSuspected => new(CurrentSuspicions
        .Where(suspicion => suspicion.Suspect.Equals(ProcessInfo.Id))
        .Select(suspicion => suspicion.Suspected).ToList());

    /// <summary>
    ///     Servers that are suspecting this server.
    /// </summary>
    private HashSet<string> MyCurrentSuspecting => new(CurrentSuspicions
        .Where(suspicion => suspicion.Suspected.Equals(ProcessInfo.Id))
        .Select(suspicion => suspicion.Suspect).ToList());

    /// <summary>
    ///     Servers that are suspected by this server.
    ///     This list is updated when a server takes too long to respond to a request.
    /// </summary>
    private readonly HashSet<string> _realSuspected = new();

    public IEnumerable<string> RealSuspected => _realSuspected;

    public void AddRealSuspicion(string id)
    {
        if (id == ProcessInfo!.Id)
            return;
        _realSuspected.Add(id);
    }

    public void RemoveRealSuspicion(string id)
    {
        _realSuspected.Remove(id);
    }

    public ulong ServerId
    {
        get
        {
            var index = FindServerProcessIndex(ProcessInfo.Id);

            if (index < 0)
                throw new Exception("Server not found");

            return (ulong)index;
        }
    }

    /// <summary>
    ///     Throws an exception if this server is being suspected by the server with the given id.
    /// </summary>
    /// <param name="suspectingServerId">The id of the server that is suspecting this server.</param>
    public void TimeoutIfBeingSuspectedBy(ulong suspectingServerId)
    {
        var suspectingId = FindServerProcessId((int)suspectingServerId);
        if (!MyCurrentSuspecting.Contains(suspectingId)) // TODO: || !MyCurrentSuspected.Contains(suspectingId))
            return;

        _logger.LogDebug($"{suspectingId} is suspecting this server. Playing dead.");
        throw new Exception(); // Play dead
    }
}