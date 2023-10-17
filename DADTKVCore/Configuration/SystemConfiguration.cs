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

        Duration = systemConfiguration.Duration;
        Slots = systemConfiguration.Slots;
        WallTime = systemConfiguration.WallTime;
    }

    protected List<IProcessInfo> Processes { get; } = new();
    public readonly List<ServerProcessInfo> ServerProcesses = new();
    public readonly List<ServerProcessInfo> LeaseManagers = new();
    public readonly List<ServerProcessInfo> TransactionManagers = new();
    public readonly List<ClientProcessInfo> Clients = new();

    private int Slots { get; set; }
    private int Duration { get; set; }
    private DateTime WallTime { get; set; }

    private Dictionary<int, List<Tuple<string, string>>> Suspicions { get; } = new();

    protected IEnumerable<Tuple<string, string>> CurrentSuspicions
    {
        get
        {
            var currentTimeSlot = (int)Math.Floor((DateTime.Now - WallTime).TotalMilliseconds / Duration) + 1;
            var suspicion = Suspicions.ContainsKey(currentTimeSlot) ? Suspicions[currentTimeSlot] : null;

            // Get previous non null timeslot
            // TODO: Improve
            while (suspicion == null && currentTimeSlot > 0)
            {
                currentTimeSlot--;
                suspicion = Suspicions.ContainsKey(currentTimeSlot) ? Suspicions[currentTimeSlot] : null;
            }

            return suspicion ?? new List<Tuple<string, string>>();
        }
    }

    /// <summary>
    ///     Gets the lease manager Id number.
    /// </summary>
    /// <param name="id">The id of the lease manager.</param>
    /// <returns>The lease manager Id number.</returns>
    protected int GetLeaseManagerIdNum(string id)
    {
        return LeaseManagers.FindIndex(proc => proc.Id.Equals(id)) + 1;
    }

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

                List<IProcessInfo>? serverProcesses = null;

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
                        systemConfig.Slots = int.Parse(parameters[0]);
                        break;

                    case "D": // Duration of time slots in milliseconds
                        systemConfig.Duration = int.Parse(parameters[0]);
                        break;

                    case "T": // Global wall time of the first slot
                        systemConfig.WallTime = DateTime.Parse(parameters[0]);
                        break;

                    case "F": // Suspected nodes during time slots
                        var slotNumber = int.Parse(parameters[0]);
                        if (serverProcesses == null)
                            throw new Exception("Server processes not initialized");

                        for (var i = 0; i < serverProcesses.Count; i++)
                            serverProcesses[i].SlotStatus[slotNumber] = parameters[1 + i];

                        var suspicions = parameters.Skip(1 + serverProcesses.Count).ToList();

                        systemConfig.Suspicions[slotNumber] = suspicions
                            .Select(s => s.Trim('(', ')'))
                            .Select(s => new Tuple<string, string>(
                                s.Split(',')[0],
                                s.Split(',')[1])
                            ).ToList();
                        break;
                }
            }
        }

        return systemConfig;
    }
}