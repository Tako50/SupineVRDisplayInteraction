#!/usr/bin/env python3
"""Validate T1, T2, common-event, and trajectory CSV experiment logs.

The validator is intentionally dependency-free so the same checks can run on the
experiment PC after copying Quest logs. It validates every explicit CSV input, or
recursively discovers current experiment files below the supplied directories.
"""

from __future__ import annotations

import argparse
import csv
import math
import tempfile
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from pathlib import Path


CURRENT_SCHEMA_VERSION = "2"
IDENTITY_ALIASES = {
    "participant": ("participantId", "participant_id"),
    "allocation": ("allocationCode", "allocation_code"),
    "session": ("sessionId", "session_id"),
    "run": ("runId", "run_id"),
    "schema": ("schemaVersion", "schema_version"),
}


@dataclass
class Report:
    checked_files: int = 0
    checked_rows: int = 0
    failures: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)

    def fail(self, path: Path, message: str) -> None:
        self.failures.append(f"{path}: {message}")

    def warn(self, path: Path, message: str) -> None:
        self.warnings.append(f"{path}: {message}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Validate experiment CSV logs.")
    parser.add_argument(
        "inputs",
        nargs="*",
        default=["Logs"],
        help="CSV files or directories to validate recursively.",
    )
    parser.add_argument(
        "--require-complete-t1",
        action="store_true",
        help="Also require the full 288 completed Main trials and T1 balance rules.",
    )
    parser.add_argument(
        "--self-test",
        action="store_true",
        help="Run built-in valid/invalid fixture checks instead of reading logs.",
    )
    return parser.parse_args()


def discover(inputs: list[str]) -> list[Path]:
    files: list[Path] = []
    for raw in inputs:
        path = Path(raw)
        if path.is_file() and path.suffix.lower() == ".csv":
            files.append(path)
        elif path.is_dir():
            files.extend(path.rglob("*.csv"))
        else:
            files.extend(match for match in Path().glob(raw) if match.is_file())
    return sorted(dict.fromkeys(files))


def first_value(row: dict[str, str], names: tuple[str, ...]) -> str:
    for name in names:
        value = row.get(name, "").strip()
        if value:
            return value
    return ""


def classify(header: list[str]) -> str:
    columns = set(header)
    if {"t1Task", "resultType", "targetDisplayId", "responseTime"} <= columns:
        return "t1_results"
    if {"targetOrderList", "cycleIndex", "positionId", "responseTime"} <= columns:
        return "t1_targets"
    if {"task_phase", "result", "duration", "youtube_operation_count"} <= columns:
        return "t2_results"
    if {"event_type", "task_phase", "display_id", "details"} <= columns:
        return "t2_events"
    if {"sampleIndex", "hmdPosX", "gazeNormalizedX", "cursorNormalizedX"} <= columns:
        return "trajectory"
    if {"rowType", "condition", "layoutPresetOrEvent", "participantId"} <= columns:
        return "common"
    if {"post_timeout_final_selection", "selection_delay_seconds"} <= columns:
        return "t2_post_timeout"
    return "unknown"


def to_float(value: str) -> float | None:
    try:
        result = float(value)
    except (TypeError, ValueError):
        return None
    return result if math.isfinite(result) else None


def to_int(value: str) -> int | None:
    number = to_float(value)
    if number is None or not number.is_integer():
        return None
    return int(number)


def to_bool(value: str) -> bool | None:
    normalized = value.strip().lower()
    if normalized in {"1", "true", "yes"}:
        return True
    if normalized in {"0", "false", "no"}:
        return False
    return None


def validate_identity(
    path: Path,
    header: list[str],
    rows: list[dict[str, str]],
    report: Report,
) -> None:
    if not any(name in header for name in IDENTITY_ALIASES["schema"]):
        report.warn(path, "legacy schema: schema-v2 identity checks were skipped")
        return

    for label, aliases in IDENTITY_ALIASES.items():
        if not any(name in header for name in aliases):
            report.fail(path, f"schema v2 identity column is missing: {label}")
            continue
        values: set[str] = set()
        for line_number, row in enumerate(rows, 2):
            value = first_value(row, aliases)
            if not value:
                report.fail(path, f"line {line_number}: {label} is empty")
                break
            values.add(value)
        if len(values) > 1:
            report.fail(
                path,
                f"multiple {label} values are mixed in one file: {sorted(values)}",
            )
    schema_values = {
        first_value(row, IDENTITY_ALIASES["schema"]) for row in rows if row
    }
    schema_values.discard("")
    if schema_values and schema_values != {CURRENT_SCHEMA_VERSION}:
        report.fail(path, f"unexpected schema version(s): {sorted(schema_values)}")


