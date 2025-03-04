using System;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.NHibernate.Models;
using OpenIddict.NHibernate.Stores;

namespace OpenIddict.NHibernate.Resolvers
{
	/// <summary>
	/// Exposes a method allowing to resolve an application store.
	/// </summary>
	public class OpenIddictApplicationStoreResolver : IOpenIddictApplicationStoreResolver
	{
		private readonly TypeResolutionCache cache;
		private readonly IServiceProvider provider;

		public OpenIddictApplicationStoreResolver(TypeResolutionCache cache
			, IServiceProvider provider
		)
		{
			this.cache = cache;
			this.provider = provider;
		}

		/// <summary>
		/// Returns an application store compatible with the specified application type or throws an
		/// <see cref="InvalidOperationException"/> if no store can be built using the specified type.
		/// </summary>
		/// <typeparam name="TApplication">The type of the Application entity.</typeparam>
		/// <returns>An <see cref="IOpenIddictApplicationStore{TApplication}"/>.</returns>
		public IOpenIddictApplicationStore<TApplication> Get<TApplication>()
			where TApplication : class
		{
			var store = this.provider.GetService<IOpenIddictApplicationStore<TApplication>>();
			if (store != null)
			{
				return store;
			}

			var type = this.cache.GetOrAdd(typeof(TApplication), key =>
				{
					var root = OpenIddictHelpers.FindGenericBaseType(key, typeof(OpenIddictApplication<,,>));
					if (root == null)
					{
						throw new InvalidOperationException(new StringBuilder()
							.AppendLine("The specified application type is not compatible with the NHibernate stores.")
							.Append("When enabling the NHibernate stores, make sure you use the built-in ")
							.Append("'OpenIddictApplication' entity (from the 'OpenIddict.NHibernate.Models' package) ")
							.Append("or a custom entity that inherits from the generic 'OpenIddictApplication' entity.")
							.ToString()
						);
					}

					return typeof(OpenIddictApplicationStore<,,,>).MakeGenericType(/* TApplication: */ key
						, /* TAuthorization: */ root.GenericTypeArguments[1]
						, /* TToken: */ root.GenericTypeArguments[2]
						, /* TKey: */ root.GenericTypeArguments[0]
					);
				}
			);

			return (IOpenIddictApplicationStore<TApplication>)this.provider.GetRequiredService(type);
		}

		// Note: NHibernate resolvers are registered as scoped dependencies as their inner
		// service provider must be able to resolve scoped services (typically, the store they return).
		// To avoid having to declare a static type resolution cache, a special cache service is used
		// here and registered as a singleton dependency so that its content persists beyond the scope.
		public class TypeResolutionCache : ConcurrentDictionary<Type, Type>
		{
		}
	}
}
