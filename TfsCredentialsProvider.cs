using System;
using System.Net;
using Microsoft.TeamFoundation.Client;

namespace pulse
{
	/// <summary>
	/// TFS credential provider.
	/// </summary>
	/// <remarks>Currently not used but may want to.</remarks>
	public class TfsCredentialProvider : ICredentialsProvider
	{
		/// <summary>
		/// Gets the credentials.
		/// </summary>
		/// <param name="uri">The URI.</param>
		/// <param name="failedCredentials">The failed credentials.</param>
		/// <returns></returns>
		public ICredentials GetCredentials(Uri uri, ICredentials failedCredentials)
		{
			return new NetworkCredential("UserName", "Password", "Domain");
		}

		/// <summary>
		/// Notifies the credentials authenticated.
		/// </summary>
		/// <param name="uri">The URI.</param>
		/// <exception cref="System.NotImplementedException"></exception>
		public void NotifyCredentialsAuthenticated(Uri uri)
		{
			throw new NotImplementedException();
		}
	}
}