def validate_normalized(
    path: Path,
    rows: list[dict[str, str]],
    pairs: list[tuple[str, str]],
    report: Report,
) -> None:
    for line_number, row in enumerate(rows, 2):
        for x_name, y_name in pairs:
            x_raw = row.get(x_name, "").strip()
            y_raw = row.get(y_name, "").strip()
            if not x_raw and not y_raw:
                continue
            x_value = to_float(x_raw)
            y_value = to_float(y_raw)
            if x_value is None or y_value is None:
                report.fail(path, f"line {line_number}: invalid {x_name}/{y_name}")
            elif not (0.0 <= x_value <= 1.0 and 0.0 <= y_value <= 1.0):
                report.fail(
                    path,
                    f"line {line_number}: {x_name}/{y_name} is outside [0, 1]",
                )


def validate_t1_results(
    path: Path,
    rows: list[dict[str, str]],
    report: Report,
) -> None:
    validate_normalized(
        path,
        rows,
        [("targetLocalX", "targetLocalY"), ("clickedLocalX", "clickedLocalY")],
        report,
    )
    for line_number, row in enumerate(rows, 2):
        start = to_float(row.get("trialStartTime", ""))
        click = to_float(row.get("clickTime", ""))
        response = to_float(row.get("responseTime", ""))
        if None in (start, click, response):
            report.fail(path, f"line {line_number}: invalid trial timing")
        elif click < start or response < 0 or abs((click - start) - response) > 0.02:
            report.fail(path, f"line {line_number}: inconsistent response time")

        flags = {
            name: to_bool(row.get(name, ""))
            for name in ("isCorrect", "displayError", "targetError", "miss", "advancesTrial")
        }
        if any(value is None for value in flags.values()):
            report.fail(path, f"line {line_number}: invalid boolean result field")
            continue
        if flags["isCorrect"] and (
            flags["displayError"] or flags["targetError"] or flags["miss"]
        ):
            report.fail(path, f"line {line_number}: correct attempt is also an error")
        if flags["displayError"] and flags["targetError"]:
            report.fail(path, f"line {line_number}: both error classes are true")


def validate_t1_complete(
    rows: list[tuple[Path, dict[str, str]]],
    report: Report,
) -> None:
    expected_task_counts = {
        "TaskA_LeftRight": 96,
        "TaskB_FrontBackClear": 96,
        "TaskC_FrontBackOccluded": 96,
    }
    grouped: dict[tuple[str, str], list[tuple[Path, dict[str, str]]]] = defaultdict(list)
    for path, row in rows:
        if row.get("taskPhase") != "MainTask" or to_bool(row.get("advancesTrial", "")) is not True:
            continue
        key = (
            first_value(row, IDENTITY_ALIASES["participant"]),
            first_value(row, IDENTITY_ALIASES["run"]),
        )
        grouped[key].append((path, row))

    if not grouped:
        report.fail(Path("<all T1 logs>"), "no completed schema v2 T1 Main run found")
        return

    for key, records in grouped.items():
        label = Path(f"<T1 participant={key[0]} run={key[1]}>")
        if len(records) != 288:
            report.fail(label, f"expected 288 completed trials, found {len(records)}")
        task_counts = Counter(row.get("t1Task", "") for _, row in records)
        if task_counts != expected_task_counts:
            report.fail(label, f"unexpected per-task counts: {dict(task_counts)}")
        occluded = Counter(
            row.get("t1Task", "")
            for _, row in records
            if row.get("occlusion_type") == "inputOccluded"
        )
        if occluded != {"TaskC_FrontBackOccluded": 24}:
            report.fail(label, f"expected 24 TaskC inputOccluded trials: {dict(occluded)}")

        global_indices = [to_int(row.get("globalTrialIndex", "")) for _, row in records]
        if any(index is None for index in global_indices):
            report.fail(label, "globalTrialIndex contains a non-integer value")
        elif sorted(global_indices) != list(range(1, 289)):
            report.fail(label, "globalTrialIndex must cover each value from 1 through 288 exactly once")

        for task_name in expected_task_counts:
            task_rows = [row for _, row in records if row.get("t1Task") == task_name]
            ordered = sorted(
                task_rows,
                key=lambda row: to_int(row.get("trialIndexInCondition", "")) or -1,
            )
            trial_indices = [
                to_int(row.get("trialIndexInCondition", "")) for row in ordered
            ]
            if trial_indices != list(range(1, 97)):
                report.fail(
                    label,
                    f"{task_name} trialIndexInCondition must cover 1 through 96 exactly once",
                )
                continue

            for previous, current in zip(ordered, ordered[1:]):
                same_target_position = (
                    previous.get("targetDisplayId") == current.get("targetDisplayId")
                    and previous.get("positionId") == current.get("positionId")
                )
                if same_target_position:
                    report.fail(
                        label,
                        f"{task_name} repeats the same display and position at "
                        f"trialIndexInCondition={current.get('trialIndexInCondition')}",
                    )


