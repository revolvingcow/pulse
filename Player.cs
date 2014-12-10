using System.Collections.Generic;

namespace pulse
{
	/// <summary>
	/// Player class.
	/// </summary>
	public class Player
	{
		/// <summary>
		/// Gets or sets the name.
		/// </summary>
		/// <value>
		/// The name.
		/// </value>
		public string Name { get; set; }

		/// <summary>
		/// Gets or sets the point metrics.
		/// </summary>
		/// <value>
		/// The point metrics.
		/// </value>
		public IDictionary<string, double> PointMetrics { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="Player"/> class.
		/// </summary>
		public Player(string name)
		{
			this.Name = name;
			this.PointMetrics = Points.EmptyDefaultValues();
		}

		/// <summary>
		/// Scores the specified point values.
		/// </summary>
		/// <param name="pointValues">The point values.</param>
		/// <returns>The score with the given values per metric.</returns>
		public double Score(IDictionary<string, double> pointValues)
		{
			var score = 0.0;

			foreach (var metric in this.PointMetrics)
			{
				if (pointValues.ContainsKey(metric.Key))
				{
					score += metric.Value * pointValues[metric.Key];
				}
			}

			return score;
		}
	}
}