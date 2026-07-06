#!/usr/bin/env python3
"""Prototype analyzer for T1 target-selection CSV logs.

Input:
  Logs/Quest/t1_results_*.csv by default, or explicit files/directories.

Output:
  Analysis/T1/cleaned_attempts.csv
  Analysis/T1/cleaned_completed_trials.csv
  Analysis/T1/summary_by_condition.csv
  Analysis/T1/summary_by_condition_display.csv
  Analysis/T1/summary_by_condition_occlusion.csv
"""

from __future__ import annotations

import argparse
import csv
import math
from collections import defaultdict
from pathlib import Path
from statistics import mean, median, stdev


EXPECTED_COLUMNS = [
    "participantId",
    "sessionId",
    "taskPhase",
    "conditionName",
    "interactionCondition",
    "t1Task",
    "taskOrder",
    "methodOrder",
    "layoutPreset",
    "sequenceSeed",
    "trialIndexInCondition",
    "globalTrialIndex",
    "trialSetId",
    "trialIndexInSet",
    "occlusion_type",
    "targetDisplayId",
    "targetPositionId",
    "targetLocalX",
    "targetLocalY",
    "targetSize",
    "attemptIndex",
    "clickedDisplayId",
    "clickedLocalX",
    "clickedLocalY",
    "resultType",
    "isCorrect",
    "displayError",
    "targetError",
    "miss",
    "advancesTrial",
    "trialStartTime",
    "clickTime",
    "responseTime",
    "controllerMovementMeters",
    "controllerRotationDegrees",
    "timestamp",
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Analyze T1 target-selection CSV logs.")
    parser.add_argument(
        "inputs",
        nargs="*",
        default=["Logs/Quest"],
        help="CSV files or directories. Directories are searched for t1_results_*.csv.",
    )
    parser.add_argument(
        "--out",
        default="Analysis/T1",
        help="Output directory for cleaned and summary CSV files.",
    )
    parser.add_argument(
        "--phase",
        default="MainTask",
        help="Task phase to analyze. Use 'all' to include Training and MainTask.",
    )
    return parser.parse_args()


def find_input_files(inputs: list[str]) -> list[Path]:
    files: list[Path] = []
    for item in inputs:
        path = Path(item)
        if path.is_dir():
            files.extend(sorted(path.rglob("t1_results_*.csv")))
        elif path.is_file():
            files.append(path)
        else:
            matches = sorted(Path().glob(item))
            files.extend(match for match in matches if match.is_file())
    return sorted(dict.fromkeys(files))


def read_rows(files: list[Path], phase: str) -> list[dict[str, str]]:
    rows: list[dict[str, str]] = []
    for file_path in files:
        with file_path.open("r", newline="", encoding="utf-8-sig") as handle:
            reader = csv.DictReader(handle)
            for row in reader:
                if phase != "all" and row.get("taskPhase") != phase:
                    continue
                normalized = normalize_row(row)
                normalized["sourceFile"] = str(file_path)
                rows.append(normalized)
    return rows


def normalize_row(row: dict[str, str]) -> dict[str, str]:
    normalized = {column: row.get(column, "") for column in EXPECTED_COLUMNS}
    if not normalized.get("occlusion_type"):
        normalized["occlusion_type"] = row.get("occlusionType", "")
    legacy_occlusion = normalized.get("occlusion_type", "")
    if legacy_occlusion in {"Front", "BackClear"}:
        normalized["occlusion_type"] = "none"
    elif legacy_occlusion == "BackOccluded":
        normalized["occlusion_type"] = "inputOccluded"
    return normalized


def to_float(value: str) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return math.nan


def to_int(value: str) -> int:
    try:
        return int(float(value))
    except (TypeError, ValueError):
        return 0


def flag(row: dict[str, str], key: str) -> bool:
    return row.get(key, "").strip().lower() in {"1", "true", "yes"}


def write_csv(path: Path, rows: list[dict[str, object]], fieldnames: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(rows)


def clean_attempt_rows(rows: list[dict[str, str]]) -> list[dict[str, object]]:
    cleaned: list[dict[str, object]] = []
    for row in rows:
        cleaned.append(
            {
                **row,
                "trialIndexInCondition": to_int(row["trialIndexInCondition"]),
                "globalTrialIndex": to_int(row["globalTrialIndex"]),
                "trialSetId": to_int(row["trialSetId"]),
                "trialIndexInSet": to_int(row["trialIndexInSet"]),
                "targetLocalX": to_float(row["targetLocalX"]),
                "targetLocalY": to_float(row["targetLocalY"]),
                "targetSize": to_float(row["targetSize"]),
                "attemptIndex": to_int(row["attemptIndex"]),
                "clickedLocalX": to_float(row["clickedLocalX"]),
                "clickedLocalY": to_float(row["clickedLocalY"]),
                "isCorrect": int(flag(row, "isCorrect")),
                "displayError": int(flag(row, "displayError")),
                "targetError": int(flag(row, "targetError")),
                "miss": int(flag(row, "miss")),
                "advancesTrial": int(flag(row, "advancesTrial")),
                "trialStartTime": to_float(row["trialStartTime"]),
                "clickTime": to_float(row["clickTime"]),
                "responseTime": to_float(row["responseTime"]),
                "controllerMovementMeters": to_float(row["controllerMovementMeters"]),
                "controllerRotationDegrees": to_float(row["controllerRotationDegrees"]),
            }
        )
    return cleaned


def completed_trial_rows(cleaned_attempts: list[dict[str, object]]) -> list[dict[str, object]]:
    return [row for row in cleaned_attempts if row.get("advancesTrial") == 1]


def summarize(rows: list[dict[str, object]], group_keys: list[str]) -> list[dict[str, object]]:
    grouped: dict[tuple[object, ...], list[dict[str, object]]] = defaultdict(list)
    for row in rows:
        grouped[tuple(row.get(key, "") for key in group_keys)].append(row)

    summaries: list[dict[str, object]] = []
    for key_values, group_rows in sorted(grouped.items(), key=lambda item: item[0]):
        attempts = len(group_rows)
        completed = [row for row in group_rows if row.get("advancesTrial") == 1]
        correct = sum(int(row.get("isCorrect", 0)) for row in group_rows)
        display_errors = sum(int(row.get("displayError", 0)) for row in group_rows)
        target_errors = sum(int(row.get("targetError", 0)) for row in group_rows)
        misses = sum(int(row.get("miss", 0)) for row in group_rows)
        completion_times = [float(row["responseTime"]) for row in completed if not math.isnan(float(row["responseTime"]))]
        movement = [float(row["controllerMovementMeters"]) for row in completed if not math.isnan(float(row["controllerMovementMeters"]))]
        rotation = [float(row["controllerRotationDegrees"]) for row in completed if not math.isnan(float(row["controllerRotationDegrees"]))]
        attempts_per_completed = attempts / len(completed) if completed else math.nan

        summary = {group_keys[i]: key_values[i] for i in range(len(group_keys))}
        summary.update(
            {
                "nAttempts": attempts,
                "nCompletedTrials": len(completed),
                "nCorrectAttempts": correct,
                "accuracyPerAttempt": ratio(correct, attempts),
                "displayErrorRate": ratio(display_errors, attempts),
                "targetErrorRate": ratio(target_errors, attempts),
                "missRate": ratio(misses, attempts),
                "attemptsPerCompletedTrial": round_float(attempts_per_completed),
                "meanResponseTime": round_float(safe_mean(completion_times)),
                "medianResponseTime": round_float(safe_median(completion_times)),
                "sdResponseTime": round_float(safe_stdev(completion_times)),
                "minResponseTime": round_float(min(completion_times) if completion_times else math.nan),
                "maxResponseTime": round_float(max(completion_times) if completion_times else math.nan),
                "meanControllerMovementMeters": round_float(safe_mean(movement)),
                "meanControllerRotationDegrees": round_float(safe_mean(rotation)),
            }
        )
        summaries.append(summary)
    return summaries


def ratio(numerator: int, denominator: int) -> float:
    return round_float(numerator / denominator) if denominator else math.nan


def round_float(value: float) -> float | str:
    if value is None or math.isnan(value):
        return ""
    return round(value, 4)


def safe_mean(values: list[float]) -> float:
    return mean(values) if values else math.nan


def safe_median(values: list[float]) -> float:
    return median(values) if values else math.nan


def safe_stdev(values: list[float]) -> float:
    return stdev(values) if len(values) >= 2 else math.nan


def main() -> int:
    args = parse_args()
    files = find_input_files(args.inputs)
    if not files:
        print("[analyze_t1_results] No t1_results_*.csv files found.")
        return 1

    rows = read_rows(files, args.phase)
    cleaned = clean_attempt_rows(rows)
    completed = completed_trial_rows(cleaned)
    out_dir = Path(args.out)

    cleaned_fields = EXPECTED_COLUMNS + ["sourceFile"]
    write_csv(out_dir / "cleaned_attempts.csv", cleaned, cleaned_fields)
    write_csv(out_dir / "cleaned_completed_trials.csv", completed, cleaned_fields)

    condition_keys = ["participantId", "sessionId", "taskPhase", "t1Task", "interactionCondition"]
    display_keys = condition_keys + ["targetDisplayId"]
    occlusion_keys = condition_keys + ["occlusion_type"]
    summary_fields = condition_keys + [
        "nAttempts",
        "nCompletedTrials",
        "nCorrectAttempts",
        "accuracyPerAttempt",
        "displayErrorRate",
        "targetErrorRate",
        "missRate",
        "attemptsPerCompletedTrial",
        "meanResponseTime",
        "medianResponseTime",
        "sdResponseTime",
        "minResponseTime",
        "maxResponseTime",
        "meanControllerMovementMeters",
        "meanControllerRotationDegrees",
    ]
    display_summary_fields = display_keys + summary_fields[len(condition_keys) :]
    occlusion_summary_fields = occlusion_keys + summary_fields[len(condition_keys) :]

    write_csv(out_dir / "summary_by_condition.csv", summarize(cleaned, condition_keys), summary_fields)
    write_csv(out_dir / "summary_by_condition_display.csv", summarize(cleaned, display_keys), display_summary_fields)
    write_csv(
        out_dir / "summary_by_condition_occlusion.csv",
        summarize(cleaned, occlusion_keys),
        occlusion_summary_fields,
    )

    print(f"[analyze_t1_results] Input files: {len(files)}")
    print(f"[analyze_t1_results] Attempts: {len(cleaned)}")
    print(f"[analyze_t1_results] Completed trials: {len(completed)}")
    print(f"[analyze_t1_results] Wrote: {out_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
