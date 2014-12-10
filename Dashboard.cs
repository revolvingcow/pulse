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
		private IDictionary<string, double> PointValues { get; set; }

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
		private IList<Player> Players { get; set; }

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
			this.Players = new List<Player>();
			this.HighlightedScores = new List<string>();
			this.PointValues = Points.DefaultValues();

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
								var pointMetrics = Points.EmptyDefaultValues();
								for (var i = 2; i < fields.Length; i++)
								{
									var parts = fields[i].Split(new char[] { '=' });
									if (parts.Length == 2)
									{
										double metricValue;
										if (!double.TryParse(parts[1], out metricValue))
										{
											metricValue = 0.0;
										}

										if (pointMetrics.ContainsKey(parts[0]))
										{
											pointMetrics[parts[0]] = metricValue;
										}
										else
										{
											pointMetrics.Add(parts[0], metricValue);
										}
									}
								}

								if (this.Players.Any(x => x.Name.Equals(fields[1], StringComparison.InvariantCultureIgnoreCase)))
								{
									this.Players.First(x => x.Name.Equals(fields[1], StringComparison.InvariantCultureIgnoreCase)).PointMetrics = pointMetrics;
								}
								else
								{
									var player = new Player(fields[1]);
									player.PointMetrics = pointMetrics;
									this.Players.Add(player);
								}

								break;

							case "point":
								var pointValue = Convert.ToDouble(fields[2]);

								if (!this.PointValues.ContainsKey(fields[1]))
								{
									this.PointValues.Add(fields[1], pointValue);
								}
								else
								{
									this.PointValues[fields[1]] = pointValue;
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
				var player = this.Players.FirstOrDefault(x => x.Name.Equals(personOfInterest, StringComparison.InvariantCultureIgnoreCase));

				if (player == null)
				{
					player = new Player(personOfInterest);
					this.Players.Add(player);
				}

				// Get a point for participation.
				player.PointMetrics[Points.Participation]++;

				// Handle points for commits with comments.
				if (string.IsNullOrWhiteSpace(commit.Comment))
				{
					player.PointMetrics[Points.LackingVerbosity]++;
				}
				else
				{
					player.PointMetrics[Points.Verbosity]++;
				}

				// Handle points with associating commits with actual work.
				if (commit.AssociatedWorkItems.Count() == 0)
				{
					player.PointMetrics[Points.LackOfPurpose]++;
				}
				else
				{
					player.PointMetrics[Points.Collaboration]++;
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

				if (item.CreatedBy != item.ChangedBy)
				{
					if (!string.IsNullOrWhiteSpace(item.ChangedBy))
					{
						var player = this.Players.FirstOrDefault(x => x.Name.Equals(item.ChangedBy, StringComparison.InvariantCultureIgnoreCase));

						if (player == null)
						{
							player = new Player(item.ChangedBy);
							this.Players.Add(player);
						}

						// Add points for collaboration.
						player.PointMetrics[Points.Collaboration]++;
					}
				}
				else
				{
					if (!string.IsNullOrWhiteSpace(item.CreatedBy))
					{
						var player = this.Players.FirstOrDefault(x => x.Name.Equals(item.CreatedBy, StringComparison.InvariantCultureIgnoreCase));

						if (player == null)
						{
							player = new Player(item.CreatedBy);
							this.Players.Add(player);
						}

						// Add points for collaboration.
						player.PointMetrics[Points.Participation]++;
					}
				}

				var personOfInterest = item.CreatedDate < item.ChangedDate
					? item.ChangedBy
					: item.CreatedBy;
				this.AddHighlightedScore(personOfInterest);

				if (!string.IsNullOrWhiteSpace(personOfInterest))
				{
					var player = this.Players.FirstOrDefault(x => x.Name.Equals(personOfInterest, StringComparison.InvariantCultureIgnoreCase));
					if (player != null)
					{
						switch (item.Type.Name.ToLower())
						{
							case "product backlog item":
								player.PointMetrics[Points.Participation]++;
								break;

							case "user story":
								player.PointMetrics[Points.Participation]++;
								break;

							case "feature":
								player.PointMetrics[Points.Participation]++;
								break;

							case "task":
								player.PointMetrics[Points.CreatedWork]++;
								break;

							case "impediment":
							case "issue":
							case "bug":
								player.PointMetrics[Points.BugReport]++;
								break;

							case "test case":
							case "shared steps":
								player.PointMetrics[Points.CreatedWork]++;
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
								player.PointMetrics[Points.TakingOwnership]++;
								break;

							case "done":
							case "committed":
							case "resolved":
							case "closed":
								player.PointMetrics[Points.FinishWork]++;
								break;

							case "removed":
								player.PointMetrics[Points.HouseCleaning]++;
								break;

							default:
								Debug.WriteLine("Did not handle work item state [" + item.State + "]");
								break;
						}
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

				foreach (var player in this.Players)
				{
					writer.WriteLine(string.Format(
						"{0};{1};{2}",
						"score",
						player.Name, string.Join(";", player.PointMetrics.Select(x => string.Format("{0}={1}", x.Key, x.Value)).ToArray())));
				}

				foreach (var point in this.PointValues)
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

			// Clear the displayed highlighting.
			this.ClearHighlights();
		}

		/// <summary>
		/// Adds the highlighted score.
		/// </summary>
		/// <param name="person">The person.</param>
		private void AddHighlightedScore(string person)
		{
			if (this.Players.Any(x => x.Name.Equals(person, StringComparison.InvariantCultureIgnoreCase)) && !this.HighlightedScores.Contains(person))
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
			if (this.PointValues.ContainsKey(action))
			{
				return this.PointValues[action];
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
			if (this.Players == null || this.Players.Count() == 0)
			{
				return new List<string>();
			}

			var rank = 0;
			return this.Players
				.Select(x => new KeyValuePair<string, double>(x.Name, x.Score(this.PointValues)))
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