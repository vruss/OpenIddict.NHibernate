using NHibernate;

namespace OpenIddict.NHibernate
{
	/// <summary>
	/// Provides various settings needed to configure the OpenIddict NHibernate integration.
	/// </summary>
	public class OpenIddictNHibernateOptions
	{
		/// <summary>
		/// Gets or sets the session factory used by the OpenIddict NHibernate stores.
		/// If none is explicitly set, the session factory is resolved from the DI container.
		/// </summary>
		public ISessionFactory? SessionFactory { get; set; }
	}
}
