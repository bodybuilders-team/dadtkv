# ist-meic-dad-g05

> Design and Implementation of Distributed Applications project of group 05 - MEIC @ IST 2022/2023.

## Authors

- [110817 André Páscoa](https://github.com/devandrepascoa)
- [110860 André Jesus](https://github.com/andre-j3sus)
- [110893 Nyckollas Brandão](https://github.com/Nyckoka)

Professors: Paolo Romano and João Garcia

@IST<br>
Master in Computer Science and Computer Engineering<br>
Design and Implementation of Distributed Applications - Group 05<br>
Winter Semester of 2023/2024

## Table of Contents

- [Organization Description](#organization-description)
- [How to Run](#how-to-run)
- [Project Situation at the Checkpoint](#project-situation-at-the-checkpoint)
    - [What has been done](#what-has-been-done)
    - [Current issues](#current-issues)
    - [What needs to be done](#what-needs-to-be-done)

---

## Organization Description

The solution to the project is divided into 4 projects:

* **DADTKVClient**: Client application that communicates with the Transaction Managers, using the DADTKVService
  interface.
* **DADTKVTransactionManager**: Transaction Manager application.
* **DADTKVLeaseManager**: Lease Manager application.
* **DADTKVCore**: Contains the interfaces and classes that are common to all the other projects, including the
  configuration of the system. It also contains the System Manager application, which is used to start and shutdown the
  system.

---

## How to Run

The project can be run using the System Manager application, which is located in the DADTKVCore project.

The System Manager only receives one argument, which is the path to the configuration file (relative to the solution). A
configuration file is located in `DADTKVCore/Configuration/configuration_sample.txt`.

To run the system, follow these steps:

1. Open a terminal in the **solution's root directory**.
2. Run `dotnet clean` to clean the solution (if needed).
3. Run `dotnet build` to build the solution.
4. Run `dotnet run --project DADTKVCore/DADTKVCore.csproj <configuration_file_path>` to run the System
   Manager application. For example, as the configuration file is located in the Configuration folder, the command would
   be `dotnet run --project DADTKVCore/DADTKVCore.csproj "./DADTKVCore/Configuration/configuration_sample.txt"`.

---

## Project Situation at the Checkpoint

### What has been done

- Clients implemented:
    * Reads script file and sends transaction requests to a transaction manager.
- Transaction managers:
    * Receives transaction requests from clients and asks lease managers for the leases of that transaction.
    * Upon receiving the leases, executes the transaction, and broadcasts updates to other transaction managers.
    * Sends the read data to the client.
    * Frees the acquired leases in the case there are other conflicting lease requests.
- Lease managers:
    * Receives lease requests and stores them in a list until the next round of consensus in which it is the proposer.
    * If it is, or thinks it is, the leader, it acts as the proposer, starting the round of consensus.
    * Since the lease manager is a learner, it also keeps track of all previous consensus values.
    * Before starting a new round of consensus, it makes sure it has knowledge of all previous consensus values.
    * Lease requests already applied in previous consensus values are removed from the list of lease requests.

### Current issues

- The consensus value is the full current hashmap of lease queues.
- Freeing of leases requires a new consensus round: this means that it's common that only one transaction is executed
  per consensus round.

### What needs to be done

- Change the consensus value to be a list of lease requests to be applied instead of being the full hashmap of queues.
- Consensus should only be reached on the order of lease requests. Notion of freeing of lease requests should only
  happen among transaction managers.
- Improve locking mechanisms.
- Liveness.
- Improve time outs and failure detector logic.
- Implement multi-paxos.
- Fix bug that prevents some transactions from being executed when using multiple clients.