using System;
using System.Collections.Generic;
using System.Linq;
using ProjectChronos.Models;

namespace ProjectChronos.ViewModels;

public class TimelineReportExportInput
{
	public string Title { get; }

	public string SectionLabel { get; }

	public IReadOnlyList<string> FooterNotes { get; }

	public double CanvasWidth { get; }

	public double CanvasHeight { get; }

	public IReadOnlyList<SimulationEventMarker> Events { get; }

	public string OutputPath { get; }

	public double TimeResolution { get; }

	public TimelineReportExportInput(string title, string sectionLabel, IEnumerable<string> footerNotes, double canvasWidth, double canvasHeight, IReadOnlyList<SimulationEventMarker> events, string outputPath, double timeResolution = 0.01)
	{
		Title = (string.IsNullOrWhiteSpace(title) ? "Timeline Report" : title);
		SectionLabel = (string.IsNullOrWhiteSpace(sectionLabel) ? "Time-Event" : sectionLabel);
		FooterNotes = new List<string>(footerNotes ?? Enumerable.Empty<string>());
		CanvasWidth = ((canvasWidth > 0.0) ? canvasWidth : 1920.0);
		CanvasHeight = ((canvasHeight > 0.0) ? canvasHeight : 0.0);
		Events = events ?? Array.Empty<SimulationEventMarker>();
		OutputPath = outputPath;
		TimeResolution = ((timeResolution > 0.0) ? timeResolution : 0.01);
	}
}
