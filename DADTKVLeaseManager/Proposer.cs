using DADTKVCore;
using Grpc.Core;

namespace DADTKV;

public class Proposer : LeaseService.LeaseServiceBase
{
    private readonly object _lockObject;
    private readonly List<ILeaseRequest> _leaseRequests;
    private readonly ConsensusState _consensusState;
    private readonly LeaseManagerConfiguration _leaseManagerConfiguration;

    private readonly List<AcceptorService.AcceptorServiceClient> _acceptorServiceServiceClients;

    private readonly UrBroadcaster<LearnRequest, LearnResponse, LearnerService.LearnerServiceClient> _urBroadcaster;
    private ulong _proposalNumber;

    public Proposer(
        object lockObject,
        List<ILeaseRequest> leaseRequests,
        ConsensusState consensusState,
        List<AcceptorService.AcceptorServiceClient> acceptorServiceClients,
        List<LearnerService.LearnerServiceClient> learnerServiceClients,
        LeaseManagerConfiguration leaseManagerConfiguration
    )
    {
        _lockObject = lockObject;
        _leaseRequests = leaseRequests;
        _consensusState = consensusState;
        _acceptorServiceServiceClients = acceptorServiceClients;
        _leaseManagerConfiguration = leaseManagerConfiguration;
        _proposalNumber =
            (ulong)_leaseManagerConfiguration.LeaseManagers.IndexOf(_leaseManagerConfiguration.ProcessInfo) + 1;
        _urBroadcaster =
            new UrBroadcaster<LearnRequest, LearnResponse, LearnerService.LearnerServiceClient>(learnerServiceClients);
    }

    public void Start()
    {
        // TODO ADD LOCK

        var roundNumber = _consensusState.Values.Count;

        if (_leaseRequests.Count == 0)
        {
            return;
        }

        ConsensusValue? previouslyConsensusValue = null;

        var myProposalValue = previouslyConsensusValue?.DeepCopy() ?? new ConsensusValue();

        var leaseQueue = myProposalValue.LeaseQueues;

        foreach (var currentRequest in _leaseRequests)
        {
            switch (currentRequest)
            {
                case LeaseRequest leaseRequest:
                    HandleLeaseRequest(leaseQueue, leaseRequest);
                    break;
                case FreeLeaseRequest freeLeaseRequest:
                    HandleFreeLeaseRequest(leaseQueue, freeLeaseRequest);
                    break;
            }
        }

        Propose(myProposalValue);

        /*const int timeDelta = 1000;
        var timer = new System.Timers.Timer(timeDelta);

        timer.Elapsed += (source, e) =>
        {
            if (_leaseRequests.Count == 0)
            {
                timer.Start();
                return;
            }

            // TODO: Place LeaseServiceImpl callbacks and process requests in a proposer class
            ProcessRequests();
            timer.Start();
        };
        timer.AutoReset = false;
        timer.Start();*/
    }

    public override Task<LeaseResponse> RequestLease(LeaseRequest request, ServerCallContext context)
    {
        lock (_lockObject)
        {
            _leaseRequests.Add(request);

            return Task.FromResult(new LeaseResponse { Ok = true });
        }
    }

    public override Task<FreeLeaseResponse> FreeLease(FreeLeaseRequest request, ServerCallContext context)
    {
        lock (_lockObject)
        {
            _leaseRequests.Add(request);
            return Task.FromResult(new FreeLeaseResponse() { Ok = true });
        }
    }

