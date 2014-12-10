using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace pulse
{
	/// <summary>
	/// Dashboard.
	/// </summary>
	public class Dashboard
	{
		/// <summary>
		/// Gets or sets the date refresh.
		/// </summary>
		/// <value>
		/// The date refresh.
		/// </value>
		public DateTime? DateRefresh { get; set; }

		/// <summary>
		/// Gets or sets the selected item.
		/// </summary>
		/// <value>
		/// The selected item.
		/// </value>
		public int SelectedItem { get; set; }

		/// <summary>
		/// Gets or sets the server.
		/// </summary>
		/// <value>
		/// The server.
		/// </value>
		public Uri Server { get; set; }

		/// <summary>
		/// Gets the cache file.
		/// </summary>
		/// <value>
		/// The cache file.
		/// </value>
		private string CacheFile
		{
			get
			{
				if (this.Server == null)
				{
					return "$cache";
				}

				return string.Format(
					"{0}_{1}_{2}.cache",
					this.Server.Scheme,
					this.Server.Host,
					this.Server.Port);
			}
		}

		/// <summary>
		/// Gets or sets the commits.
		/// </summary>
		/// <value>
		/// The commits.
		/// </value>
		private int Commits { get; set; }

		/// <summary>
		/// Gets the height.
		/// </summary>
		/// <value>
		/// The height.
		/// </value>
		private int Height
		{
			get
			{
				return Console.WindowHeight - 2;
			}
		}

		/// <summary>
		/// Gets or sets the highlighted scores.
		/// </summary>
		/// <value>
		/// The highlighted scores.
		/// </value>
		private IList<string> HighlightedScores { get; set; }

		/// <summary>
		/// Gets or sets the points.
		/// </summary>
		/// <value>
		/// The points.
		/// </value>
		private IDictionary<string, double> Points { get; set; }

		/// <summary>
		/// Gets or sets the projects.
		/// </summary>
		/// <value>
		/// The projects.
		/// </value>
		private int Projects { get; set; }

		/// <summary>
		/// Gets or sets the scores.
		/// </summary>
		/// <value>
		/// The scores.
		/// </value>
		private IDictionary<string, double> Scores { get; set; }

		/// <summary>
		/// Gets the width.
		/// </summary>
		/// <value>
		/// The width.
		/// </value>
		private int Width
		{
			get
			{
				return Console.WindowWidth;
			}
		}

		/// <summary>
		/// Gets or sets the work items.
		/// </summary>
		/// <value>
		/// The work items.
		/// </value>
		private int WorkItems { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="Dashboard" /> class.
		/// </summary>
		/// <param name="server">The server.</param>
		public Dashboard(Uri server)
		{
			this.Commits = 0;
			this.Projects = 0;
			this.WorkItems = 0;
			this.DateRefresh = null;
			this.Server = server;
			this.SelectedItem = 1;
			this.Scores = new Dictionary<string, double>();
			this.HighlightedScores = new List<string>();
			this.Points = new Dictionary<string, double>()
			{
				{ Penalties.LackingVerbosity, -0.2 },
				{ Penalties.LackOfPurpose, -0.2 },
				{ Rewards.BugReport, 0.7 },
				{ Rewards.Collaboration, 0.7 },
				{ Rewards.CreatedWork, 0.02 },
				{ Rewards.DueDiligence, 0.4 },
				{ Rewards.FinishWork, 0.5 },
				{ Rewards.HouseCleaning, 0.1 },
				{ Rewards.Participation, 0.3 },
				{ Rewards.TakingOwnership, 0.1 },
				{ Rewards.Verbosity, 0.8 }
			};

			var cacheFile = new FileInfo(this.CacheFile);

			if (cacheFile.Exists)
			{
				using (var reader = new StreamReader(cacheFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read)))
				{
					while (!reader.EndOfStream)
					{
						var line = reader.ReadLine();
						var fields = line.Split(new char[] { ';' });

						switch (fields[0].ToLower())
						{
							case "refreshed":
								if (fields.Count() > 1 && !string.IsNullOrWhiteSpace(fields[1]))
								{
									this.DateRefresh = Convert.ToDateTime(fields[1]);
								}
								break;

							case "projects":
								this.Projects = Convert.ToInt32(fields[1]);
								break;

							case "work":
								this.WorkItems = Convert.ToInt32(fields[1]);
								break;

							case "commits":
								this.Commits = Convert.ToInt32(fields[1]);
								break;

							case "score":
								var points = Convert.ToDouble(fields[2]);

								if (!this.Scores.ContainsKey(fields[1]))
								{
									this.Scores.Add(fields[1], points);
								}
								else
								{
									this.Scores[fields[1]] = points;
								}

								break;

							case "point":
								var pointValue = Convert.ToDouble(fields[2]);

								if (!this.Points.ContainsKey(fields[1]))
								{
									this.Points.Add(fields[1], pointValue);
								}
								else
								{
									this.Points[fields[1]] = pointValue;
								}

								break;
						}
					}
				}
			}
		}

		/// <summary>
		/// Adds the commits.
		/// </summary>
		/// <param name="commits">The commits.</param>
		public void AddCommits(IEnumerable<Changeset> commits)
		{
			foreach (var commit in commits)
			{
				// Add to the overall tally.
				this.Commits++;

				var personOfInterest = commit.CommitterDisplayName;
				this.AddHighlightedScore(personOfInterest);

				if (!this.Scores.Any(x => x.Key.Equals(personOfInterest, StringComparison.InvariantCultureIgnoreCase)))
				{
					this.Scores.Add(personOfInterest, 0);
				}

				// Get a point for participation.
				this.Scores[personOfInterest] += this.GetPoints(Rewards.Participation);

				// Handle points for commits with comments.
				if (string.IsNullOrWhiteSpace(commit.Comment))
				{
					this.Scores[personOfInterest] += this.GetPoints(Penalties.LackingVerbosity);
				}
				else
				{
					this.Scores[personOfInterest] += this.GetPoints(Rewards.Verbosity);
				}

				// Handle points with associating commits with actual work.
				if (commit.AssociatedWorkItems.Count() == 0)
				{
					this.Scores[personOfInterest] += this.GetPoints(Penalties.LackOfPurpose);
				}
				else
				{
					this.Scores[personOfInterest] += this.GetPoints(Rewards.Collaboration);
				}
			}
		}

		/// <summary>
		/// Adds the projects.
		/// </summary>
		/// <param name="projects">The projects.</param>
		public void AddProjects(IEnumerable<object> projects)
		{
			this.Projects = projects.Count();
		}

		/// <summary>
		/// Adds the work items.
		/// </summary>
		/// <param name="workItems">The work items.</param>
		public void AddWorkItems(IEnumerable<WorkItem> workItems)
		{
			foreach (var item in workItems)
			{
				// Add to the overall tally.
				this.WorkItems++;

				if (!string.IsNullOrWhiteSpace(item.CreatedBy))
				{
					if (!this.Scores.Any(x => x.Key.Equals(item.CreatedBy, StringComparison.InvariantCultureIgnoreCase)))
					{
						this.Scores.Add(item.CreatedBy, 0);
					}
				}

				if (item.CreatedBy != item.ChangedBy)
				{
					if (!string.IsNullOrWhiteSpace(item.ChangedBy))
					{
						if (!this.Scores.Any(x => x.Key.Equals(item.ChangedBy, StringComparison.InvariantCultureIgnoreCase)))
						{
							this.Scores.Add(item.ChangedBy, 0);
						}

						// Add points for collaboration.
						this.Scores[item.ChangedBy] += this.GetPoints(Rewards.Collaboration);
					}
				}
				else
				{
					if (!string.IsNullOrWhiteSpace(item.CreatedBy))
					{
						// They must have updated something right?
						this.Scores[item.CreatedBy] += this.GetPoints(Rewards.Participation);
					}
				}

				var personOfInterest = item.CreatedDate < item.ChangedDate
					? item.ChangedBy
					: item.CreatedBy;
				this.AddHighlightedScore(personOfInterest);

				if (!string.IsNullOrWhiteSpace(personOfInterest))
				{
					switch (item.Type.Name.ToLower())
					{
						case "product backlog item":
							this.Scores[personOfInterest] += this.GetPoints(Rewards.Participation);
							break;

						case "user story":
							this.Scores[personOfInterest] += this.GetPoints(Rewards.Participation);
							break;

						case "feature":
							this.Scores[personOfInterest] += this.GetPoints(Rewards.Participation);
							break;

						case "task":
							this.Scores[personOfInterest] += this.GetPoints(Rewards.CreatedWork);
							break;

						case "impediment":
						case "issue":
						case "bug":
							this.Scores[personOfInterest] += this.GetPoints(Rewards.BugReport);
							break;

						case "test case":
						case "shared steps":
							this.Scores[personOfInterest] += this.GetPoints(Rewards.CreatedWork);
							break;

						default:
							Debug.WriteLine("Did not handle work item type [" + item.Type.Name + "]");
							break;
					}

					switch (item.State.ToLower())
					{
						case "new":
						case "design":
						case "to do":
						case "open":
						case "approved":
						case "active":
						case "in progress":
							this.Scores[personOfInterest] += this.GetPoints(Rewards.TakingOwnership);
							break;

						case "done":
						case "committed":
						case "resolved":
						case "closed":
							this.Scores[personOfInterest] += this.GetPoints(Rewards.FinishWork);
							break;

						case "removed":
							this.Scores[personOfInterest] += this.GetPoints(Rewards.HouseCleaning);
							break;

						default:
							Debug.WriteLine("Did not handle work item state [" + item.State + "]");
							break;
					}
				}
			}
		}

		/// <summary>
		/// Caches this instance.
		/// </summary>
		public void Cache()
		{
			var cacheFile = new FileInfo(this.CacheFile);

			using (var writer = new StreamWriter(cacheFile.Open(FileMode.OpenOrCreate, FileAccess.Write, FileShare.None)))
			{
				writer.WriteLine(string.Format("{0};{1}", "refreshed", this.DateRefresh.HasValue ? this.DateRefresh.Value.ToString() : string.Empty));
				writer.WriteLine(string.Format("{0};{1}", "projects", this.Projects));
				writer.WriteLine(string.Format("{0};{1}", "work", this.WorkItems));
				writer.WriteLine(string.Format("{0};{1}", "commits", this.Commits));

				foreach (var score in this.Scores)
				{
					writer.WriteLine(string.Format("{0};{1};{2}", "score", score.Key, score.Value));
				}

				foreach (var point in this.Points)
				{
					writer.WriteLine(string.Format("{0};{1};{2}", "point", point.Key, point.Value));
				}
			}
		}

		/// <summary>
		/// Clears the highlights.
		/// </summary>
		public void ClearHighlights()
		{
			this.HighlightedScores.Clear();
		}

		/// <summary>
		/// Updates the specified refreshing.
		/// </summary>
		public void Update()
		{
			// Header
			var buffer = new List<string>();
			buffer.AddRange(this.Statistics());

			// Divider
			buffer.Add(Padding(this.Width, "-"));

			// Leaderboard
			var leaderBoard = this.Leadboard();
			var pageSize = this.Height - buffer.Count();

			// Reset the selected item if it exceeds the leader board limitations.
			if (this.SelectedItem > leaderBoard.Count())
			{
				this.SelectedItem = leaderBoard.Count();
			}

			// Figure out what to put in the page.
			if (this.SelectedItem > pageSize)
			{
				buffer.AddRange(leaderBoard.Skip(this.SelectedItem % pageSize).Take(pageSize));
			}
			else
			{
				buffer.AddRange(leaderBoard.Take(pageSize));
			}

			// Filler
			var heightRemaining = this.Height - buffer.Count();
			for (var i = 0; i < heightRemaining; i++)
			{
				buffer.Add(string.Empty);
			}

			Console.CursorVisible = false;
			Console.SetCursorPosition(0, 0);
			foreach (var line in buffer)
			{
				foreach (var highlight in this.HighlightedScores)
				{
					if (line.Contains(highlight))
					{
						Console.ForegroundColor = ConsoleColor.Green;
						break;
					}
				}

				if (line.StartsWith(string.Format(" {0,3:###}.", this.SelectedItem)))
				{
					Console.BackgroundColor = ConsoleColor.White;
					if (Console.ForegroundColor != ConsoleColor.Green)
					{
						Console.ForegroundColor = ConsoleColor.Black;
					}
				}

				if (line.Length >= this.Width)
				{
					Console.Write(line);
				}
				else
				{
					Console.WriteLine(line);
				}

				Console.ResetColor();
			}

			// Notify with a sound
			if (this.HighlightedScores.Count() > 0)
			{
				Sound.Queue.Enqueue(Sound.Notify);
			}
		}

		/// <summary>
		/// Adds the highlighted score.
		/// </summary>
		/// <param name="person">The person.</param>
		private void AddHighlightedScore(string person)
		{
			if (this.Scores.ContainsKey(person) && !this.HighlightedScores.Contains(person))
			{
				this.HighlightedScores.Add(person);
			}
		}

		/// <summary>
		/// Gets the points.
		/// </summary>
		/// <param name="action">The action.</param>
		/// <returns>Returns the point value for a specific action.</returns>
		private double GetPoints(string action)
		{
			if (this.Points.ContainsKey(action))
			{
				return this.Points[action];
			}

			return 0.0;
		}

		/// <summary>
		/// Statisticses this instance.
		/// </summary>
		/// <returns></returns>
		private IEnumerable<string> Statistics()
		{
			var lines = new List<string>();

			var title = "pulse";
			var titlePadding = Padding((this.Width - title.Length) / 2);
			lines.Add(string.Format("{0}{1}{0}", titlePadding, title));

			var stats = string.Format(
				"p: {0} | w: {1} | c: {2} | r: {3:MM/dd/yyyy HH:mm}",
				this.Projects,
				this.WorkItems,
				this.Commits,
				this.DateRefresh.HasValue ? this.DateRefresh.Value : DateTime.Now);
			var statsPadding = Padding((this.Width - stats.Length) / 2);
			lines.Add(string.Format("{0}{1}{0}", statsPadding, stats));

			return lines;
		}

		/// <summary>
		/// Leadboards this instance.
		/// </summary>
		/// <returns></returns>
		private IEnumerable<string> Leadboard()
		{
			if (this.Scores == null || this.Scores.Count() == 0)
			{
				return new List<string>();
			}

			var rank = 0;
			return this.Scores
				.OrderByDescending(x => x.Value)
				.Select(x =>
				{
					var player = x.Key;

					var points = (int)x.Value;
					if (points < 0)
					{
						points = 0;
					}

					var basic = string.Format(" {0,3:###}. {1}{{0}}{2}", ++rank, player, points);
					return basic.Replace("{0}", Padding(this.Width - basic.Length + 2, " "));
				})
				.ToList();
		}

		/// <summary>
		/// Paddings the specified length.
		/// </summary>
		/// <param name="length">The length.</param>
		/// <param name="text">The text.</param>
		/// <returns></returns>
		private static string Padding(int length, string text = " ")
		{
			var padding = string.Empty;

			for (var i = 0; i < length; i++)
			{
				padding += text;
			}

			return padding;
		}
	}
}