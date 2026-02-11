import json
import os
from typing import List, Tuple

import cv2
import numpy as np

MAPS_DIR = os.path.join("PitWall.UI", "Assets", "Tracks", "maps")
OUTLINES_DIR = os.path.join("PitWall.UI", "Assets", "Tracks", "outlines")
POINT_COUNT = 200


def resample_contour(points: List[Tuple[float, float]], count: int) -> List[Tuple[float, float]]:
    if not points:
        return []

    # Ensure closed loop
    if points[0] != points[-1]:
        points = points + [points[0]]

    distances = [0.0]
    for i in range(1, len(points)):
        dx = points[i][0] - points[i - 1][0]
        dy = points[i][1] - points[i - 1][1]
        distances.append(distances[-1] + (dx * dx + dy * dy) ** 0.5)

    total = distances[-1]
    if total <= 0:
        return points

    targets = [i * total / count for i in range(count)]
    sampled = []
    idx = 1
    for t in targets:
        while idx < len(distances) and distances[idx] < t:
            idx += 1
        if idx >= len(distances):
            sampled.append(points[-1])
            continue
        prev_d = distances[idx - 1]
        next_d = distances[idx]
        if next_d - prev_d == 0:
            sampled.append(points[idx])
            continue
        ratio = (t - prev_d) / (next_d - prev_d)
        x = points[idx - 1][0] + ratio * (points[idx][0] - points[idx - 1][0])
        y = points[idx - 1][1] + ratio * (points[idx][1] - points[idx - 1][1])
        sampled.append((x, y))

    return sampled


def extract_outline(image_path: str) -> List[Tuple[float, float]]:
    image = cv2.imread(image_path)
    if image is None:
        return []

    height, width = image.shape[:2]
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    blurred = cv2.GaussianBlur(gray, (5, 5), 0)

    edges = cv2.Canny(blurred, 50, 150)
    edges = cv2.dilate(edges, np.ones((3, 3), np.uint8), iterations=1)

    contours, _ = cv2.findContours(edges, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    if not contours:
        return []

    contour = max(contours, key=cv2.contourArea)
    if contour is None or len(contour) < 10:
        return []

    raw_points = [(float(pt[0][0]), float(pt[0][1])) for pt in contour]
    sampled = resample_contour(raw_points, POINT_COUNT)

    normalized = []
    for x, y in sampled:
        nx = x / max(1.0, width - 1)
        ny = 1.0 - (y / max(1.0, height - 1))
        normalized.append((nx, ny))

    return normalized


def main() -> None:
    maps_dir = os.path.abspath(MAPS_DIR)
    outlines_dir = os.path.abspath(OUTLINES_DIR)
    os.makedirs(outlines_dir, exist_ok=True)

    for filename in os.listdir(maps_dir):
        name, ext = os.path.splitext(filename)
        if ext.lower() not in {".png", ".jpg", ".jpeg"}:
            continue

        image_path = os.path.join(maps_dir, filename)
        outline = extract_outline(image_path)
        if not outline:
            print(f"Skipping {filename}: no contour found")
            continue

        payload = [
            {"x": round(point[0], 4), "y": round(point[1], 4)}
            for point in outline
        ]

        output_path = os.path.join(outlines_dir, f"{name}.json")
        with open(output_path, "w", encoding="utf-8") as handle:
            json.dump(payload, handle, separators=(",", ":"))

        print(f"Wrote {output_path} ({len(payload)} points)")


if __name__ == "__main__":
    main()
