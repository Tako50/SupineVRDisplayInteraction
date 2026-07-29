using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 最新T1設計の24組合せ/周を、再現可能な制約付きランダム順で生成する。
/// 各周は 2 displays x 6 positions x 2 sizes をちょうど1回ずつ含む。
/// </summary>
public static class T1TrialSequenceGenerator
{
    public const int PositionsPerDisplay = 6;
    public const int CombinationsPerCycle = 24;
    public const int MainCycles = 4;
    public const int MainTrialsPerBlock = CombinationsPerCycle * MainCycles;

    private static readonly float[] XPositions = { 0.20f, 0.50f, 0.80f };
    private static readonly float[] YPositions = { 0.20f, 0.80f };
    private static readonly T1TargetSize[] Sizes = { T1TargetSize.Small, T1TargetSize.Large };

    public static List<FocusPointingTrialConfig> GenerateMainBlock(
        T1PointingTask task,
        InteractionCondition condition,
        string display1Id,
        string display2Id,
        int seed)
    {
        return Generate(task, condition, display1Id, display2Id, seed, MainCycles, MainTrialsPerBlock);
    }

    public static List<FocusPointingTrialConfig> GenerateTrainingBlock(
        T1PointingTask task,
        InteractionCondition condition,
        string display1Id,
        string display2Id,
        int seed,
        int requestedTrialCount)
    {
        int count = Mathf.Clamp(requestedTrialCount, 6, 12);
        return Generate(task, condition, display1Id, display2Id, seed, 1, count);
    }

    private static List<FocusPointingTrialConfig> Generate(
        T1PointingTask task,
        InteractionCondition condition,
        string display1Id,
        string display2Id,
        int seed,
        int cycleCount,
        int outputCount)
    {
        List<FocusPointingTrialConfig> result = new List<FocusPointingTrialConfig>();
        string previousDisplay = string.Empty;
        int previousPositionId = -1;
        List<FocusPointingTrialConfig> previousCycle = null;

        for (int cycle = 1; cycle <= cycleCount; cycle++)
        {
            List<FocusPointingTrialConfig> pool = BuildCyclePool(task, condition, display1Id, display2Id, cycle, seed);
            List<FocusPointingTrialConfig> ordered = FindBestOrder(
                pool,
                seed + cycle * 7919,
                previousDisplay,
                previousPositionId);

            if (previousCycle != null && HasSameCombinationOrder(previousCycle, ordered))
            {
                List<FocusPointingTrialConfig> rotated =
                    new List<FocusPointingTrialConfig>(ordered);
                Rotate(rotated, 5);
                if (!HasConsecutiveSameTargetPosition(
                        rotated,
                        previousDisplay,
                        previousPositionId))
                {
                    ordered = rotated;
                }
            }

            for (int i = 0; i < ordered.Count && result.Count < outputCount; i++)
            {
                FocusPointingTrialConfig trial = ordered[i];
                trial.previousDisplay = previousDisplay;
                trial.previousPositionId = previousPositionId;
                trial.transitionDirection = string.IsNullOrEmpty(previousDisplay)
                    ? "Start"
                    : previousDisplay == trial.targetDisplayId
                        ? "SameDisplay"
                        : previousDisplay + "To" + trial.targetDisplayId;
                trial.transitionType = string.IsNullOrEmpty(previousDisplay)
                    ? "Start"
                    : previousDisplay == trial.targetDisplayId ? "WithinDisplay" : "BetweenDisplays";
                trial.occlusionPrevType = result.Count > 0
                    ? (result[result.Count - 1].inputOccluded ? "inputOccluded" : "none")
                    : string.Empty;

                result.Add(trial);
                previousDisplay = trial.targetDisplayId;
                previousPositionId = trial.positionId;
            }

            previousCycle = ordered;
        }

        if (HasConsecutiveSameTargetPosition(result, string.Empty, -1))
        {
            throw new InvalidOperationException(
                "T1 trial generation produced consecutive trials at the same display position.");
        }

        for (int i = 0; i < result.Count; i++)
        {
            FocusPointingTrialConfig trial = result[i];
            trial.sourceTrialIndex = i + 1;
            trial.trialSetId = i + 1;
            trial.trialIndexInSet = i + 1;
            trial.sequenceSeed = seed;
            trial.repetition = trial.cycleIndex;
        }

        return result;
    }

    private static List<FocusPointingTrialConfig> BuildCyclePool(
        T1PointingTask task,
        InteractionCondition condition,
        string display1Id,
        string display2Id,
        int cycleIndex,
        int sequenceSeed)
    {
        List<FocusPointingTrialConfig> pool = new List<FocusPointingTrialConfig>(CombinationsPerCycle);
        string[] displayIds = { display1Id, display2Id };

        for (int displayIndex = 0; displayIndex < displayIds.Length; displayIndex++)
        {
            bool isDisplay2 = displayIndex == 1;
            int positionId = 0;
            for (int row = 0; row < YPositions.Length; row++)
            {
                for (int column = 0; column < XPositions.Length; column++)
                {
                    positionId++;
                    for (int sizeIndex = 0; sizeIndex < Sizes.Length; sizeIndex++)
                    {
                        T1TargetSize size = Sizes[sizeIndex];
                        bool inputOccluded = task == T1PointingTask.TaskC_FrontBackOccluded
                            && isDisplay2
                            && Mathf.Approximately(YPositions[row], 0.80f);
                        pool.Add(new FocusPointingTrialConfig
                        {
                            task = task,
                            condition = condition,
                            conditionName = condition.ToString(),
                            sourcePhase = "main",
                            cycleIndex = cycleIndex,
                            positionId = positionId,
                            targetDisplayId = displayIds[displayIndex],
                            targetPositionId = $"D{displayIndex + 1}_C{column + 1}_R{row + 1}_{size}",
                            targetNormalizedPosition = new Vector2(XPositions[column], YPositions[row]),
                            targetSize = size,
                            targetSizeDegrees = size == T1TargetSize.Small ? 1.5f : 3f,
                            inputOccluded = inputOccluded,
                            occlusionType = inputOccluded
                                ? T1OcclusionType.BackOccluded
                                : isDisplay2 ? T1OcclusionType.BackClear : T1OcclusionType.Front,
                            sequenceSeed = sequenceSeed
                        });
                    }
                }
            }
        }

        return pool;
    }

