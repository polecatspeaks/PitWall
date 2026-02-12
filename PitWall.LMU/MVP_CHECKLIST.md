# PitWall LMU — MVP Checklist

## UI Refresh & Telemetry
- [x] UI refresh rate at 10 Hz with 100 Hz telemetry ingest.

## Strategy Tab
- [x] Strategy tab updates with recommendation and confidence.
- [x] Recommendation fallback behavior verified on API failure.

## Agent Endpoints
- [x] `GET /agent/config` — request construction and error handling verified.
- [x] `PUT /agent/config` — request construction and error handling verified.
- [x] `GET /agent/llm/discover` — request construction and error handling verified.
- [x] `POST /agent/query` — request construction, error handling, and response parsing verified.

## Track Map Visualization
- [x] Track map visualization renders car position — outline and map assets exist for multiple tracks (Silverstone, Spa, Monza, etc.). TrackMapService tests verify map frame production, current point rendering, and graceful handling of missing data.
