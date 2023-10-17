using System.Diagnostics;

namespace Dadtkv;

/// <summary>
///     Configuration of the system.
/// </summary>
public class SystemConfiguration
{
    private SystemConfiguration()
    {
    }

    protected SystemConfiguration(SystemConfiguration systemConfiguration)
    {
        Processes = systemConfiguration.Processes;
        ServerProcesses = systemConfiguration.ServerProcesses;
        LeaseManagers = systemConfiguration.LeaseManagers;
        TransactionManagers = systemConfiguration.TransactionManagers;
        Clients = systemConfiguration.Clients;

        TimeSlotDuration = systemConfiguration.TimeSlotDuration;
        NumberOfTimeSlots = systemConfiguration.NumberOfTimeSlots;
        WallTime = systemConfiguration.WallTime;
        TimeSlotSuspicionsList = systemConfiguration.TimeSlotSuspicionsList;
    }

    protected List<IProcessInfo> Processes { get; } = new();
    public readonly List<ServerProcessInfo> ServerProcesses = new();
    public readonly List<ServerProcessInfo> LeaseManagers = new();
    public readonly List<ServerProcessInfo> TransactionManagers = new();
    public readonly List<ClientProcessInfo> Clients = new();

    private int NumberOfTimeSlots { get; set; }
    public int TimeSlotDuration { get; set; }
    private DateTime WallTime { get; set; }

    public List<TimeSlotSuspicions> TimeSlotSuspicionsList { get; } = new();

    public class TimeSlotSuspicions
    {
        public int TimeSlot { get; set; }
        public List<Suspicion> Suspicions { get; }

        public TimeSlotSuspicions(int timeSlot, List<Suspicion> suspicions)
        {
            TimeSlot = timeSlot;
            Suspicions = suspicions;
        }
    }

    public class Suspicion
    {
        public string Suspect { get; set; }
        public string Suspected { get; set; }

        public Suspicion(string suspect, string suspected)
        {
            Suspect = suspect;
            Suspected = suspected;
        }
    }
    
    public int TimeSlotCursor = 0;

    protected List<Suspicion> CurrentSuspicions =>
        TimeSlotSuspicionsList.Count > 0 ? TimeSlotSuspicionsList[TimeSlotCursor].Suspicions : new List<Suspicion>();

    /// <summary>
    ///     Gets the lease manager Id number.
    /// </summary>
    /// <param name="id">The id of the lease manager.</param>
    /// <returns>The lease manager Id number.</returns>
    protected int GetLeaseManagerIdNum(string id)
    {
        return LeaseManagers.FindIndex(proc => proc.Id.Equals(id)) + 1;
    }

    public int FindProcessIndex(string id) => Processes.FindIndex(p => p.Id.Equals(id));

    /// <summary>
    ///     Reads the system configuration from a file and returns a <see cref="SystemConfiguration" /> object.
    /// </summary>
    /// <param name="filePath">Path to the configuration file.</param>
    /// <returns>A <see cref="SystemConfiguration" /> object.</returns>
    public static SystemConfiguration ReadSystemConfiguration(string filePath)
    {
        // Read and parse the configuration file
        var lines = File.ReadAllLines(filePath);
        var systemConfig = new SystemConfiguration();

        foreach (var line in lines)
        {
            var parts = line.Split(' ');
            if (parts.Length >= 2)
            {
                var command = parts[0];
                var parameters = parts.Skip(1).ToArray();

                // Process different commands from the configuration file
                switch (command)
                {
                    case "#": // Comment
                        continue;
                    case "P": // Process identifier and role (Server or Client)
                        var id = parameters[0];
                        var role = parameters[1];

                        if (role is "T" or "L")
                        {
                            var server = new ServerProcessInfo(id, role, parameters[2]);

                            systemConfig.ServerProcesses.Add(server);
                            systemConfig.Processes.Add(server);
                            if (role is "T")
                                systemConfig.TransactionManagers.Add(server);
                            else
                                systemConfig.LeaseManagers.Add(server);
                        }
                        else
                        {
                            var client = new ClientProcessInfo(id, role, parameters[2]);
                            systemConfig.Clients.Add(client);
                            systemConfig.Processes.Add(client);
                        }

                        break;

                    case "S": // Number of time slots
                        systemConfig.NumberOfTimeSlots = int.Parse(parameters[0]);
                        break;

                    case "D": // Duration of time slots in milliseconds
                        systemConfig.TimeSlotDuration = int.Parse(parameters[0]);
                        break;

                    case "T": // Global wall time of the first slot
                        systemConfig.WallTime = DateTime.Parse(parameters[0]);
                        break;

                    case "F": // Suspected nodes during time slots
                        var slotNumber = int.Parse(parameters[0]);
                        if (systemConfig.ServerProcesses == null)
                            throw new Exception("Server processes not initialized");

                        for (var i = 0; i < systemConfig.ServerProcesses.Count; i++)
                            systemConfig.ServerProcesses[i].TimeSlotStatusList
                                .Add(new TimeSlotStatus(slotNumber, parameters[1 + i]));

                        var suspicions = parameters.Skip(1 + systemConfig.ServerProcesses.Count).ToList();

                        systemConfig.TimeSlotSuspicionsList.Add(new TimeSlotSuspicions(slotNumber, suspicions
                            .Select(s => s.Trim('(', ')'))
                            .Select(s => new Suspicion(
                                suspect: s.Split(',')[0],
                                suspected: s.Split(',')[1])
                            )
                            .ToList()));

                        break;
                }
            }
        }

        systemConfig.ServerProcesses.ForEach(server =>
        {
            server.TimeSlotStatusList.Sort((a, b) => a.TimeSlot.CompareTo(b.TimeSlot));
        });

        return systemConfig;
    }
}