    private static List<FocusPointingTrialConfig> FindBestOrder(
        List<FocusPointingTrialConfig> source,
        int seed,
        string previousDisplay,
        int previousPositionId)
    {
        const int candidateCount = 1500;
        System.Random random = new System.Random(seed);
        List<FocusPointingTrialConfig> best = null;
        int bestScore = int.MaxValue;

        for (int attempt = 0; attempt < candidateCount; attempt++)
        {
            List<FocusPointingTrialConfig> candidate = new List<FocusPointingTrialConfig>(source);
            Shuffle(candidate, random);
            if (HasConsecutiveSameTargetPosition(
                    candidate,
                    previousDisplay,
                    previousPositionId))
            {
                continue;
            }

            int score = Score(candidate, previousDisplay);
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
                if (score == 0)
                {
                    break;
                }
            }
        }

        if (best == null)
        {
            throw new InvalidOperationException(
                $"Could not generate a T1 order without consecutive repeated target positions. seed={seed}");
        }

        return best;
    }

    private static int Score(List<FocusPointingTrialConfig> trials, string previousDisplay)
    {
        int d1ToD2 = 0;
        int d2ToD1 = 0;
        int sameDisplay = 0;
        int betweenDisplays = 0;
        int runLength = 0;
        int sizeRunLength = 0;
        int score = 0;
        string lastDisplay = previousDisplay;
        T1TargetSize? lastSize = null;

        for (int i = 0; i < trials.Count; i++)
        {
            FocusPointingTrialConfig trial = trials[i];
            if (!string.IsNullOrEmpty(lastDisplay))
            {
                if (lastDisplay == trial.targetDisplayId)
                {
                    sameDisplay++;
                    runLength++;
                }
                else
                {
                    betweenDisplays++;
                    if (lastDisplay.Contains("Display_A") && trial.targetDisplayId.Contains("Display_B"))
                    {
                        d1ToD2++;
                    }
                    else
                    {
                        d2ToD1++;
                    }
                    runLength = 1;
                }
            }
            else
            {
                runLength = 1;
            }

            if (runLength > 2)
            {
                score += (runLength - 2) * 12;
            }

            if (lastSize.HasValue && lastSize.Value == trial.targetSize)
            {
                sizeRunLength++;
                if (sizeRunLength > 3)
                {
                    score += (sizeRunLength - 3) * 3;
                }
            }
            else
            {
                sizeRunLength = 1;
            }

            if (trial.inputOccluded && i > 0 && trials[i - 1].inputOccluded)
            {
                score += 14;
            }

            lastDisplay = trial.targetDisplayId;
            lastSize = trial.targetSize;
        }

        score += Mathf.Abs(d1ToD2 - d2ToD1) * 20;
        score += Mathf.Abs(sameDisplay - betweenDisplays) * 2;

        for (int quarter = 0; quarter < 4; quarter++)
        {
            int start = quarter * trials.Count / 4;
            int end = (quarter + 1) * trials.Count / 4;
            int count = 0;
            for (int i = start; i < end; i++)
            {
                if (trials[i].inputOccluded)
                {
                    count++;
                }
            }
            if (count > 0)
            {
                score += Mathf.Abs(count * 2 - 3) * 2;
            }
        }

        return score;
    }

    private static bool HasConsecutiveSameTargetPosition(
        IReadOnlyList<FocusPointingTrialConfig> trials,
        string previousDisplay,
        int previousPositionId)
    {
        string lastDisplay = previousDisplay;
        int lastPositionId = previousPositionId;

        for (int i = 0; i < trials.Count; i++)
        {
            FocusPointingTrialConfig trial = trials[i];
            if (!string.IsNullOrEmpty(lastDisplay)
                && lastDisplay == trial.targetDisplayId
                && lastPositionId == trial.positionId)
            {
                return true;
            }

            lastDisplay = trial.targetDisplayId;
            lastPositionId = trial.positionId;
        }

        return false;
    }

    private static void Shuffle<T>(List<T> list, System.Random random)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            T value = list[i];
            list[i] = list[j];
            list[j] = value;
        }
    }

    private static bool HasSameCombinationOrder(
        List<FocusPointingTrialConfig> first,
        List<FocusPointingTrialConfig> second)
    {
        if (first == null || second == null || first.Count != second.Count)
        {
            return false;
        }

        for (int i = 0; i < first.Count; i++)
        {
            if (first[i].targetDisplayId != second[i].targetDisplayId
                || first[i].positionId != second[i].positionId
                || first[i].targetSize != second[i].targetSize)
            {
                return false;
            }
        }

        return true;
    }

    private static void Rotate<T>(List<T> list, int offset)
    {
        if (list == null || list.Count == 0)
        {
            return;
        }

        offset = ((offset % list.Count) + list.Count) % list.Count;
        if (offset == 0)
        {
            return;
        }

        List<T> copy = new List<T>(list);
        for (int i = 0; i < list.Count; i++)
        {
            list[i] = copy[(i + offset) % copy.Count];
        }
    }
}
