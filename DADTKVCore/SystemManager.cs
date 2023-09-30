namespace DADTKV
{
    class SystemManager
    {
        // Entry point for the DADTKV system
        // Arguments: systemConfigFilePath
        private static void Main(string[] args)
        {
            // Read the system configuration file
            string configurationFile = Path.Combine(Environment.CurrentDirectory,
                "/IST/1st Semester/DAD/ist-meic-dad-g05/DADTKVCore/configurations/configuration_sample.txt"); // args[0];
            var configuration = SystemConfiguration.ReadSystemConfiguration(configurationFile);

            // Start DADTKV servers (Transaction Managers, Lease Managers)
            StartServers(configuration);

            // Wait for user input to shut down the system
            Console.WriteLine("Press Enter to shut down the DADTKV system.");
            Console.ReadLine();

            // Stop DADTKV servers gracefully
            StopServers();
        }


        private static void StartServers(SystemConfiguration? config)
        {
            // Start server processes based on configuration
            foreach (var process in config?.Processes.Where(process => process.Role is "T" or "L")!)
            {
                // Start the server process using process.ID and process.URL
                Console.WriteLine($"Starting {process.Role} {process.Id} at {process.URL}");
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

        // Class to represent the system configuration
    }
}