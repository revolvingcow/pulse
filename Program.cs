using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.TeamFoundation.Client;

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

			// Start the TFS worker.
			var worker = new PollingService(configurationServer, dashboard);

			// Create a timer to run this in an interval.
			var timer = new Timer(
				(e) => {
					worker.RunWorkerAsync();
				},
				null,
				new TimeSpan(0, 0, 0, 0, 200),
				new TimeSpan(0, 15, 0));
			
			var running = true;
			while (running)
			{
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

				if (running)
				{
					autoResetEvent.WaitOne(new TimeSpan(0, 0, 0, 0, 100));
					dashboard.Update();

					// Play any queued sounds.
					while (Sound.Queue.Count > 0)
					{
						Sound.Queue.Dequeue()();
					}

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
					// Stub.
				}
				finally
				{
					timer.Dispose();
				}
			}
		}
	}
}