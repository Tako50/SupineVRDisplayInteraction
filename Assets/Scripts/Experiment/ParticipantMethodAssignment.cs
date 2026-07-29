using System;

/// <summary>
/// Assigns one of the three between-participant interaction methods with
/// reproducible permuted blocks. Every consecutive block of three participant
/// numbers contains each method exactly once.
/// </summary>
public static class ParticipantMethodAssignment
{
    public const int MethodsPerBlock = 3;
    public const int DefaultAllocationSeed = 20260725;

    public readonly struct Assignment
    {
        public Assignment(
            int participantNumber,
            int blockNumber,
            int slotInBlock,
            int allocationSeed,
            InteractionCondition condition)
        {
            ParticipantNumber = participantNumber;
            BlockNumber = blockNumber;
            SlotInBlock = slotInBlock;
            AllocationSeed = allocationSeed;
            Condition = condition;
        }

        public int ParticipantNumber { get; }
        public int BlockNumber { get; }
        public int SlotInBlock { get; }
        public int AllocationSeed { get; }
        public InteractionCondition Condition { get; }
        public string ParticipantId => $"P{ParticipantNumber:000}";
        public string MethodCode => GetMethodCode(Condition);
        public string ShortAllocationCode =>
            $"PB3-B{BlockNumber:000}-S{SlotInBlock}-{MethodCode}";
        public string AllocationCode =>
            $"{ShortAllocationCode}-Seed{AllocationSeed}";
    }

    public static Assignment GetAssignment(int participantNumber, int allocationSeed)
    {
        if (participantNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(participantNumber),
                participantNumber,
                "Participant number must be greater than zero.");
        }

        int zeroBasedParticipant = participantNumber - 1;
        int zeroBasedBlock = zeroBasedParticipant / MethodsPerBlock;
        int zeroBasedSlot = zeroBasedParticipant % MethodsPerBlock;
        InteractionCondition[] methods =
        {
            InteractionCondition.RaycastBaseline,
            InteractionCondition.GazeRay,
            InteractionCondition.ExplicitDisplayFocus
        };

        Random random = new Random(BuildBlockSeed(allocationSeed, zeroBasedBlock));
        for (int i = methods.Length - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            InteractionCondition temporary = methods[i];
            methods[i] = methods[swapIndex];
            methods[swapIndex] = temporary;
        }

        return new Assignment(
            participantNumber,
            zeroBasedBlock + 1,
            zeroBasedSlot + 1,
            allocationSeed,
            methods[zeroBasedSlot]);
    }

    private static int BuildBlockSeed(int allocationSeed, int zeroBasedBlock)
    {
        unchecked
        {
            int hash = allocationSeed;
            hash = (hash * 397) ^ zeroBasedBlock;
            hash ^= (zeroBasedBlock + 1) * 486187739;
            return hash;
        }
    }

    internal static string GetMethodCode(InteractionCondition condition)
    {
        switch (condition)
        {
            case InteractionCondition.GazeRay:
                return "GAZERAY";
            case InteractionCondition.ExplicitDisplayFocus:
                return "GAZESTICK";
            default:
                return "RAY";
        }
    }
}
