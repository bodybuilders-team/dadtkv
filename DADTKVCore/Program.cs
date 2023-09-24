namespace DADTKV
{
    class Program
    {
        // Entry point for the DADTKV system
        // Arguments: systemConfigFilePath
        private static void Main(string[] args)
        {
            // Read the system configuration file
            const string configurationFile = "system_config.txt"; // args[0];
            var configuration = ReadSystemConfiguration(configurationFile);

            // Start DADTKV servers (Transaction Managers, Lease Managers)
            StartServers(configuration);

            // Wait for user input to shut down the system
            Console.WriteLine("Press Enter to shut down the DADTKV system.");
            Console.ReadLine();

            // Stop DADTKV servers gracefully
            StopServers();
        }

        private static SystemConfiguration? ReadSystemConfiguration(string filePath)
        {
            try
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
                            case "P": // Process identifier and role (Server or Client)
                                var process = new ProcessInfo
                                {
                                    ID = parameters[0],
                                    Role = parameters[1]
                                };
                                if (parameters.Length > 2)
                                    process.URL = parameters[2];
                                systemConfig.Processes.Add(process);
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
                                var suspectedNodeIDs = parameters.Skip(1).ToList();
                                systemConfig.SuspectedNodes[slotNumber] = suspectedNodeIDs;
                                break;
                        }
                    }
                }

                return systemConfig;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading system configuration: {ex.Message}");
                return null;
            }
        }

        private static void StartServers(SystemConfiguration? config)
        {
            // Start server processes based on configuration
            foreach (var process in config?.Processes.Where(process => process.Role == "Server")!)
            {
                // Start the server process using process.ID and process.URL
                Console.WriteLine($"Starting {process.Role} {process.ID} at {process.URL}");
                // Implement the logic to start server processes
            }
        }

        private static void StopServers()
        {
            // Implement logic to gracefully stop server processes
            Console.WriteLine("Shutting down servers gracefully...");
            // You can send shutdown signals or terminate server processes here
        }

        // Class to represent a single process (Server or Client)
        public class ProcessInfo
        {
            public string ID { get; set; }
            public string Role { get; set; }
            public string URL { get; set; }
        }

        // Class to represent the system configuration
        public class SystemConfiguration
        {
            public List<ProcessInfo> Processes { get; } = new List<ProcessInfo>();
            public int Slots { get; set; }
            public int Duration { get; set; }
            public DateTime WallTime { get; set; }
            public Dictionary<int, List<string>> SuspectedNodes { get; } = new Dictionary<int, List<string>>();
        }
    }
}