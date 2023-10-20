using Microsoft.Extensions.Logging;

namespace Dadtkv;

/// <summary>
///     Configuration of a server process.
/// </summary>
public class ServerProcessConfiguration : SystemConfiguration
{
    private readonly ILogger<ServerProcessConfiguration> _logger =
        DadtkvLogger.Factory.CreateLogger<ServerProcessConfiguration>();

    public readonly ServerProcessInfo ProcessInfo;

    public ServerProcessConfiguration(SystemConfiguration systemConfiguration, string serverId) : base(
        systemConfiguration)
    {
        ProcessInfo = ServerProcesses.Find(info => info.Id.Equals(serverId))!;

        var currentTimeSlot = 0;
        TimeSlotTimer.Elapsed += (_, _) =>
        {
            // Needed because the timer is started before the first time has elapsed
            if (currentTimeSlot++ == 0)
                return;

            _logger.LogDebug($"Time slot {currentTimeSlot - 1} ended. Starting time slot {currentTimeSlot}");

            // Check if process is crashed in the current time slot
            if (ProcessInfo.TimeSlotStatusList[_timeSlotCursor].Status == "C")
            {
                _logger.LogDebug("Crashing the process");
                Environment.Exit(1);
            }

            if (_timeSlotCursor + 1 < TimeSlotSuspicionsList.Count &&
                currentTimeSlot >= TimeSlotSuspicionsList[_timeSlotCursor + 1].TimeSlot)
                _timeSlotCursor++;

            _logger.LogDebug($"At time slot {currentTimeSlot}, the following suspicions are active:");
            foreach (var suspicion in CurrentSuspicions)
                _logger.LogDebug($"- {suspicion.Suspect} suspects {suspicion.Suspected}");
        };
    }

    public List<ServerProcessInfo> OtherServerProcesses =>
        ServerProcesses.Where(info => !info.Id.Equals(ProcessInfo.Id)).ToList();

    public List<ServerProcessInfo> OtherTransactionManagers =>
        TransactionManagers.Where(info => !info.Id.Equals(ProcessInfo.Id)).ToList();

    /// <summary>
    ///     The current suspicions where this server is the suspect.
    /// </summary>
    public List<string> MyCurrentSuspected => CurrentSuspicions
        .Where(suspicion => suspicion.Suspect.Equals(ProcessInfo.Id))
        .Select(suspicion => suspicion.Suspected).ToList();

    /// <summary>
    ///     The current suspicions where this server is the suspected.
    /// </summary>
    public List<string> MyCurrentSuspecting => CurrentSuspicions
        .Where(suspicion => suspicion.Suspected.Equals(ProcessInfo.Id))
        .Select(suspicion => suspicion.Suspect).ToList();

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
        if (!MyCurrentSuspecting.Contains(suspectingId))
            return;

        _logger.LogDebug($"{suspectingId} is suspecting this server. Playing dead.");
        throw new Exception(); // Play dead
    }
}