    private void Propose(ConsensusValue myProposalValue)
    {
        lock (_lockObject)
        {
            // TODO: This is sus, we are always the leader?
            var currentIsLeader = _leaseManagerConfiguration.GetLeaderId(_proposalNumber) ==
                                  _leaseManagerConfiguration.ProcessInfo.Id;

            //TODO: Verify edge case where leader is crashed but not suspected

            if (!currentIsLeader)
                return;

            // broadcast prepare
            var asyncTasks = new List<Task<PrepareResponse>>();
            foreach (var acceptorServiceServiceClient in _acceptorServiceServiceClients)
            {
                var prepareRequest = new PrepareRequest { ProposalNumber = _proposalNumber };
                var res = acceptorServiceServiceClient.PrepareAsync(prepareRequest);
                asyncTasks.Add(res.ResponseAsync);
            }

            var highestWriteTimestamp = 0UL;
            ConsensusValueDto? adoptedValue = null;

            var majorityPromised = DADTKVUtils.WaitForMajority(
                asyncTasks,
                (res) =>
                {
                    if (!res.Promise) return false;

                    if (res.WriteTimestamp == 0)
                        return true;

                    if (res.WriteTimestamp <= highestWriteTimestamp)
                        return true;

                    highestWriteTimestamp = res.WriteTimestamp;
                    adoptedValue = res.Value;

                    return true;
                }
            );

            if (!majorityPromised)
            {
                _proposalNumber += (ulong)_leaseManagerConfiguration.LeaseManagers.Count;
                Propose(myProposalValue);
                return;
            }

            var majorityAccepted = ActOnPromiseMajority(adoptedValue == null
                ? myProposalValue
                : ConsensusValueDtoConverter.ConvertFromDto(adoptedValue));

            if (!majorityAccepted)
            {
                _proposalNumber += (ulong)_leaseManagerConfiguration.LeaseManagers.Count;
                Propose(myProposalValue);
            }
        }
    }

    private bool ActOnPromiseMajority(ConsensusValue proposalValue)
    {
        var acceptCalls = new List<Task<AcceptResponse>>();
        _acceptorServiceServiceClients.ForEach(client =>
        {
            var acceptReq = new AcceptRequest
            {
                ProposalNumber = _proposalNumber,
                Value = ConsensusValueDtoConverter.ConvertToDto(proposalValue),
            };

            var res = client.AcceptAsync(acceptReq);
            acceptCalls.Add(res.ResponseAsync);
        });

        var majority = DADTKVUtils.WaitForMajority(acceptCalls, (res) => res.Accepted);

        if (majority)
            Decide(proposalValue);

        return majority;
    }

    private void Decide(ConsensusValue newConsensusValue)
    {
        var request = new LearnRequest
        {
            ServerId = _leaseManagerConfiguration.ProcessInfo.Id,
            ConsensusValue = ConsensusValueDtoConverter.ConvertToDto(newConsensusValue),
            RoundNumber = _roundNumber
        };

        _urBroadcaster.UrBroadcast(
            request,
            (req, seqNum) => req.SequenceNum = seqNum,
            (req) => { /* TODO Update the consensus round value here too? */},
            (client, req) => client.LearnAsync(req).ResponseAsync
        );
    }

    private static void HandleFreeLeaseRequest(IReadOnlyDictionary<string, Queue<LeaseId>> leaseQueues,
        FreeLeaseRequest freeLeaseRequest)
    {
        var leaseId = LeaseIdDtoConverter.ConvertFromDto(freeLeaseRequest.LeaseId);

        foreach (var (key, queue) in leaseQueues)
        {
            if (queue.Peek().Equals(leaseId))
                queue.Dequeue();
        }
    }

    private static void HandleLeaseRequest(IDictionary<string, Queue<LeaseId>> leaseQueue, LeaseRequest leaseRequest)
    {
        var leaseId = LeaseIdDtoConverter.ConvertFromDto(leaseRequest.LeaseId);

        foreach (var leaseKey in leaseRequest.Set)
        {
            if (!leaseQueue.ContainsKey(leaseKey))
                leaseQueue.Add(leaseKey, new Queue<LeaseId>());

            //TODO: ignore request? send not okay to transaction manager?
            if (!leaseQueue[leaseKey].Contains(leaseId))
                leaseQueue[leaseKey].Enqueue(leaseId);
        }
    }
}