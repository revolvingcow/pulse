using System.Collections.Generic;

namespace pulse
{
	/// <summary>
	/// Point values.
	/// </summary>
	public static class Points
	{
		/// <summary>
		/// The bug report.
		/// </summary>
		public const string BugReport = "BugReport";

		/// <summary>
		/// The collaboration.
		/// </summary>
		public const string Collaboration = "Collaboration";

		/// <summary>
		/// The created work.
		/// </summary>
		public const string CreatedWork = "CreatedWork";

		/// <summary>
		/// The due diligence.
		/// </summary>
		public const string DueDiligence = "DueDiligence";

		/// <summary>
		/// The finish work.
		/// </summary>
		public const string FinishWork = "FinishWork";

		/// <summary>
		/// The house cleaning.
		/// </summary>
		public const string HouseCleaning = "HouseCleaning";

		/// <summary>
		/// The lacking verbosity.
		/// </summary>
		public const string LackingVerbosity = "LackingVerbosity";

		/// <summary>
		/// The lack of purpose.
		/// </summary>
		public const string LackOfPurpose = "LackOfPurpose";

		/// <summary>
		/// The participation.
		/// </summary>
		public const string Participation = "Participation";

		/// <summary>
		/// The taking ownership.
		/// </summary>
		public const string TakingOwnership = "TakingOwnership";

		/// <summary>
		/// The verbosity.
		/// </summary>
		public const string Verbosity = "Verbosity";

		/// <summary>
		/// Defaults the values.
		/// </summary>
		/// <returns>Default values for points.</returns>
		public static IDictionary<string, double> DefaultValues()
		{
			return new Dictionary<string, double>()
			{
				{ Points.LackingVerbosity, -0.2 },
				{ Points.LackOfPurpose, -0.2 },
				{ Points.BugReport, 0.7 },
				{ Points.Collaboration, 0.7 },
				{ Points.CreatedWork, 0.02 },
				{ Points.DueDiligence, 0.4 },
				{ Points.FinishWork, 0.5 },
				{ Points.HouseCleaning, 0.1 },
				{ Points.Participation, 0.3 },
				{ Points.TakingOwnership, 0.1 },
				{ Points.Verbosity, 0.8 }
			};
		}

		/// <summary>
		/// Defaults the values zeroed.
		/// </summary>
		/// <returns></returns>
		public static IDictionary<string, double> EmptyDefaultValues()
		{
			return new Dictionary<string, double>()
			{
				{ Points.LackingVerbosity, 0 },
				{ Points.LackOfPurpose, 0 },
				{ Points.BugReport, 0 },
				{ Points.Collaboration, 0 },
				{ Points.CreatedWork, 0 },
				{ Points.DueDiligence, 0 },
				{ Points.FinishWork, 0 },
				{ Points.HouseCleaning, 0 },
				{ Points.Participation, 0 },
				{ Points.TakingOwnership, 0 },
				{ Points.Verbosity, 0 }
			};
		}
	}
}