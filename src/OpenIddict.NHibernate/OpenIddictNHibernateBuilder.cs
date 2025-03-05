using System;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using OpenIddict.Core;
using OpenIddict.NHibernate.Models;

namespace OpenIddict.NHibernate
{
	/// <summary>
	/// Exposes the necessary methods required to configure the OpenIddict NHibernate services.
	/// </summary>
	public class OpenIddictNHibernateBuilder
	{
		/// <summary>
		/// Initializes a new instance of <see cref="OpenIddictNHibernateBuilder"/>.
		/// </summary>
		/// <param name="services">The services collection.</param>
		public OpenIddictNHibernateBuilder(IServiceCollection services)
		{
			ArgumentNullException.ThrowIfNull(services);

			this.Services = services;
		}

		/// <summary>
		/// Gets the services collection.
		/// </summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public IServiceCollection Services { get; }

		/// <summary>
		/// Amends the default OpenIddict NHibernate configuration.
		/// </summary>
		/// <param name="configuration">The delegate used to configure the OpenIddict options.</param>
		/// <remarks>This extension can be safely called multiple times.</remarks>
		/// <returns>The <see cref="OpenIddictNHibernateBuilder"/>.</returns>
		public OpenIddictNHibernateBuilder Configure(Action<OpenIddictNHibernateOptions> configuration)
		{
			ArgumentNullException.ThrowIfNull(configuration);

			this.Services.Configure(configuration);

			return this;
		}

		/// <summary>
		/// Configures the NHibernate stores to use the specified session factory
		/// instead of retrieving it from the dependency injection container.
		/// </summary>
		/// <param name="factory">The <see cref="ISessionFactory"/>.</param>
		/// <returns>The <see cref="OpenIddictNHibernateBuilder"/>.</returns>
		public OpenIddictNHibernateBuilder UseSessionFactory(ISessionFactory factory)
		{
			ArgumentNullException.ThrowIfNull(factory);

			return this.Configure(options => options.SessionFactory = factory);
		}

		/// <summary>
		/// Configures OpenIddict to use the default OpenIddict Entity Framework entities, with the specified key type.
		/// </summary>
		/// <returns>The <see cref="OpenIddictNHibernateBuilder"/>.</returns>
		public OpenIddictNHibernateBuilder ReplaceDefaultEntities<TKey>()
			where TKey : IEquatable<TKey>
		{
			return this.ReplaceDefaultEntities<OpenIddictApplication<TKey>, OpenIddictAuthorization<TKey>, OpenIddictScope<TKey>, OpenIddictToken<TKey>, TKey>();
		}

		/// <summary>
		/// Configures OpenIddict to use the specified entities, derived from the default OpenIddict Entity Framework entities.
		/// </summary>
		/// <returns>The <see cref="OpenIddictNHibernateBuilder"/>.</returns>
		public OpenIddictNHibernateBuilder ReplaceDefaultEntities<TApplication, TAuthorization, TScope, TToken, TKey>()
			where TApplication : OpenIddictApplication<TKey, TAuthorization, TToken>
			where TAuthorization : OpenIddictAuthorization<TKey, TApplication, TToken>
			where TScope : OpenIddictScope<TKey>
			where TToken : OpenIddictToken<TKey, TApplication, TAuthorization>
			where TKey : IEquatable<TKey>
		{
			this.Services.Configure<OpenIddictCoreOptions>(options =>
				{
					options.DefaultApplicationType = typeof(TApplication);
					options.DefaultAuthorizationType = typeof(TAuthorization);
					options.DefaultScopeType = typeof(TScope);
					options.DefaultTokenType = typeof(TToken);
				}
			);

			return this;
		}
	}
}
