namespace DADTKV;

// TODO: Maybe create package Consensus (com subpackages LeaseId, ConsensusValue e file ConsensusState)

/// <summary>
/// The state of the consensus algorithm.
/// Contains the list of values that have been agreed upon, for each round of the algorithm.
/// </summary>
public class ConsensusState
{
    public readonly List<ConsensusValue?> Values = new();
}