def validate_t2_results(
    path: Path,
    rows: list[dict[str, str]],
    report: Report,
) -> None:
    for line_number, row in enumerate(rows, 2):
        start = to_float(row.get("task_start_time", ""))
        end = to_float(row.get("task_end_time", ""))
        duration = to_float(row.get("duration", ""))
        if None in (start, end, duration) or end < start or duration < 0:
            report.fail(path, f"line {line_number}: invalid T2 timing")
        elif abs((end - start) - duration) > 0.02:
            report.fail(path, f"line {line_number}: inconsistent T2 duration")

        phase = row.get("task_phase", "")
        if phase == "practice":
            minimum = to_int(row.get("minimum_practice_rounds", ""))
            completed = to_int(row.get("completed_practice_rounds", ""))
            steps = to_int(row.get("completed_practice_steps", ""))
            additional = to_int(row.get("additional_practice_steps", ""))
            readiness = to_bool(row.get("readiness_confirmed", ""))
            if None in (minimum, completed, steps, additional, readiness):
                report.fail(path, f"line {line_number}: incomplete Practice summary")
            elif row.get("result") == "completed":
                if completed < minimum or steps < minimum * 8 or not readiness:
                    report.fail(path, f"line {line_number}: invalid completed Practice")
                if additional != max(0, steps - minimum * 8):
                    report.fail(path, f"line {line_number}: wrong additional Practice steps")
        elif phase == "main":
            practice_fields = (
                "minimum_practice_rounds",
                "completed_practice_rounds",
                "completed_practice_steps",
                "additional_practice_steps",
            )
            if any(row.get(name, "").strip() for name in practice_fields):
                report.fail(path, f"line {line_number}: Main row contains Practice counts")
        else:
            report.fail(path, f"line {line_number}: invalid task_phase={phase!r}")


def validate_trajectory(
    path: Path,
    rows: list[dict[str, str]],
    report: Report,
) -> None:
    validate_normalized(
        path,
        rows,
        [("gazeNormalizedX", "gazeNormalizedY"), ("cursorNormalizedX", "cursorNormalizedY")],
        report,
    )
    previous_index = -1
    previous_time = -math.inf
    for line_number, row in enumerate(rows, 2):
        index = to_int(row.get("sampleIndex", ""))
        timestamp = to_float(row.get("timestamp", ""))
        if index is None or timestamp is None:
            report.fail(path, f"line {line_number}: invalid sample index or timestamp")
            continue
        if index != previous_index + 1:
            report.fail(path, f"line {line_number}: non-contiguous sampleIndex={index}")
        if timestamp < previous_time:
            report.fail(path, f"line {line_number}: timestamp moved backward")
        previous_index = index
        previous_time = timestamp


