using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Framework.Common;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace pulse
{
	/// <summary>
	/// The polling service.
	/// </summary>
	public class PollingService : BackgroundWorker
	{
		/// <summary>
		/// Gets or sets the team foundation server.
		/// </summary>
		/// <value>
		/// The team foundation server.
		/// </value>
		public TfsConfigurationServer TeamFoundationServer { get; set; }
		
		/// <summary>
		/// Gets or sets the dashboard.
		/// </summary>
		/// <value>
		/// The dashboard.
		/// </value>
		public Dashboard Dashboard { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="PollingService"/> class.
		/// </summary>
		/// <param name="tfs">The TFS.</param>
		/// <param name="dashboard">The dashboard.</param>
		public PollingService(TfsConfigurationServer tfs, Dashboard dashboard)
		{
			this.TeamFoundationServer = tfs;
			this.Dashboard = dashboard;
		}

		/// <summary>
		/// Raises the <see cref="E:System.ComponentModel.BackgroundWorker.DoWork" /> event.
		/// </summary>
		/// <param name="e">An <see cref="T:System.EventArgs" /> that contains the event data.</param>
		protected override void OnDoWork(DoWorkEventArgs e)
		{
			base.OnDoWork(e);

			var now = DateTime.Now;
			Debug.WriteLine("Worker started at " + DateTime.Now);

			if (e.Cancel)
			{
				return;
			}

			// Get all the project collections.
			var collections = this.TeamFoundationServer.CatalogNode.QueryChildren(
				new[] { CatalogResourceTypes.ProjectCollection },
				false,
				CatalogQueryOptions.None);

			foreach (var collection in collections)
			{
				if (e.Cancel)
				{
					return;
				}

				// Find the project collection.
				var projectCollectionId = new Guid(collection.Resource.Properties["InstanceId"]);
				var projectCollection = this.TeamFoundationServer.GetTeamProjectCollection(projectCollectionId);

				if (e.Cancel)
				{
					return;
				}

				// Get the projects.
				var projects = projectCollection.CatalogNode.QueryChildren(
					new[] { CatalogResourceTypes.TeamProject },
					false,
					CatalogQueryOptions.None);
				this.Dashboard.AddProjects(projects.ToArray());

				if (e.Cancel)
				{
					return;
				}

				// Get the work history.
				var workItemStore = projectCollection.GetService<WorkItemStore>();
				var workItemCollection = workItemStore.Query("select * from workitems order by [changed date] asc");
				if (this.Dashboard.DateRefresh.HasValue)
				{
					this.Dashboard.AddWorkItems(workItemCollection.Cast<WorkItem>().Where(x => x.ChangedDate > this.Dashboard.DateRefresh.Value));
				}
				else
				{
					this.Dashboard.AddWorkItems(workItemCollection.Cast<WorkItem>());
				}

				if (e.Cancel)
				{
					return;
				}

				// Get the commit log.
				var versionControlServer = projectCollection.GetService<VersionControlServer>();
				var commits = versionControlServer.QueryHistory("$/", RecursionType.Full);
				if (this.Dashboard.DateRefresh.HasValue)
				{
					this.Dashboard.AddCommits(commits.Where(x => x.CreationDate > this.Dashboard.DateRefresh.Value));
				}
				else
				{
					this.Dashboard.AddCommits(commits);
				}
			}

			e.Result = now;
		}

		/// <summary>
		/// Raises the <see cref="E:System.ComponentModel.BackgroundWorker.RunWorkerCompleted" /> event.
		/// </summary>
		/// <param name="e">An <see cref="T:System.EventArgs" /> that contains the event data.</param>
		protected override void OnRunWorkerCompleted(RunWorkerCompletedEventArgs e)
		{
			base.OnRunWorkerCompleted(e);

			Debug.WriteLine("Worker completed at " + DateTime.Now);

			this.Dashboard.DateRefresh = (DateTime?)e.Result;
			this.Dashboard.Cache();
		}
	}
}