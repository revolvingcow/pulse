using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Framework.Common;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace pulse
{
	/// <summary>
	/// The program.
	/// </summary>
	public class Program
	{
		/// <summary>
		/// Mains the specified arguments.
		/// </summary>
		/// <param name="args">The arguments.</param>
		public static void Main(string[] args)
		{
			if (args.Length != 1)
			{
				Console.WriteLine("Missing path to TFS project collection.");
				return;
			}

			var now = DateTime.Now;
			var autoResetEvent = new AutoResetEvent(true);
			var uri = new Uri(args[0]);
			var dashboard = new Dashboard(uri);
			var configurationServer = TfsConfigurationServerFactory.GetConfigurationServer(uri, new UICredentialsProvider());
			configurationServer.EnsureAuthenticated();

			// Start the sound board.
			var soundBoard = new BackgroundWorker();
			soundBoard.DoWork += (o, a) =>
			{
				while (!a.Cancel)
				{
					if (Sound.Queue.Count > 0)
					{
						Sound.Queue.Dequeue()();
					}
				}
			};
			soundBoard.RunWorkerAsync();

			// Start the TFS worker.
			var worker = new BackgroundWorker();
			worker.DoWork += (o, a) =>
			{
				now = DateTime.Now;
				dashboard.ClearHighlights();
				Debug.WriteLine("Worker started at " + DateTime.Now);

				if (a.Cancel)
				{
					return;
				}

				// Get all the project collections.
				var collections = configurationServer.CatalogNode.QueryChildren(
					new[] { CatalogResourceTypes.ProjectCollection },
					false,
					CatalogQueryOptions.None);

				foreach (var collection in collections)
				{
					if (a.Cancel)
					{
						return;
					}

					// Find the project collection.
					var projectCollectionId = new Guid(collection.Resource.Properties["InstanceId"]);
					var projectCollection = configurationServer.GetTeamProjectCollection(projectCollectionId);

					if (a.Cancel)
					{
						return;
					}

					// Get the projects.
					var projects = projectCollection.CatalogNode.QueryChildren(
						new[] { CatalogResourceTypes.TeamProject },
						false,
						CatalogQueryOptions.None);
					dashboard.AddProjects(projects.ToArray());

					if (a.Cancel)
					{
						return;
					}

					// Get the work history.
					var workItemStore = projectCollection.GetService<WorkItemStore>();
					var workItemCollection = workItemStore.Query("select * from workitems order by [changed date] asc");
					if (dashboard.DateRefresh.HasValue)
					{
						dashboard.AddWorkItems(workItemCollection.Cast<WorkItem>().Where(x => x.ChangedDate > dashboard.DateRefresh.Value));
					}
					else
					{
						dashboard.AddWorkItems(workItemCollection.Cast<WorkItem>());
					}

					if (a.Cancel)
					{
						return;
					}

					// Get the commit log.
					var versionControlServer = projectCollection.GetService<VersionControlServer>();
					var commits = versionControlServer.QueryHistory("$/", RecursionType.Full);
					if (dashboard.DateRefresh.HasValue)
					{
						dashboard.AddCommits(commits.Where(x => x.CreationDate > dashboard.DateRefresh.Value));
					}
					else
					{
						dashboard.AddCommits(commits);
					}
				}
			};
			worker.RunWorkerCompleted += (o, a) =>
			{
				Debug.WriteLine("Worker completed at " + DateTime.Now);

				dashboard.DateRefresh = now;
				dashboard.Cache();
				dashboard.Update();

				worker.RunWorkerAsync();
			};
			worker.RunWorkerAsync();

			var running = true;
			while (running)
			{
				dashboard.Update();

				// Listen for key events.
				if (Console.KeyAvailable)
				{
					var key = Console.ReadKey(true);
						
					switch (key.Key)
					{
						case ConsoleKey.PageDown:
							dashboard.SelectedItem += 20;
							break;

						case ConsoleKey.J:
						case ConsoleKey.DownArrow:
							// Move down.
							dashboard.SelectedItem++;
							break;

						case ConsoleKey.PageUp:
							dashboard.SelectedItem -= 20;
							if (dashboard.SelectedItem < 1)
							{
								dashboard.SelectedItem = 1;
							}
							break;

						case ConsoleKey.UpArrow:
						case ConsoleKey.K:
							// Move up.
							if (dashboard.SelectedItem > 0)
							{
								dashboard.SelectedItem--;
							}
							break;

						case ConsoleKey.Escape:
						case ConsoleKey.Q:
							// Exit the program.
							running = false;
							break;

						default:
							Debug.WriteLine("Unhandled key press: " + key.Key);
							break;
					}
				}

				if (running && autoResetEvent.WaitOne(new TimeSpan(0, 0, 0, 0, 100)))
				{
					autoResetEvent.Reset();
				}
			}

			if (autoResetEvent.WaitOne(new TimeSpan(0, 0, 5)))
			{
				// Clean-up.
				try
				{
					worker.CancelAsync();
				}
				catch
				{
				}

				try
				{
					soundBoard.CancelAsync();
				}
				catch
				{
				}
			}
		}
	}
}