def validate_file(
    path: Path,
    report: Report,
    t1_rows: list[tuple[Path, dict[str, str]]],
) -> None:
    try:
        with path.open("r", encoding="utf-8-sig", newline="") as handle:
            raw_rows = list(csv.reader(handle))
    except (OSError, UnicodeError, csv.Error) as error:
        report.fail(path, f"could not read CSV: {error}")
        return

    report.checked_files += 1
    if not raw_rows:
        report.fail(path, "empty file")
        return
    header = raw_rows[0]
    if not header or len(set(header)) != len(header):
        report.fail(path, "empty or duplicate header columns")
        return
    expected_width = len(header)
    for line_number, values in enumerate(raw_rows[1:], 2):
        if len(values) != expected_width:
            report.fail(
                path,
                f"line {line_number}: expected {expected_width} columns, found {len(values)}",
            )

    rows = [
        dict(zip(header, values))
        for values in raw_rows[1:]
        if len(values) == expected_width
    ]
    report.checked_rows += len(rows)
    if not rows:
        report.warn(path, "header only; no data rows")
        return

    kind = classify(header)
    if kind == "unknown":
        report.warn(path, "unrecognized CSV schema; only structural checks were applied")
        return

    validate_identity(path, header, rows, report)
    if kind == "t1_results":
        validate_t1_results(path, rows, report)
        t1_rows.extend((path, row) for row in rows)
    elif kind == "t2_results":
        validate_t2_results(path, rows, report)
    elif kind == "trajectory":
        validate_trajectory(path, rows, report)


def run_validation(paths: list[Path], require_complete_t1: bool) -> Report:
    report = Report()
    t1_rows: list[tuple[Path, dict[str, str]]] = []
    for path in paths:
        validate_file(path, report, t1_rows)
    if require_complete_t1:
        validate_t1_complete(t1_rows, report)
    return report


def write_self_test_fixture(directory: Path, invalid: bool) -> Path:
    path = directory / ("invalid_T2_results.csv" if invalid else "valid_T2_results.csv")
    header = [
        "participant_id", "allocation_code", "session_id", "run_id", "schema_version",
        "condition_order", "method", "content_set", "task_phase", "task_start_time",
        "task_end_time", "duration", "minimum_practice_rounds",
        "completed_practice_rounds", "completed_practice_steps",
        "additional_practice_steps", "readiness_confirmed", "selected_candidate",
        "post_timeout_final_selection", "post_timeout_selection_time",
        "youtube_operation_count", "youtube_pause_count", "youtube_play_count",
        "youtube_seek_count", "youtube_total_pause_time", "web_scroll_amount",
        "web_click_count", "detail_page_open_count", "candidate_add_count",
        "candidate_remove_count", "v2d_completion_time", "d2v_completion_time",
        "display_switch_count", "controller_movement_amount",
        "controller_rotation_amount", "clutch_count", "result", "note",
    ]
    row = [
        "P901", "P-B3-Ray", "P901-T2-Practice-Rtest", "Rtest", "2", "P-B3-Ray",
        "RaycastBaseline", "A_2024", "practice", "10.0", "20.0",
        "5.0" if invalid else "10.0", "1", "1", "8", "0", "1", "", "", "-1",
        "4", "1", "1", "1", "0.5", "1", "2", "1", "1", "1", "-1", "-1",
        "2", "0.1", "3", "0", "completed", "",
    ]
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(header)
        writer.writerow(row)
    return path


def run_self_test() -> bool:
    with tempfile.TemporaryDirectory() as temporary:
        directory = Path(temporary)
        valid = run_validation([write_self_test_fixture(directory, False)], False)
        invalid = run_validation([write_self_test_fixture(directory, True)], False)
    passed = not valid.failures and bool(invalid.failures)
    print(
        "PASS self-test: valid fixture accepted and invalid fixture rejected"
        if passed
        else "FAIL self-test"
    )
    return passed


def print_report(report: Report) -> None:
    for message in report.failures:
        print(f"FAIL {message}")
    for message in report.warnings:
        print(f"WARN {message}")
    status = "PASS" if not report.failures else "FAIL"
    print(
        f"{status} checked {report.checked_files} file(s), "
        f"{report.checked_rows} data row(s), "
        f"{len(report.failures)} failure(s), {len(report.warnings)} warning(s)"
    )


def main() -> int:
    args = parse_args()
    if args.self_test:
        return 0 if run_self_test() else 1
    paths = discover(args.inputs)
    if not paths:
        print("FAIL no CSV files found")
        return 1
    report = run_validation(paths, args.require_complete_t1)
    print_report(report)
    return 0 if not report.failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
